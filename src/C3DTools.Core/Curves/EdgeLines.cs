using System;
using System.Collections.Generic;
using System.Linq;

namespace C3DTools.Core.Curves;

/// <summary>One widened pavement edge ("polyline các đoạn nối") of a curve: its side and its points.</summary>
public sealed class EdgeLine
{
    public EdgeLine(int curveNumber, bool isLeft, bool isInside, IReadOnlyList<PlanPoint> points)
    {
        CurveNumber = curveNumber;
        IsLeft = isLeft;
        IsInside = isInside;
        Points = points;
    }

    public int CurveNumber { get; }
    public bool IsLeft { get; }

    /// <summary>The side towards the curve centre (widening Wb); the other side takes Wl.</summary>
    public bool IsInside { get; }
    public IReadOnlyList<PlanPoint> Points { get; }
}

/// <summary>Pavement edges B/2 + W·f(station) along each widened curve, f ramping on the runoffs (SuperelevationPlanner).</summary>
public static class EdgeLineBuilder
{
    public static List<EdgeLine> Build(RouteGeometry route, RouteDesign design, double halfWidth, double step)
    {
        if (route == null) throw new ArgumentNullException(nameof(route));
        return Build(route.OffsetPoint, route.StartStation, route.EndStation, design, halfWidth, step);
    }

    /// <param name="offsetPoint">(station, offset) → point; positive offset = left of the direction of travel.</param>
    public static List<EdgeLine> Build(Func<double, double, PlanPoint> offsetPoint, double startStation, double endStation,
        RouteDesign design, double halfWidth, double step)
    {
        if (offsetPoint == null) throw new ArgumentNullException(nameof(offsetPoint));
        if (design == null) throw new ArgumentNullException(nameof(design));
        if (!(halfWidth > 0)) throw new ArgumentOutOfRangeException(nameof(halfWidth), "Bề rộng nửa mặt đường phải lớn hơn 0.");
        if (!(step > 0)) throw new ArgumentOutOfRangeException(nameof(step));

        var lines = new List<EdgeLine>();
        foreach (var c in design.Curves.Where(c => c.Elements != null && (c.Input.Wb > 0 || c.Input.Wl > 0)))
        {
            var entry = SuperelevationPlanner.EntryRange(c);
            var exit = SuperelevationPlanner.ExitRange(c);
            var from = Math.Max(startStation, Math.Min(entry.Begin, c.StationStart));
            var to = Math.Min(endStation, Math.Max(exit.End, c.StationEnd));
            var stations = Stations(from, to, step, entry.End, exit.Begin, c.StationArcStart, c.StationArcEnd, c.StationStart, c.StationEnd);
            var insideIsLeft = c.Turn == -1;   // left turn: centre on the left
            foreach (var (isLeft, widening) in new[] { (true, insideIsLeft ? c.Input.Wb : c.Input.Wl), (false, insideIsLeft ? c.Input.Wl : c.Input.Wb) })
            {
                if (!(widening > 0)) continue;
                var sign = isLeft ? 1 : -1;
                var points = stations.Select(s => offsetPoint(s, sign * (halfWidth + widening * SuperelevationPlanner.WideningFactor(c, s)))).ToList();
                lines.Add(new EdgeLine(c.Number, isLeft, isLeft == insideIsLeft, points));
            }
        }

        return lines;
    }

    private static List<double> Stations(double from, double to, double step, params double[] keys)
    {
        var list = new List<double>();
        for (var k = 0; from + k * step < to; k++) list.Add(from + k * step);
        list.Add(to);
        list.AddRange(keys.Where(s => s > from && s < to));
        list.Sort();
        var result = new List<double>();
        foreach (var s in list)
            if (result.Count == 0 || s - result[result.Count - 1] > 1e-6) result.Add(s);
        return result;
    }
}

/// <summary>"Hiệu chỉnh góc chuyển hướng": turns the route after a PI so its deflection becomes the given angle.</summary>
public static class PiEditor
{
    /// <summary>Deflection angle at pis[index], radians (0…π).</summary>
    public static double Deflection(IReadOnlyList<PlanPoint> pis, int index)
    {
        Check(pis, index);
        PlanPoint a = pis[index - 1], b = pis[index], c = pis[index + 1];
        double x1 = b.X - a.X, y1 = b.Y - a.Y, x2 = c.X - b.X, y2 = c.Y - b.Y;
        return Math.Abs(Math.Atan2(x1 * y2 - y1 * x2, x1 * x2 + y1 * y2));
    }

    /// <summary>
    /// A copy of pis where every point after pis[index] is rotated about it, keeping the turn direction, every leg length
    /// and every later deflection; only the deflection at index changes.
    /// </summary>
    public static List<PlanPoint> SetDeflection(IReadOnlyList<PlanPoint> pis, int index, double deltaRadians)
    {
        Check(pis, index);
        if (!(deltaRadians > 1e-9 && deltaRadians < Math.PI - 1e-9))
            throw new ArgumentOutOfRangeException(nameof(deltaRadians), "Góc chuyển hướng phải nằm giữa 0° và 180°.");

        PlanPoint a = pis[index - 1], b = pis[index], c = pis[index + 1];
        var cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
        var ccw = cross > 0 ? 1.0 : -1.0;   // a left turn deflects counter-clockwise
        var rotation = ccw * (deltaRadians - Deflection(pis, index));
        var result = pis.ToList();
        for (var k = index + 1; k < pis.Count; k++)
        {
            var v = RouteGeometry.Rotate(new PlanPoint(pis[k].X - b.X, pis[k].Y - b.Y), rotation);
            result[k] = new PlanPoint(b.X + v.X, b.Y + v.Y);
        }

        return result;
    }

    private static void Check(IReadOnlyList<PlanPoint> pis, int index)
    {
        if (pis == null) throw new ArgumentNullException(nameof(pis));
        if (index < 1 || index > pis.Count - 2) throw new ArgumentOutOfRangeException(nameof(index), "Chỉ sửa được góc tại đỉnh giữa tuyến.");
    }
}
