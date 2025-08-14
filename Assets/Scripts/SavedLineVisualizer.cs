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

    private LineRenderer lr;
    private readonly List<Vector3> _buf = new();

    void Awake()
    {
    lr = GetComponent<LineRenderer>();
    // Inspectorで設定するのでここでは設定しない
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
}
