using System;
using System.Collections.Generic;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Core.Curves;
using C3DTools.Core.Tables;

namespace C3DTools.Civil2021.Curves;

internal sealed class CurveWidening
{
    public double Wb { get; set; }
    public double Wl { get; set; }
}

/// <summary>YTCA's widening reading: Wb/Wl per curve from Offset Alignments (edges) that carry widening.</summary>
internal static class OffsetWideningReader
{
    private const int Samples = 60;   // 61 points

    /// <returns>Curve number → Wb/Wl. messages receives the nominal width per edge, as the LISP prints it.</returns>
    public static Dictionary<int, CurveWidening> Read(Alignment centreline, IEnumerable<Alignment> offsetAlignments,
        RouteDesign design, List<string> messages)
    {
        var result = new Dictionary<int, CurveWidening>();
        foreach (var curve in design.Curves) result[curve.Number] = new CurveWidening();

        foreach (var edge in offsetAlignments)
        {
            var offsets = SampleOffsets(centreline, edge);
            if (offsets.Count == 0)
            {
                messages.Add($"Bỏ qua {edge.Name}: không đo được khoảng cách tới tim tuyến.");
                continue;
            }

            var nominal = WideningEstimator.Nominal(offsets);
            var side = Math.Sign(offsets[offsets.Count / 2]);   // Civil 3D: positive offset = right
            messages.Add($"{(side < 0 ? "Mép trái" : "Mép phải")} ({edge.Name}): bề rộng danh nghĩa = {NumberFormat.Trimmed(nominal, 3)} m");

            foreach (var curve in design.Curves)
            {
                if (curve.Elements == null) continue;
                if (!TryOffsetAt(centreline, edge, curve.StationArcMid, out var offsetAtMid)) continue;
                var w = WideningEstimator.Widening(offsetAtMid, nominal);
                var target = result[curve.Number];
                // Turn +1 = right (clockwise): the inside of the curve is the right edge.
                if (curve.Turn == side) target.Wb = Math.Max(target.Wb, w);
                else target.Wl = Math.Max(target.Wl, w);
            }
        }

        return result;
    }

    /// <summary>Signed offsets from the centreline of points sampled along the offset alignment.</summary>
    private static List<double> SampleOffsets(Alignment centreline, Alignment edge)
    {
        var list = new List<double>();
        double s0 = edge.StartingStation, s1 = edge.EndingStation;
        for (var k = 0; k <= Samples; k++)
        {
            try
            {
                double x = 0, y = 0, station = 0, offset = 0;
                edge.PointLocation(s0 + (s1 - s0) * k / Samples, 0, ref x, ref y);
                centreline.StationOffset(x, y, ref station, ref offset);
                list.Add(offset);
            }
            catch (Exception)
            {
                // Point beyond the centreline's ends: StationOffset has no answer there.
            }
        }

        return list;
    }

    /// <summary>Distance from the centreline point at station to the offset alignment.</summary>
    private static bool TryOffsetAt(Alignment centreline, Alignment edge, double station, out double offset)
    {
        offset = 0;
        try
        {
            double x = 0, y = 0, edgeStation = 0;
            centreline.PointLocation(station, 0, ref x, ref y);
            edge.StationOffset(x, y, ref edgeStation, ref offset);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

/// <summary>T1, T2, P measured on an existing alignment (YTCA): the box shows these in "Chỉ cắm cọc + khung" mode.</summary>
internal static class MeasuredElements
{
    private const double Step = 0.01;

    /// <summary>PI from the tangent directions at NĐ and NC, then T1 = |PI − NĐ|, T2 = |PI − NC|, P = |PI − P point|.</summary>
    public static MeasuredCurve Measure(Alignment alignment, double startStation, double endStation, double midStation)
    {
        var start = Point(alignment, startStation);
        var end = Point(alignment, endStation);
        var startAhead = Point(alignment, Math.Min(startStation + Step, alignment.EndingStation));
        var endBehind = Point(alignment, Math.Max(endStation - Step, alignment.StartingStation));
        var mid = Point(alignment, midStation);
        return PiExtractor.Measure(start, startAhead, end, endBehind, mid);
    }

    public static PlanPoint Point(Alignment alignment, double station)
    {
        double x = 0, y = 0;
        alignment.PointLocation(station, 0, ref x, ref y);
        return new PlanPoint(x, y);
    }

    /// <summary>Unit direction of travel at station, from two points 0.01 m apart.</summary>
    public static PlanPoint Direction(Alignment alignment, double station)
    {
        var a = Math.Max(station - Step, alignment.StartingStation);
        var b = Math.Min(station + Step, alignment.EndingStation);
        var p = Point(alignment, a);
        var q = Point(alignment, b);
        double dx = q.X - p.X, dy = q.Y - p.Y;
        var l = Math.Sqrt(dx * dx + dy * dy);
        return l < 1e-12 ? new PlanPoint(1, 0) : new PlanPoint(dx / l, dy / l);
    }
}
