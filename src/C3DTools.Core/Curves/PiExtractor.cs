using System;
using System.Collections.Generic;

namespace C3DTools.Core.Curves;

/// <summary>T1, T2, P measured on the drawing (YTCA method), not computed from R and L.</summary>
public sealed class MeasuredCurve
{
    public PlanPoint Pi { get; set; }
    public double T1 { get; set; }
    public double T2 { get; set; }
    public double P { get; set; }
}

/// <summary>PIs of an existing alignment, rebuilt from its tangent lines.</summary>
public static class PiExtractor
{
    /// <summary>First line's start, the intersection of each consecutive pair of lines, last line's end.</summary>
    public static List<PlanPoint> FromTangents(IReadOnlyList<(PlanPoint start, PlanPoint end)> lines)
    {
        if (lines == null) throw new ArgumentNullException(nameof(lines));
        if (lines.Count == 0) throw new ArgumentException("Tuyến không có đoạn thẳng nào.", nameof(lines));

        var pis = new List<PlanPoint> { lines[0].start };
        for (var i = 0; i + 1 < lines.Count; i++)
            pis.Add(Intersect(lines[i].start, lines[i].end, lines[i + 1].start, lines[i + 1].end));
        pis.Add(lines[lines.Count - 1].end);
        return pis;
    }

    /// <summary>Intersection of the infinite line a1–a2 with the infinite line b1–b2.</summary>
    public static PlanPoint Intersect(PlanPoint a1, PlanPoint a2, PlanPoint b1, PlanPoint b2)
    {
        double dax = a2.X - a1.X, day = a2.Y - a1.Y, dbx = b2.X - b1.X, dby = b2.Y - b1.Y;
        var la = Math.Sqrt(dax * dax + day * day);
        var lb = Math.Sqrt(dbx * dbx + dby * dby);
        if (la < 1e-12 || lb < 1e-12)
            throw new ArgumentException("Không xác định được hướng: hai điểm của đường thẳng trùng nhau.");

        var cross = dax * dby - day * dbx;
        if (Math.Abs(cross) < 1e-9 * la * lb)
            throw new ArgumentException("Hai đường thẳng song song, không tìm được giao điểm (đỉnh).");

        var t = ((b1.X - a1.X) * dby - (b1.Y - a1.Y) * dbx) / cross;
        return new PlanPoint(a1.X + dax * t, a1.Y + day * t);
    }

    /// <summary>
    /// start/end: NĐ and NC; startAhead: a point further along the tangent at NĐ; endBehind: a point back along
    /// the tangent at NC; arcMid: the P point. PI = intersection of the two tangents.
    /// </summary>
    public static MeasuredCurve Measure(PlanPoint start, PlanPoint startAhead, PlanPoint end, PlanPoint endBehind, PlanPoint arcMid)
    {
        var pi = Intersect(start, startAhead, endBehind, end);
        return new MeasuredCurve
        {
            Pi = pi,
            T1 = Distance(pi, start),
            T2 = Distance(pi, end),
            P = Distance(pi, arcMid),
        };
    }

    private static double Distance(PlanPoint a, PlanPoint b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
