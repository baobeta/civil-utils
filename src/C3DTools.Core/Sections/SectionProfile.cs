using System;
using System.Collections.Generic;
using System.Linq;

namespace C3DTools.Core.Sections;

/// <summary>
/// One section line (ground or design) as (offset, elevation) vertices, left to right. Offsets are metres from the
/// sample line's centre (negative = left). Two vertices at the same offset make a vertical step (curb, wall).
/// </summary>
public sealed class SectionProfile
{
    public const double Tolerance = 1e-6;

    public SectionProfile(IEnumerable<(double Offset, double Elevation)> points)
    {
        if (points == null) throw new ArgumentNullException(nameof(points));
        var list = new List<(double Offset, double Elevation)>();
        // OrderBy is stable: vertices at the same offset keep the order the section gave them.
        foreach (var p in points.Where(p => IsFinite(p.Offset) && IsFinite(p.Elevation)).OrderBy(p => p.Offset))
        {
            if (list.Count > 0 && Math.Abs(list[list.Count - 1].Offset - p.Offset) <= Tolerance
                               && Math.Abs(list[list.Count - 1].Elevation - p.Elevation) <= Tolerance) continue;
            list.Add(p);
        }

        Points = list;
    }

    public IReadOnlyList<(double Offset, double Elevation)> Points { get; }

    /// <summary>At least two vertices at different offsets.</summary>
    public bool IsUsable => Points.Count >= 2 && MaxOffset - MinOffset > Tolerance;

    public double MinOffset => Points.Count == 0 ? double.NaN : Points[0].Offset;
    public double MaxOffset => Points.Count == 0 ? double.NaN : Points[Points.Count - 1].Offset;

    /// <summary>Linear interpolation; null outside the line. At a vertical step the first vertex at that offset wins.</summary>
    public double? ElevationAt(double offset)
    {
        if (Points.Count == 0 || offset < MinOffset - Tolerance || offset > MaxOffset + Tolerance) return null;
        for (var i = 0; i < Points.Count; i++)
        {
            if (Math.Abs(Points[i].Offset - offset) <= Tolerance) return Points[i].Elevation;
        }

        for (var i = 1; i < Points.Count; i++)
        {
            var a = Points[i - 1];
            var b = Points[i];
            if (offset > a.Offset && offset < b.Offset)
                return a.Elevation + (b.Elevation - a.Elevation) * (offset - a.Offset) / (b.Offset - a.Offset);
        }

        return null;
    }

    /// <summary>Elevations at both ends of the open interval (from, to), taken along the one segment that spans it; null outside.</summary>
    internal (double AtFrom, double AtTo)? AcrossInterval(double from, double to)
    {
        for (var i = 1; i < Points.Count; i++)
        {
            var a = Points[i - 1];
            var b = Points[i];
            if (b.Offset - a.Offset <= Tolerance) continue;
            if (a.Offset <= from + Tolerance && b.Offset >= to - Tolerance)
            {
                double Z(double x) => a.Elevation + (b.Elevation - a.Elevation) * (x - a.Offset) / (b.Offset - a.Offset);
                return (Z(from), Z(to));
            }
        }

        return null;
    }

    private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
}
