using UnityEngine;

/// <summary>
/// 2D の簡易幾何ユーティリティ。線分交差やクロス積など。
/// </summary>
public static class Geometry2D
{
    public const float EPS = 1e-7f;

    /// <summary>2D のクロス積 z 成分（スカラー）。</summary>
    public static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    /// <summary>
    /// 点 p が線分 a-b の内側にあるか（境界含む）。
    /// </summary>
    public static bool OnSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        return p.x >= Mathf.Min(a.x, b.x) - EPS && p.x <= Mathf.Max(a.x, b.x) + EPS &&
               p.y >= Mathf.Min(a.y, b.y) - EPS && p.y <= Mathf.Max(a.y, b.y) + EPS;
    }

    /// <summary>
    /// 2D 線分 p1-p2 と p3-p4 が交差（端点含む）するか。
    /// </summary>
    public static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
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
}

