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
        }
        else
        {
            DrawSegmentedWithGaps();
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

