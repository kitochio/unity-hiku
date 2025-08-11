using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // New Input System

public class HoverPickAndStore : MonoBehaviour
{
    [Header("Pick settings")]
    [Tooltip("2Dならtrue（Physics2D）、3Dならfalse（Physics）")]
    public bool use2D = true;

    [Tooltip("対象に必須の型名（例：Enemy や MyNamespace.MyComponent）。空なら無条件で拾う")]
    public string requiredTypeName = "";

    [Tooltip("レイキャストに使うレイヤー")]
    public LayerMask layerMask = ~0;

    [Tooltip("レイの最大距離（3D時）")]
    public float maxDistance3D = 100f;

    [Header("Store settings")]
    [Range(1, 20)]
    public int capacity = 5;

    // 内部
    private readonly Queue<GameObject> _queue = new Queue<GameObject>();
    private readonly HashSet<int> _ids = new HashSet<int>(); // 重複防止

    const float EPS = 1e-7f;

    private readonly List<Vector2> _pts = new(5); // 現在の点列（順番通り：p0, p1, ...）

    void Update()
    {
        // マウスが無い環境はスキップ
        if (Mouse.current == null || Camera.main == null) return;

        // 保存中に壊れた（Destroy）参照を掃除
        CleanupDead();

        // マウス位置からヒット判定
        var screen = Mouse.current.position.ReadValue();
        var ray = Camera.main.ScreenPointToRay(screen);

        GameObject hitObj = null;

        if (use2D)
        {
            // 2D: カメラの前方へ長めに飛ばす
            var hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity, layerMask);
            if (hit.collider != null) hitObj = hit.collider.gameObject;
        }
        else
        {
            // 3D: ふつうのRaycast
            if (Physics.Raycast(ray, out var hit, maxDistance3D, layerMask))
                hitObj = hit.collider.gameObject;
        }

        if (hitObj == null) return;

        // 型フィルタ（空なら無条件OK）
        if (!string.IsNullOrEmpty(requiredTypeName) && !HasType(hitObj, requiredTypeName))
            return;

        // 既に保存済みなら何もしない
        int id = hitObj.GetInstanceID();
        if (_ids.Contains(id)) return;

        // 交差する場合も何もしない
        Vector3 pos = hitObj.transform.position;
        if (!CanPlaceNextPoint(new Vector2(pos.x, pos.y))) return;

        // 収容上限を越えるなら最古を外す
        if (_queue.Count >= capacity)
        {
            var old = _queue.Dequeue();
            if (old != null) _ids.Remove(old.GetInstanceID());
        }

        _queue.Enqueue(hitObj);
        _ids.Add(id);
        LogSavedDetails();
    }

    /// <summary>破棄済み参照を掃除</summary>
    private void CleanupDead()
    {
        if (_queue.Count == 0) return;

        int n = _queue.Count;
        bool removed = false;

        for (int i = 0; i < n; i++)
        {
            var go = _queue.Dequeue();
            if (go == null)
            {
                removed = true;
                continue; // 捨てる
            }
            _queue.Enqueue(go); // 生きてるので戻す
        }

        // HashSetを作り直して同期
        if (removed)
        {
            _ids.Clear();
            foreach (var go in _queue)
                if (go != null) _ids.Add(go.GetInstanceID());
            Debug.Log("破棄されたオブジェクトをリストから削除しました。");
            LogSavedDetails();
        }
    }

    /// <summary>GameObjectが指定型（名前）のコンポーネントを持つか</summary>
    private bool HasType(GameObject go, string typeName)
    {
        // GetComponent(string) で探す
        var c = go.GetComponent(typeName);
        if (c != null) return true;
        return false;
    }

    [ContextMenu("Log Saved Details")]
    public void LogSavedDetails()
    {
        var arr = _queue.ToArray();
        for (int i = 0; i < arr.Length; i++)
        {
            var go = arr[i];
            if (go == null)
            {
                Debug.Log($"[{i}] (null) 破棄済み");
                continue;
            }

            // 基本情報
            string info = $"[{i}] Name: {go.name}, " +
                        $"Tag: {go.tag}, " +
                        $"Layer: {LayerMask.LayerToName(go.layer)}, " +
                        $"Position: {go.transform.position}";
            Debug.Log($"{info}");
        }
    }

    /// <summary>次の候補点 q を置けるか？（交差なし＆最大制約）</summary>
    private bool CanPlaceNextPoint(Vector2 q)
    {
        FillPointsFromQueue();
        // 最大点数チェック
        if (_pts.Count >= capacity) return false;

        // 0～1点なら交差しようがない
        if (_pts.Count < 2) return true;

        Vector2 pn = _pts[_pts.Count - 1];

        // 新規線分 [pn, q] が既存のどれとも交差しないか
        for (int i = 0; i < _pts.Count - 2; i++)
        {
            Vector2 a = _pts[i];
            Vector2 b = _pts[i + 1];
            if (SegmentsIntersect(a, b, pn, q))
                return false;
        }
        return true;
    }

        /// <summary>
    /// _queue から座標を抽出して _pts に代入（上限 capacity）
    /// </summary>
    private void FillPointsFromQueue()
    {
        _pts.Clear(); // 前回の内容は消す

        foreach (var go in _queue)
        {
            if (_pts.Count >= capacity) break; // 上限到達で終了

            if (go != null)
            {
                Vector3 pos = go.transform.position;
                _pts.Add(new Vector2(pos.x, pos.y));
            }
        }
    }

    // ---- 交差判定まわり ----
    private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
    {
        float d1 = Cross(p4 - p3, p1 - p3);
        float d2 = Cross(p4 - p3, p2 - p3);
        float d3 = Cross(p2 - p1, p3 - p1);
        float d4 = Cross(p2 - p1, p4 - p1);

        // 一般の交差
        if (((d1 > EPS && d2 < -EPS) || (d1 < -EPS && d2 > EPS)) &&
            ((d3 > EPS && d4 < -EPS) || (d3 < -EPS && d4 > EPS)))
            return true;

        // 端点が一直線上に乗るなどの境界ケース
        if (Mathf.Abs(d1) <= EPS && OnSegment(p3, p4, p1)) return true;
        if (Mathf.Abs(d2) <= EPS && OnSegment(p3, p4, p2)) return true;
        if (Mathf.Abs(d3) <= EPS && OnSegment(p1, p2, p3)) return true;
        if (Mathf.Abs(d4) <= EPS && OnSegment(p1, p2, p4)) return true;

        return false;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static bool OnSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        return p.x >= Mathf.Min(a.x, b.x) - EPS && p.x <= Mathf.Max(a.x, b.x) + EPS &&
               p.y >= Mathf.Min(a.y, b.y) - EPS && p.y <= Mathf.Max(a.y, b.y) + EPS;
    }

}
