using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SavedLineVisualizer : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private HoverPickAndStore store;

    [Header("Render")]
    [Tooltip("2Dで座標を固定する")]
    public bool force2D = true;
    [Tooltip("2D時の固定Z値（カメラ前後）")]
    public float z2D = 0f;

    [Header("Gaps Around Points")]
    [Tooltip("各頂点の前後を空ける距離（ワールド座標単位）")]
    [Min(0f)] public float gap = 0.05f;
    [Tooltip("頂点ごとに線分を分割して隙間を作る（オフなら従来の1本線）")]
    public bool useGappedSegments = true;

    [Header("Collision")]
    [Tooltip("線分にコリジョンを付与する（2DならBoxCollider2D、3DならCapsuleCollider）")]
    public bool enableCollision = false;
    [Tooltip("コリジョンの太さ（直径）。0以下でLineRendererの幅を使用")]
    public float colliderThickness = 0.05f;
    [Tooltip("コリジョンをTriggerにする")]
    public bool colliderIsTrigger = false;

    private LineRenderer lr;
    private readonly List<Vector3> _buf = new();
    private readonly List<LineRenderer> _segments = new();

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
    }

    void Update()
    {
        if (store == null) return;

        // SavedObjects から座標収集（null 安全）
        _buf.Clear();
        foreach (var go in store.SavedObjects)
        {
            if (!go) continue;
            var p = go.transform.position;
            if (force2D) p.z = z2D;
            _buf.Add(p);
        }

        // 2点未満は描画しない
        if (_buf.Count < 2)
        {
            if (useGappedSegments)
            {
                EnsureSegmentCount(0);
            }
            else
            {
                if (lr.positionCount != 0) lr.positionCount = 0;
            }
            return;
        }

        // 描画
        if (!useGappedSegments || gap <= 0f)
        {
            lr.positionCount = _buf.Count;
            lr.SetPositions(_buf.ToArray());
            if (enableCollision)
            {
                UpdateCollidersForContinuousLine();
            }
            else
            {
                // コリジョン無効化
                DisableAllSegmentColliders();
                // レンダリング用分割セグメントが残っていれば消す
                EnsureSegmentCount(0);
            }
        }
        else
        {
            DrawSegmentedWithGaps();
            if (!enableCollision)
            {
                // コリジョン無効化
                DisableAllSegmentColliders();
            }
        }
    }

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

            if (force2D)
            {
                a.z = z; b.z = z;
            }

            Vector3 ab = b - a;
            float len = ab.magnitude;
            var lrSeg = _segments[i];

            // セグメントが短すぎる場合は非表示
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
            if (force2D)
            {
                a.z = z; b.z = z;
            }

            // セグメントのLineRendererは非表示（単線モードのため）
            var lrSeg = _segments[i];
            lrSeg.positionCount = 0;

            UpdateColliderOnSegment(lrSeg.gameObject, a, b);
        }
    }

    void EnsureSegmentCount(int count)
    {
        // 余剰セグメントを削除
        for (int i = _segments.Count - 1; i >= count; i--)
        {
            if (_segments[i])
            {
                if (Application.isPlaying)
                {
                    Destroy(_segments[i].gameObject);
                }
                else
                {
                    DestroyImmediate(_segments[i].gameObject);
                }
            }
            _segments.RemoveAt(i);
        }

        // 不足分を生成
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

    void UpdateColliderOnSegment(GameObject go, Vector3 start, Vector3 end)
    {
        float length = (end - start).magnitude;
        if (length <= 0f)
        {
            DisableCollidersOn(go);
            return;
        }

        float thickness = colliderThickness > 0f ? colliderThickness : Mathf.Max(0.0001f, lr.widthMultiplier);
        bool is2D = force2D;

        if (is2D)
        {
            // 2D: BoxCollider2D
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
        else
        {
            // 3D: CapsuleCollider (X軸方向に想定)
            RemoveCollider2D(go);
            var col = go.GetComponent<CapsuleCollider>();
            if (!col) col = go.AddComponent<CapsuleCollider>();
            col.isTrigger = colliderIsTrigger;
            col.direction = 0; // X
            col.radius = thickness * 0.5f;
            col.height = Mathf.Max(thickness, length + thickness);
            col.center = Vector3.zero;

            Vector3 center = (start + end) * 0.5f;
            Vector3 v = (end - start).normalized;
            go.transform.position = center;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.right, v);
            col.enabled = true;
        }
    }

    void DisableAllSegmentColliders()
    {
        foreach (var seg in _segments)
        {
            if (!seg) continue;
            DisableCollidersOn(seg.gameObject);
        }
    }

    static void DisableCollidersOn(GameObject go)
    {
        if (!go) return;
        foreach (var c in go.GetComponents<Collider>()) c.enabled = false;
        foreach (var c in go.GetComponents<Collider2D>()) c.enabled = false;
    }

    static void RemoveCollider2D(GameObject go)
    {
        var c2d = go.GetComponent<Collider2D>();
        if (c2d)
        {
            if (Application.isPlaying) Object.Destroy(c2d); else Object.DestroyImmediate(c2d);
        }
    }

    static void RemoveCollider3D(GameObject go)
    {
        var c3d = go.GetComponent<Collider>();
        if (c3d)
        {
            if (Application.isPlaying) Object.Destroy(c3d); else Object.DestroyImmediate(c3d);
        }
    }

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
