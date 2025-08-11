using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SavedLineVisualizer : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private HoverPickAndStore store;

    [Header("Render")]
    [Tooltip("2DならZ座標を固定する")]
    public bool force2D = true;
    [Tooltip("2D時の固定Z値（カメラより手前に）")]
    public float z2D = 0f;
    [Tooltip("Lineの太さ")]
    public float lineWidth = 0.05f;

    [Header("2D Sorting (任意)")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 0;

    private LineRenderer lr;
    private readonly List<Vector3> _buf = new();

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 0;
        lr.startWidth = lr.endWidth = lineWidth;

        // 2D UI順制御したい場合（Spriteと同じ概念）
        lr.sortingLayerName = sortingLayerName;
        lr.sortingOrder = sortingOrder;

        // マテリアル未設定だと見えない場合があるので最低限
        if (lr.sharedMaterial == null)
        {
            // デフォルトのUnlit/Colorを適用（プロジェクトにより変えてOK）
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = Color.cyan;
            lr.material = mat;
        }
    }

    void Update()
    {
        if (store == null) return;

        // SavedObjects から座標を収集（nullは除外）
        _buf.Clear();
        foreach (var go in store.SavedObjects)
        {
            if (!go) continue;
            var p = go.transform.position;
            if (force2D) p.z = z2D;
            _buf.Add(p);
        }

        // 2点未満なら非表示
        if (_buf.Count < 2)
        {
            if (lr.positionCount != 0) lr.positionCount = 0;
            return;
        }

        // 直線列を反映
        lr.positionCount = _buf.Count;
        lr.SetPositions(_buf.ToArray());
    }

    // エディタ上で線のイメージだけ見たい場合のギズモ（任意）
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (store == null) return;

        Vector3? prev = null;
        foreach (var go in store.SavedObjects)
        {
            if (!go) continue;
            var p = go.transform.position;
            if (force2D) p.z = z2D;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(p, 0.05f);

            if (prev.HasValue)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(prev.Value, p);
            }
            prev = p;
        }
    }
#endif
}
