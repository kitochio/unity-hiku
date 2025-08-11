using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 接触した GameObject を順に記録し、以下の規則で棒状コリジョン(2D)を動的に更新/整理する。
///   0本目: Anchor(初期位置/Transform) - touched[0]
///   1本目: touched[1] - touched[2]
///   2本目: touched[3] - touched[4]
///   ...
/// - 各フレームで最新座標に合わせて長さ/角度を更新。
/// - 接触相手が Destroy/無効化された場合は、関係する線を自動削除（リストからも除去）。
/// </summary>
public class DynamicPairLineManager2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("BoxCollider2D を含む棒プレハブ。見た目が必要なら SpriteRenderer も。")]
    private GameObject linePrefab;

    [SerializeField, Tooltip("最初の線の片端。未設定ならこの GameObject の Start 時点の位置がアンカー。")]
    private Transform anchorTransform;

    [Header("Line Appearance & Physics")]
    [SerializeField, Tooltip("棒の太さ(BoxCollider2D.size.y / Sprite高さ)。")]
    private float lineThickness = 0.1f;

    [SerializeField, Tooltip("各端から当たり判定を無効化する距離（端の自己接触防止）。")]
    private float endMargin = 0.2f;

    [SerializeField, Tooltip("2D向け：Z=0固定。")]
    private bool forceZ0 = true;

    [SerializeField, Tooltip("見た目(SpriteRenderer)も当たり判定と同じ長さに縮める。")]
    private bool shrinkVisualAlso = true;

    [Header("Cleanup")]
    [SerializeField, Tooltip("非アクティブ(GameObject.activeInHierarchy==false)も“消えた扱い”にする。")]
    private bool treatInactiveAsGone = true;

    // 記録対象（順序保持 & 重複防止）
    private readonly List<GameObject> touchedObjects = new();
    private readonly HashSet<GameObject> touchedSet = new();

    // 生成済みライン
    private readonly List<GameObject> lines = new();

    // アンカー（Transform未指定時の固定座標）
    private Vector3 anchorPosition;

    private void Start()
    {
        anchorPosition = anchorTransform ? anchorTransform.position : transform.position;
        if (forceZ0) anchorPosition.z = 0f;
    }

    private void Update()
    {
        // 1) 破棄/無効化された接触相手をリストから除外（関連する線は後段で自動削除）
        PruneGoneContacts();

        // 2) 本数を合わせる（不足→生成／過剰→Destroy）
        EnsureLineCount();

        // 3) 最新座標で全ラインを更新
        UpdateAllLines();
    }

    /// <summary>接触相手を（未登録なら）登録する。</summary>
    public void RegisterContact(GameObject obj)
    {
        if (!IsValidContact(obj)) return;
        if (touchedSet.Contains(obj)) return;

        touchedSet.Add(obj);
        touchedObjects.Add(obj);
        // 次フレームの Update で EnsureLineCount/UpdateAllLines が走る
    }

    /// <summary>外部から明示的に“解除”したい時用。</summary>
    public void UnregisterContact(GameObject obj)
    {
        if (obj == null) return;
        if (!touchedSet.Contains(obj)) return;

        touchedSet.Remove(obj);
        touchedObjects.Remove(obj);
        // 次フレームでライン本数が再調整され、不要分は Destroy される
    }

    // ====== 内部処理 ======

    /// <summary>
    /// Destroy済み(null) や（設定により）非アクティブな相手を、記録から除外。
    /// 除外後は pairing が詰められるため、関係していた線は EnsureLineCount で削除される。
    /// </summary>
    private void PruneGoneContacts()
    {
        if (touchedObjects.Count == 0) return;

        bool removedAny = false;
        for (int i = touchedObjects.Count - 1; i >= 0; i--)
        {
            var g = touchedObjects[i];
            bool gone = (g == null) || (treatInactiveAsGone && g != null && !g.activeInHierarchy);
            if (gone)
            {
                touchedObjects.RemoveAt(i);
                // HashSet からも外す
                if (g != null) touchedSet.Remove(g);
                removedAny = true;
            }
        }

        if (removedAny)
        {
            // pairing が変わるので、この場で余剰ラインを削除しておくと視覚的に早い
            TrimExcessLines();
        }
    }

    /// <summary>必要本数に合わせてラインを生成/削除。</summary>
    private void EnsureLineCount()
    {
        int required = GetRequiredLineCount();

        // 生成
        while (lines.Count < required)
        {
            if (linePrefab == null)
            {
                Debug.LogError("[DynamicPairLineManager2D] linePrefab が未割り当てです。");
                return;
            }
            var go = Instantiate(linePrefab, Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            lines.Add(go);
        }

        // 余剰を破棄
        TrimExcessLines();
    }

    private void TrimExcessLines()
    {
        int required = GetRequiredLineCount();
        while (lines.Count > required)
        {
            var last = lines[^1];
            if (last) Destroy(last);
            lines.RemoveAt(lines.Count - 1);
        }
    }

    /// <summary>
    /// 必要ライン本数:
    ///  - touched が 0: 0 本
    ///  - touched >= 1: 1 本（Anchor - touched[0]） + floor((n-1)/2) 本（以降はペア）
    /// </summary>
    private int GetRequiredLineCount()
    {
        int n = touchedObjects.Count;
        if (n <= 0) return 0;
        return 1 + Mathf.FloorToInt((n - 1) / 2f);
    }

    /// <summary>全ラインを最新の座標に合わせて更新。</summary>
    private void UpdateAllLines()
    {
        int required = GetRequiredLineCount();
        for (int i = 0; i < required; i++)
        {
            if (!TryGetPairWorldPositions(i, out var a, out var b))
            {
                if (i < lines.Count && lines[i] != null) lines[i].SetActive(false);
                continue;
            }
            if (i < lines.Count) UpdateOrPlaceLine(lines[i], a, b);
        }
    }

    /// <summary>
    /// i本目のラインが結ぶ2点を取得。
    ///  0: Anchor - touched[0]
    ///  1: touched[1] - touched[2]
    ///  2: touched[3] - touched[4] …
    /// </summary>
    private bool TryGetPairWorldPositions(int index, out Vector3 a, out Vector3 b)
    {
        a = b = default;

        if (index == 0)
        {
            if (touchedObjects.Count < 1) return false;
            a = anchorTransform ? anchorTransform.position : anchorPosition;
            b = SafeWorldPos(touchedObjects[0]);
            return true;
        }

        int leftIdx = 2 * index - 1; // 1→1, 2→3, 3→5 …
        int rightIdx = leftIdx + 1;  // 1→2, 2→4, 3→6 …
        if (rightIdx >= touchedObjects.Count) return false;

        a = SafeWorldPos(touchedObjects[leftIdx]);
        b = SafeWorldPos(touchedObjects[rightIdx]);
        return true;
    }

    private Vector3 SafeWorldPos(GameObject obj)
    {
        if (obj == null) return Vector3.zero;
        var p = obj.transform.position;
        if (forceZ0) p.z = 0f;
        return p;
    }

    /// <summary>1本のライン（当たり判定＋見た目）を a-b に合わせて更新。</summary>
    private void UpdateOrPlaceLine(GameObject line, Vector3 a, Vector3 b)
    {
        if (line == null) return;

        Vector3 dir = b - a;
        float fullLen = dir.magnitude;
        if (fullLen <= Mathf.Epsilon) { line.SetActive(false); return; }

        // 端マージンを適用（中央のみ当たり判定）
        float effectiveLen = Mathf.Max(0f, fullLen - endMargin * 2f);
        if (effectiveLen <= 0f) { line.SetActive(false); return; }

        Vector3 n = dir / fullLen;
        Vector3 a2 = a + n * endMargin;
        Vector3 b2 = b - n * endMargin;

        Vector3 center = (a2 + b2) * 0.5f;
        float angleZ = Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg;

        // 位置・角度
        line.transform.SetPositionAndRotation(center, Quaternion.Euler(0, 0, angleZ));
        line.SetActive(true);

        // BoxCollider2D
        var box = line.GetComponent<BoxCollider2D>();
        if (box == null) box = line.AddComponent<BoxCollider2D>();
        box.size = new Vector2(effectiveLen, Mathf.Max(0.0001f, lineThickness));
        box.offset = Vector2.zero;

        // 見た目（SpriteRenderer）
        var sr = line.GetComponent<SpriteRenderer>();
        if (sr != null && shrinkVisualAlso)
        {
            if (sr.drawMode != SpriteDrawMode.Simple)
            {
                sr.size = new Vector2(effectiveLen, lineThickness);
            }
            else
            {
                line.transform.localScale = new Vector3(effectiveLen, lineThickness, 1f);
            }
        }
        else
        {
            // Simple で縮めない場合は副作用回避のため 1 に戻す
            line.transform.localScale = Vector3.one;
        }
    }

    // ====== 補助：有効な接触相手か判定 ======
    private bool IsValidContact(GameObject obj)
    {
        if (obj == null) return false;
        if (treatInactiveAsGone && !obj.activeInHierarchy) return false;
        return true;
    }
}
