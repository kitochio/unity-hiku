using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2D で新規点を追加可能か（最後の線分が既存線分と交差しないか）を判定。
/// </summary>
public sealed class PlacementValidator2D
{
    private readonly SavedObjectStore _store;
    private readonly List<Vector2> _pts = new();

    public PlacementValidator2D(SavedObjectStore store)
    {
        _store = store;
    }

    /// <summary>
    /// 現在の保存点列（最大 <paramref name="capacity"/>）に対して、
    /// 新規点 <paramref name="q"/> を追加しても交差しないか。
    /// </summary>
    public bool CanPlaceNext(Vector2 q, int capacity)
    {
        _store.RebuildRecentPoints(_pts, capacity);
        if (_pts.Count < 2) return true;

        Vector2 pn = _pts[_pts.Count - 1];
        for (int i = 0; i < _pts.Count - 2; i++)
        {
            Vector2 a = _pts[i];
            Vector2 b = _pts[i + 1];
            if (Geometry2D.SegmentsIntersect(a, b, pn, q))
                return false;
        }
        return true;
    }
}

