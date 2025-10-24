using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 2D 専用：HoverPickAndStore の保存順を線で可視化
[RequireComponent(typeof(LineRenderer))]
public class SavedLineVisualizer : MonoBehaviour
{
    [Header("Source")]
    [SerializeField, Tooltip("描画する点のソース（保存中オブジェクト群）")] private HoverPickAndStore store;

    [Header("Render")]
    [Tooltip("2D 表示用の固定 Z 座標（手前/奥の重なり制御）")]
    public float z2D = 0f;

    [Header("Gaps Around Points")]
    [Min(0f), Tooltip("各ポイントの前後を空ける長さ（0 で無効）")]
    public float gap = 0.05f;
    [Tooltip("ギャップ有りの分割線で描画（OFF で連続線）")]
    public bool useGappedSegments = true;

    [Header("Collision")]
    [Tooltip("ラインに 2D コリジョン(BoxCollider2D) を付与する")]
    public bool enableCollision = false;
    [Tooltip("コリジョンの太さ。0 以下は LineRenderer の太さを使用")]
    public float colliderThickness = 0.05f;
    [Tooltip("コリジョンを Trigger にする")]
    public bool colliderIsTrigger = false;

    // メインの LineRenderer（連続線用）
    private LineRenderer lr;
    // 現在の描画ポイント（Z を z2D に固定済み）
    private readonly List<Vector3> _buf = new();
    // 分割描画用の補助 LineRenderer 群（コリジョン生成にも流用）
    private readonly List<LineRenderer> _segments = new();
    
    [Header("Preview To Mouse")]
    [Tooltip("���݂̃}�E�X�ʒu�ւ̉E�Z�O�����g��")] public bool showPreviewToMouse = true;
    private LineRenderer _preview;

    // 2 点未満は線が引けない
    private const int MinPointsToDraw = 2;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
    }

    void Update()
    {
        if (store == null) return;

        // 1) 保存物から現在の描画ポイントを作る
        BuildPointBufferFromStore();

        // 2) 点が足りなければ表示リセット
        if (!HasEnoughPoints())
        {
            ClearLineAndSegments();
            return;
        }

        // 3) 連続線 or ギャップ分割で描画
        if (!useGappedSegments || gap <= 0f)
        {
            DrawContinuousLine();
            if (enableCollision) UpdateCollidersForContinuousLine();
            else { DisableAllSegmentColliders(); EnsureSegmentCount(0); }
        }
        else
        {
            DrawSegmentedWithGaps();
            if (!enableCollision) DisableAllSegmentColliders();
        }
    }

    void LateUpdate()
    {
        if (store == null) return;
        if (showPreviewToMouse) UpdatePreviewToMouse();
        else HidePreviewToMouse();
    }

    // 保存物から _buf を更新（Z は z2D に固定）
    void BuildPointBufferFromStore()
    {
        _buf.Clear();
        foreach (var go in store.SavedObjects)
        {
            if (!go) continue;
            _buf.Add(WithZ(go.transform.position, z2D));
        }
    }

    // 最低点数の判定
    bool HasEnoughPoints() => _buf.Count >= MinPointsToDraw;

    // Z を固定した Vector3 を返す小ヘルパー
    static Vector3 WithZ(Vector3 p, float z) { p.z = z; return p; }

    // 連続線の描画（LineRenderer 1 本）
    void DrawContinuousLine()
    {
        lr.positionCount = _buf.Count;
        lr.SetPositions(_buf.ToArray());
    }

    // 点が足りない時の片付け
    void ClearLineAndSegments()
    {
        if (useGappedSegments)
        {
            EnsureSegmentCount(0);
        }
        else
        {
            if (lr.positionCount != 0) lr.positionCount = 0;
        }
    }

    // ギャップ付き（各点の前後を空ける）で分割線描画
    void DrawSegmentedWithGaps()
    {
        int n = _buf.Count;
        int segCount = Mathf.Max(0, n - 1);
        EnsureSegmentCount(segCount);

        float z = z2D;

        for (int i = 0; i < segCount; i++)
        {
            Vector3 a = _buf[i];
            Vector3 b = _buf[i + 1];
            a.z = z; b.z = z; // 念のため再固定

            Vector3 ab = b - a;
            float len = ab.magnitude;
            var lrSeg = _segments[i];

            // 両端の空きを引くと長さが無い
            if (len <= (gap * 2f))
            {
                lrSeg.positionCount = 0;
                continue;
            }

            Vector3 dir = ab / len;
            Vector3 start = a + dir * gap;
            Vector3 end = b - dir * gap;

            lrSeg.positionCount = 2;
            lrSeg.SetPosition(0, start);
            lrSeg.SetPosition(1, end);

            if (enableCollision)
            {
                UpdateColliderOnSegment(lrSeg.gameObject, start, end);
            }
            else
            {
                DisableCollidersOn(lrSeg.gameObject);
            }
        }
    }

    // 連続線の各セグメントに 2D コリジョンを配置
    void UpdateCollidersForContinuousLine()
    {
        int n = _buf.Count;
        int segCount = Mathf.Max(0, n - 1);
        EnsureSegmentCount(segCount);

        float z = z2D;
        for (int i = 0; i < segCount; i++)
        {
            Vector3 a = _buf[i];
            Vector3 b = _buf[i + 1];
            a.z = z; b.z = z;

            // 連続描画の可視線はメイン lr を使うため、
            // セグメント lr はコリジョン専用（表示点数 0）にする
            var lrSeg = _segments[i];
            lrSeg.positionCount = 0;

            UpdateColliderOnSegment(lrSeg.gameObject, a, b);
        }
    }

    // 分割用 LineRenderer の個数を count に合わせる
    void EnsureSegmentCount(int count)
    {
        // 余剰分を削除
        for (int i = _segments.Count - 1; i >= count; i--)
        {
            if (_segments[i])
            {
                if (Application.isPlaying) Destroy(_segments[i].gameObject);
                else DestroyImmediate(_segments[i].gameObject);
            }
            _segments.RemoveAt(i);
        }

        // 足りない分を作成
        for (int i = _segments.Count; i < count; i++)
        {
            var go = new GameObject($"Segment_{i}");
            go.transform.SetParent(this.transform, worldPositionStays: false);
            var seg = go.AddComponent<LineRenderer>();
            CopyLineSettings(lr, seg);
            seg.positionCount = 0;
            _segments.Add(seg);
        }
    }

    // start-end 区間に BoxCollider2D を 1 本生成/更新
    void UpdateColliderOnSegment(GameObject go, Vector3 start, Vector3 end)
    {
        float length = (end - start).magnitude;
        if (length <= 0f)
        {
            DisableCollidersOn(go);
            return;
        }

        float thickness = colliderThickness > 0f ? colliderThickness : Mathf.Max(0.0001f, lr.widthMultiplier);

        // 2D: BoxCollider2D のみ
        RemoveCollider3D(go);
        var col = go.GetComponent<BoxCollider2D>();
        if (!col) col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = colliderIsTrigger;

        Vector3 center = (start + end) * 0.5f;
        Vector3 v = (end - start).normalized;
        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        go.transform.position = center;
        go.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        col.offset = Vector2.zero;
        col.size = new Vector2(length, thickness);
        col.enabled = true;
    }

    // 分割セグメント上の全コリジョンを無効化
    void DisableAllSegmentColliders()
    {
        foreach (var seg in _segments)
        {
            if (!seg) continue;
            DisableCollidersOn(seg.gameObject);
        }
    }

    // 任意 GameObject の 2D/3D コリジョンを無効化
    static void DisableCollidersOn(GameObject go)
    {
        if (!go) return;
        foreach (var c in go.GetComponents<Collider>()) c.enabled = false;
        foreach (var c in go.GetComponents<Collider2D>()) c.enabled = false;
    }

    // ���݂̃}�E�X�ʒu�ւ̉E�`��/�R���W����
    void UpdatePreviewToMouse()
    {
        if (_buf.Count == 0 || Camera.main == null || Mouse.current == null)
        {
            HidePreviewToMouse();
            return;
        }

        if (!TryGetMouseWorldOnZ(z2D, out var mouseWorld))
        {
            HidePreviewToMouse();
            return;
        }

        Vector2 q = new Vector2(mouseWorld.x, mouseWorld.y);

        // Use store's intersection logic
        if (!store.CanPlaceNextPointForPreview(q))
        {
            HidePreviewToMouse();
            return;
        }

        Vector3 a = _buf[_buf.Count - 1];
        Vector3 b = WithZ(mouseWorld, z2D);

        Vector3 ab = b - a;
        float len = ab.magnitude;
        float useGap = Mathf.Max(0f, gap);

        EnsurePreviewRenderer();

        if (!useGappedSegments || useGap <= 0f)
        {
            _preview.positionCount = 2;
            _preview.SetPosition(0, a);
            _preview.SetPosition(1, b);
            if (enableCollision) UpdateColliderOnSegment(_preview.gameObject, a, b); else DisableCollidersOn(_preview.gameObject);
            return;
        }

        if (len <= (useGap * 2f))
        {
            // too short considering gaps
            HidePreviewToMouse();
            return;
        }

        Vector3 dir = ab / len;
        Vector3 start = a + dir * useGap;
        Vector3 end = b - dir * useGap;

        _preview.positionCount = 2;
        _preview.SetPosition(0, start);
        _preview.SetPosition(1, end);
        if (enableCollision) UpdateColliderOnSegment(_preview.gameObject, start, end); else DisableCollidersOn(_preview.gameObject);
    }

    void HidePreviewToMouse()
    {
        if (_preview)
        {
            _preview.positionCount = 0;
            DisableCollidersOn(_preview.gameObject);
        }
    }

    void EnsurePreviewRenderer()
    {
        if (_preview) return;
        var go = new GameObject("PreviewSegment");
        go.transform.SetParent(this.transform, worldPositionStays: false);
        _preview = go.AddComponent<LineRenderer>();
        CopyLineSettings(lr, _preview);
        _preview.positionCount = 0;
    }

    // Camera �̃��C���� z2D �A���t�@�x�b�g�ł̃}�E�X�ʒu
    static bool TryGetMouseWorldOnZ(float z, out Vector3 world)
    {
        world = default;
        var cam = Camera.main;
        if (cam == null || Mouse.current == null) return false;
        Vector2 screen = Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(screen);
        float dz = ray.direction.z;
        if (Mathf.Abs(dz) < 1e-6f)
        {
            // Fallback; orthographic or nearly parallel
            world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(z - cam.transform.position.z)));
            world.z = z;
            return true;
        }
        float t = (z - ray.origin.z) / dz;
        world = ray.origin + ray.direction * t;
        world.z = z;
        return true;
    }

    // 誤って付与された 3D コリジョンを除去（2D 運用を徹底）
    static void RemoveCollider3D(GameObject go)
    {
        var c3d = go.GetComponent<Collider>();
        if (c3d)
        {
            if (Application.isPlaying) Object.Destroy(c3d); else Object.DestroyImmediate(c3d);
        }
    }

    // LineRenderer の見た目設定をコピー
    static void CopyLineSettings(LineRenderer src, LineRenderer dst)
    {
        if (!src || !dst) return;

        dst.enabled = true;
        dst.loop = false;
        dst.useWorldSpace = true;
        dst.numCornerVertices = src.numCornerVertices;
        dst.numCapVertices = src.numCapVertices;
        dst.alignment = src.alignment;
        dst.textureMode = src.textureMode;
        dst.generateLightingData = src.generateLightingData;

        dst.sharedMaterial = src.sharedMaterial;
        dst.colorGradient = src.colorGradient;
        dst.widthCurve = src.widthCurve;
        dst.widthMultiplier = src.widthMultiplier;

        dst.shadowCastingMode = src.shadowCastingMode;
        dst.receiveShadows = src.receiveShadows;
        dst.lightProbeUsage = src.lightProbeUsage;
        dst.reflectionProbeUsage = src.reflectionProbeUsage;
        dst.probeAnchor = src.probeAnchor;
        dst.sortingLayerID = src.sortingLayerID;
        dst.sortingOrder = src.sortingOrder;
    }
}
