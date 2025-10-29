using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // New Input System

// 2D 専用：マウス下のオブジェクトを順番に保存し、
// 保存順の自己交差（線が交わる）を避ける簡単な制御を行う。
public class HoverPickAndStore : MonoBehaviour
{
    [Header("Pick settings (2D only)")]
    [Tooltip("必要なコンポーネントの型名（空ならフィルタなし）")]
    public string requiredTypeName = "";

    [Tooltip("2D レイ判定に使用する LayerMask")]
    public LayerMask layerMask = ~0;

    [Header("Store settings")]
    [Range(1, 20)] public int capacity = 3;
    public IReadOnlyCollection<GameObject> SavedObjects => _queue;

    [Header("Audio")]
    [Tooltip("キューに追加成功時に鳴らす音")]
    [SerializeField] private AudioClip enqueueSfx;
    [Range(0f, 1f)] [SerializeField] private float enqueueSfxVolume = 1f;
    [Tooltip("再生に使う AudioSource（未指定なら自身から取得）")]
    [SerializeField] private AudioSource sfxSource;

    // 内部状態
    private readonly Queue<GameObject> _queue = new();
    private readonly HashSet<int> _ids = new();

    // GameDirector の経過時間に応じて capacity を増やす
    private const int CapacityStart = 3;
    private const int CapacityMax = 10;
    private const float SecondsPerIncrease = 10f;
    [SerializeField] private GameDirector gameDirector;

    // 幾何用の定数
    const float EPS = 1e-7f;

    // 交差判定用バッファ（最新順で capacity 件まで）
    private readonly List<Vector2> _pts = new();

    void Update()
    {
        UpdateDynamicCapacity();
        if (Mouse.current == null || Camera.main == null) return;

        // 破棄済み要素の掃除
        CleanupDead();

        // マウス下の 2D オブジェクトを取得
        var hitObj = GetObjectUnderMouse2D();
        if (!hitObj) return;

        // 型フィルタに適合するか
        if (!PassesTypeFilter(hitObj)) return;

        // 保存キューに追加を試みる
        TryEnqueue(hitObj);
    }

    // マウス位置から 2D コライダーを取得
    GameObject GetObjectUnderMouse2D()
    {
        var screen = Mouse.current.position.ReadValue();
        var ray = Camera.main.ScreenPointToRay(screen);
        var hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity, layerMask);
        return hit.collider ? hit.collider.gameObject : null;
    }

    // 型フィルタに一致するか
    bool PassesTypeFilter(GameObject go)
    {
        if (string.IsNullOrEmpty(requiredTypeName)) return true;
        return HasType(go, requiredTypeName);
    }

    // キューへの追加を試みる（重複・交差・満杯処理を含む）
    bool TryEnqueue(GameObject go)
    {
        int id = go.GetInstanceID();
        if (_ids.Contains(id)) return false;

        var pos = go.transform.position;
        if (!CanPlaceNextPoint(new Vector2(pos.x, pos.y))) return false;

        EvictIfFull();

        _queue.Enqueue(go);
        _ids.Add(id);
        LogSavedDetails();
        PlayEnqueueSfx();
        return true;
    }

    // 満杯なら最古を追い出す
    void EvictIfFull()
    {
        if (_queue.Count < capacity) return;

        var old = _queue.Dequeue();
        if (old != null) _ids.Remove(old.GetInstanceID());
    }

    // 動的に capacity を更新（GameDirector の経過時間に応じて）
    void UpdateDynamicCapacity()
    {
        if (gameDirector == null)
            gameDirector = FindFirstObjectByType<GameDirector>();

        int target = CapacityStart;
        if (gameDirector != null)
        {
            float elapsed = gameDirector.ElapsedTime;
            int inc = Mathf.FloorToInt(elapsed / SecondsPerIncrease);
            target = Mathf.Clamp(CapacityStart + inc, CapacityStart, CapacityMax);
        }

        capacity = target;
    }

    // 破棄済み（Destroy 済み）オブジェクトをキューから取り除く
    void CleanupDead()
    {
        if (_queue.Count == 0) return;

        int n = _queue.Count;
        bool removed = false;

        for (int i = 0; i < n; i++)
        {
            var go = _queue.Dequeue();
            if (!go)
            {
                removed = true;
                continue;
            }
            _queue.Enqueue(go);
        }

        if (removed)
        {
            _ids.Clear();
            foreach (var go in _queue)
                if (go) _ids.Add(go.GetInstanceID());
            Debug.Log("Removed destroyed objects from saved list.");
            LogSavedDetails();
        }
    }

    // GameObject が指定された型名のコンポーネントを持つか
    bool HasType(GameObject go, string typeName)
    {
        var c = go.GetComponent(typeName);
        return c != null;
    }

    [ContextMenu("Log Saved Details")]
    public void LogSavedDetails()
    {
        var arr = _queue.ToArray();
        for (int i = 0; i < arr.Length; i++)
        {
            var go = arr[i];
            if (!go)
            {
                Debug.Log($"[{i}] (null) destroyed");
                continue;
            }

            string info = $"[{i}] Name: {go.name}, " +
                          $"Tag: {go.tag}, " +
                          $"Layer: {LayerMask.LayerToName(go.layer)}, " +
                          $"Position: {go.transform.position}";
            Debug.Log(info);
        }
    }

    // 次の点 q を追加しても、既存の折れ線と自己交差しないかを判定
    bool CanPlaceNextPoint(Vector2 q)
    {
        RebuildRecentPoints();
        if (_pts.Count < 2) return true;

        Vector2 pn = _pts[_pts.Count - 1];

        for (int i = 0; i < _pts.Count - 2; i++)
        {
            Vector2 a = _pts[i];
            Vector2 b = _pts[i + 1];
            if (SegmentsIntersect(a, b, pn, q))
                return false;
        }
        return true;
    }

    // 現在のキューから最大 capacity 件までの 2D 点列を作成（先頭が古い順）
    void RebuildRecentPoints()
    {
        _pts.Clear();
        foreach (var go in _queue)
        {
            if (_pts.Count >= capacity) break;
            if (!go) continue;

            Vector3 pos = go.transform.position;
            _pts.Add(new Vector2(pos.x, pos.y));
        }
    }

    // 2D 線分同士の交差判定（厳密めの境界含む） -------------------------
    static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
    {
        float d1 = Cross(p4 - p3, p1 - p3);
        float d2 = Cross(p4 - p3, p2 - p3);
        float d3 = Cross(p2 - p1, p3 - p1);
        float d4 = Cross(p2 - p1, p4 - p1);

        if (((d1 > EPS && d2 < -EPS) || (d1 < -EPS && d2 > EPS)) &&
            ((d3 > EPS && d4 < -EPS) || (d3 < -EPS && d4 > EPS)))
            return true;

        if (Mathf.Abs(d1) <= EPS && OnSegment(p3, p4, p1)) return true;
        if (Mathf.Abs(d2) <= EPS && OnSegment(p3, p4, p2)) return true;
        if (Mathf.Abs(d3) <= EPS && OnSegment(p1, p2, p3)) return true;
        if (Mathf.Abs(d4) <= EPS && OnSegment(p1, p2, p4)) return true;

        return false;
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    static bool OnSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        return p.x >= Mathf.Min(a.x, b.x) - EPS && p.x <= Mathf.Max(a.x, b.x) + EPS &&
               p.y >= Mathf.Min(a.y, b.y) - EPS && p.y <= Mathf.Max(a.y, b.y) + EPS;
    }

    // Public wrapper for external preview checks
    public bool CanPlaceNextPointForPreview(Vector2 q) => CanPlaceNextPoint(q);

    void PlayEnqueueSfx()
    {
        var gd = gameDirector != null ? gameDirector : FindFirstObjectByType<GameDirector>();
        if (gd == null || gd.State != GameDirector.GameState.Playing) return;

        var src = sfxSource != null ? sfxSource : GetComponent<AudioSource>();
        if (src != null && enqueueSfx != null)
        {
            src.PlayOneShot(enqueueSfx, enqueueSfxVolume);
        }
    }
}
