using System;
using System.Collections.Generic;
using C3DTools.Core.Curves;

namespace C3DTools.Core.Surfaces;

[Flags]
public enum TriangleFlag { None = 0, LongEdge = 1, Outside = 2 }

/// <summary>
/// CTMATDIA "Xoá tam giác dài": which TIN triangles and edges to drop. Lengths are measured in plan (as Civil 3D's
/// maximum triangle length). maxEdge ≤ 0 turns the length rule off; a null boundary (or fewer than 3 points) the polygon rule.
/// </summary>
public static class TriangleFilter
{
    /// <summary>m: a point this close to the boundary counts as inside.</summary>
    public const double BoundaryTolerance = 1e-6;

    public static TriangleFlag Check(PlanPoint a, PlanPoint b, PlanPoint c, double maxEdge, IReadOnlyList<PlanPoint> boundary)
    {
        var flag = TriangleFlag.None;
        if (IsLong(a, b, maxEdge) || IsLong(b, c, maxEdge) || IsLong(c, a, maxEdge)) flag |= TriangleFlag.LongEdge;
        if (HasBoundary(boundary) && (IsOutside(a, b, boundary) || IsOutside(b, c, boundary) || IsOutside(c, a, boundary))) flag |= TriangleFlag.Outside;
        return flag;
    }

    /// <summary>An edge is deleted when it is longer than maxEdge or leaves the boundary (see IsOutside).</summary>
    public static bool ShouldDeleteEdge(PlanPoint a, PlanPoint b, double maxEdge, IReadOnlyList<PlanPoint> boundary) =>
        IsLong(a, b, maxEdge) || (HasBoundary(boundary) && IsOutside(a, b, boundary));

    /// <summary>
    /// The edge a→b leaves the boundary: an end or its midpoint is outside, or it properly crosses a boundary segment
    /// (with a concave boundary both ends can be inside while the edge spans a notch).
    /// </summary>
    public static bool IsOutside(PlanPoint a, PlanPoint b, IReadOnlyList<PlanPoint> boundary)
    {
        if (!HasBoundary(boundary)) return false;
        if (!IsInside(a, boundary) || !IsInside(b, boundary)) return true;
        if (!IsInside(new PlanPoint((a.X + b.X) / 2, (a.Y + b.Y) / 2), boundary)) return true;
        var n = boundary.Count;
        for (var i = 0; i < n; i++)
        {
            if (ProperlyCrosses(a, b, boundary[i], boundary[(i + 1) % n])) return true;
        }

        return false;
    }

    /// <summary>
    /// Indices into edges of the ones to delete, each physical edge once (the same edge read from both of its triangles,
    /// in either direction, is listed at its first index).
    /// </summary>
    public static List<int> EdgesToDelete(IReadOnlyList<(PlanPoint a, PlanPoint b)> edges, double maxEdge, IReadOnlyList<PlanPoint> boundary)
    {
        var result = new List<int>();
        if (edges == null) return result;
        var seen = new HashSet<(long, long, long, long)>();
        for (var i = 0; i < edges.Count; i++)
        {
            var (a, b) = edges[i];
            if (!seen.Add(Key(a, b))) continue;
            if (ShouldDeleteEdge(a, b, maxEdge, boundary)) result.Add(i);
        }

        return result;
    }

    /// <summary>Winding-number point-in-polygon; the polygon may be open or closed (last = first), either orientation, concave.</summary>
    public static bool IsInside(PlanPoint p, IReadOnlyList<PlanPoint> polygon)
    {
        if (!HasBoundary(polygon)) return false;
        var n = polygon.Count;
        var winding = 0;
        for (var i = 0; i < n; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % n];
            if (DistanceToSegment(p, a, b) <= BoundaryTolerance) return true;
            if (a.Y <= p.Y)
            {
                if (b.Y > p.Y && Cross(a, b, p) > 0) winding++;
            }
            else if (b.Y <= p.Y && Cross(a, b, p) < 0)
            {
                winding--;
            }
        }

        return winding != 0;
    }

    private static bool HasBoundary(IReadOnlyList<PlanPoint> boundary) => boundary != null && boundary.Count >= 3;

    private static bool IsLong(PlanPoint a, PlanPoint b, double maxEdge) => maxEdge > 0 && Distance(a, b) > maxEdge;

    private static double Distance(PlanPoint a, PlanPoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>&gt; 0 when p is left of a→b.</summary>
    private static double Cross(PlanPoint a, PlanPoint b, PlanPoint p) => (b.X - a.X) * (p.Y - a.Y) - (p.X - a.X) * (b.Y - a.Y);

    private static double DistanceToSegment(PlanPoint p, PlanPoint a, PlanPoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var len2 = dx * dx + dy * dy;
        var t = len2 > 0 ? ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2 : 0;
        if (t < 0) t = 0;
        else if (t > 1) t = 1;
        return Distance(p, new PlanPoint(a.X + t * dx, a.Y + t * dy));
    }

    /// <summary>
    /// Segments cross at one interior point of both: each one's ends lie strictly on opposite sides of the other.
    /// Touching at a vertex or running along the boundary does not count.
    /// </summary>
    private static bool ProperlyCrosses(PlanPoint a, PlanPoint b, PlanPoint c, PlanPoint d)
    {
        var scale = Math.Max(Distance(a, b), Distance(c, d));
        var tol = BoundaryTolerance * scale;
        var d1 = Cross(c, d, a);
        var d2 = Cross(c, d, b);
        var d3 = Cross(a, b, c);
        var d4 = Cross(a, b, d);
        return (d1 > tol && d2 < -tol || d1 < -tol && d2 > tol) && (d3 > tol && d4 < -tol || d3 < -tol && d4 > tol);
    }

    /// <summary>The same edge whichever triangle it is read from and in either direction; coordinates rounded to 1e-6 m.</summary>
    private static (long, long, long, long) Key(PlanPoint a, PlanPoint b)
    {
        var ka = Round(a);
        var kb = Round(b);
        return ka.CompareTo(kb) <= 0 ? (ka.x, ka.y, kb.x, kb.y) : (kb.x, kb.y, ka.x, ka.y);
    }

    private static (long x, long y) Round(PlanPoint p) => ((long)Math.Round(p.X * 1e6), (long)Math.Round(p.Y * 1e6));
}
