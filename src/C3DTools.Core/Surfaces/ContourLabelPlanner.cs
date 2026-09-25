using System;
using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Curves;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Surfaces;

/// <summary>A contour as a plan polyline at one elevation.</summary>
public sealed class ContourLine
{
    public ContourLine(double elevation, IReadOnlyList<PlanPoint> points)
    {
        Elevation = elevation;
        Points = points ?? throw new ArgumentNullException(nameof(points));
    }

    public double Elevation { get; }
    public IReadOnlyList<PlanPoint> Points { get; }
}

public sealed class ContourLabelOptions
{
    /// <summary>m. Elevations that are multiples of it are major contours.</summary>
    public double MajorInterval { get; set; } = 5;

    /// <summary>Most decimals of the text; trailing zeros are dropped.</summary>
    public int Decimals { get; set; } = 2;

    /// <summary>m along the line; 0 = label every crossing.</summary>
    public double Spacing { get; set; }

    /// <summary>False: major contours only.</summary>
    public bool IncludeMinor { get; set; } = true;
}

public sealed class ContourLabel
{
    public ContourLabel(PlanPoint position, double distance, double elevation, bool isMajor, string text, double rotation)
    {
        Position = position;
        Distance = distance;
        Elevation = elevation;
        IsMajor = isMajor;
        Text = text;
        Rotation = rotation;
    }

    public PlanPoint Position { get; }

    /// <summary>m from the start of the picked line.</summary>
    public double Distance { get; }

    public double Elevation { get; }
    public bool IsMajor { get; }
    public string Text { get; }

    /// <summary>Radians, along the contour, turned to read left to right (TextAngle.Readable).</summary>
    public double Rotation { get; }
}

/// <summary>
/// CTMATDIA "Ghi cao độ đồng mức": where a picked line crosses the contours, which crossings get a label (major contours
/// first, then minor ones at least Spacing away along the line from every kept label), the text and its rotation.
/// </summary>
public static class ContourLabelPlanner
{
    private const double Tolerance = 1e-9;

    public static bool IsMajor(double elevation, double majorInterval)
    {
        if (!(majorInterval > 0)) return false;
        var ratio = elevation / majorInterval;
        return Math.Abs(ratio - Math.Round(ratio)) * majorInterval < 1e-4;
    }

    public static List<ContourLabel> Plan(IReadOnlyList<PlanPoint> line, IEnumerable<ContourLine> contours, ContourLabelOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        var crossings = new List<ContourLabel>();
        if (line == null || line.Count < 2 || contours == null) return crossings;

        foreach (var contour in contours)
        {
            if (contour == null || contour.Points.Count < 2) continue;
            var isMajor = IsMajor(contour.Elevation, options.MajorInterval);
            if (!isMajor && !options.IncludeMinor) continue;
            var text = NumberFormat.Trimmed(contour.Elevation, Math.Max(0, Math.Min(6, options.Decimals)));
            var found = new List<ContourLabel>();
            var start = 0.0;
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var a = line[i];
                var b = line[i + 1];
                var length = Length(a, b);
                for (var j = 0; j + 1 < contour.Points.Count; j++)
                {
                    var c = contour.Points[j];
                    var d = contour.Points[j + 1];
                    if (!Intersect(a, b, c, d, out var t)) continue;
                    var distance = start + t * length;
                    if (found.Any(f => Math.Abs(f.Distance - distance) < 1e-6)) continue;   // at a contour or line vertex
                    var position = new PlanPoint(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y));
                    var rotation = TextAngle.Readable(Math.Atan2(d.Y - c.Y, d.X - c.X));
                    found.Add(new ContourLabel(position, distance, contour.Elevation, isMajor, text, rotation));
                }

                start += length;
            }

            crossings.AddRange(found);
        }

        crossings.Sort((x, y) => x.Distance.CompareTo(y.Distance));
        if (!(options.Spacing > 0)) return crossings;

        var kept = new List<ContourLabel>();
        foreach (var pass in new[] { true, false })
        {
            foreach (var c in crossings)
            {
                if (c.IsMajor != pass) continue;
                if (kept.Any(k => Math.Abs(k.Distance - c.Distance) < options.Spacing)) continue;
                kept.Add(c);
            }
        }

        kept.Sort((x, y) => x.Distance.CompareTo(y.Distance));
        return kept;
    }

    /// <summary>Segment a→b meets c→d; t is the parameter on a→b. Parallel and collinear segments do not count.</summary>
    private static bool Intersect(PlanPoint a, PlanPoint b, PlanPoint c, PlanPoint d, out double t)
    {
        t = 0;
        double rx = b.X - a.X, ry = b.Y - a.Y, sx = d.X - c.X, sy = d.Y - c.Y;
        var denom = rx * sy - ry * sx;
        var scale = Math.Sqrt((rx * rx + ry * ry) * (sx * sx + sy * sy));
        if (!(scale > 0) || Math.Abs(denom) <= 1e-12 * scale) return false;
        double qx = c.X - a.X, qy = c.Y - a.Y;
        t = (qx * sy - qy * sx) / denom;
        var u = (qx * ry - qy * rx) / denom;
        return t >= -Tolerance && t <= 1 + Tolerance && u >= -Tolerance && u <= 1 + Tolerance;
    }

    private static double Length(PlanPoint a, PlanPoint b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
}
