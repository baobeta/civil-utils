using System;
using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Curves;

public readonly struct PlanPoint
{
    public PlanPoint(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; }
    public double Y { get; }
}

/// <summary>What the user types for one PI. Wb = widening inside the curve (bụng), Wl = outside (lưng).</summary>
public sealed class CurveInput
{
    public double Radius { get; set; }
    public double SpiralIn { get; set; }
    public double SpiralOut { get; set; }
    public double Wb { get; set; }
    public double Wl { get; set; }

    public CurveInput Clone() => (CurveInput)MemberwiseClone();
}

public sealed class DesignedCurve
{
    /// <summary>Index into the PI list (1..n-2).</summary>
    public int PiIndex { get; set; }

    /// <summary>Đ number; collinear PIs are skipped.</summary>
    public int Number { get; set; }

    public int Turn { get; set; }
    public double DeltaRadians { get; set; }
    public CurveInput Input { get; set; }

    /// <summary>Null when the input is invalid (see Issues).</summary>
    public CurveElements Elements { get; set; }

    public double TangentBefore { get; set; }
    public double StationStart { get; set; }
    public double StationArcStart { get; set; }
    public double StationArcEnd { get; set; }
    public double StationEnd { get; set; }
    public double StationArcMid => (StationArcStart + StationArcEnd) / 2;
    public double? SuggestedWidening { get; set; }
    public List<CurveIssue> Issues { get; } = new List<CurveIssue>();

    public CurveGroup ToGroup() => new CurveGroup(Number, Input.Radius, Turn, DeltaRadians,
        Input.SpiralIn, Input.SpiralOut, StationStart, StationArcStart, StationArcEnd);
}

public sealed class RouteDesign
{
    public List<DesignedCurve> Curves { get; } = new List<DesignedCurve>();
    public double EndStation { get; set; }
    public bool CanApply => Curves.All(c => c.Issues.All(i => !i.IsError));
}

public static class RouteDesigner
{
    private const double Tolerance = 1e-4;

    public static List<PlanPoint> RemoveDuplicatePoints(IEnumerable<PlanPoint> points)
    {
        var result = new List<PlanPoint>();
        foreach (var p in points)
            if (result.Count == 0 || Distance(result[result.Count - 1], p) > 1e-6) result.Add(p);
        return result;
    }

    public static RouteDesign Design(IReadOnlyList<PlanPoint> pis, double startStation,
        IReadOnlyList<CurveInput> inputs, double designSpeed, CurveRules rules)
    {
        if (pis == null) throw new ArgumentNullException(nameof(pis));
        if (inputs == null) throw new ArgumentNullException(nameof(inputs));
        if (pis.Count < 2) throw new ArgumentException("Tuyến cần ít nhất 2 đỉnh.", nameof(pis));
        if (inputs.Count != pis.Count - 2)
            throw new ArgumentException($"Cần {pis.Count - 2} bộ thông số cong.", nameof(inputs));

        var design = new RouteDesign();
        var station = startStation;
        var previousT2 = 0.0;
        for (var i = 1; i < pis.Count - 1; i++)
        {
            var legIn = Distance(pis[i - 1], pis[i]);
            Deflection(pis[i - 1], pis[i], pis[i + 1], out var delta, out var turn);
            if (delta < 1e-6)
            {
                station += legIn - previousT2;   // straight through, as ytc: does
                previousT2 = 0;
                continue;
            }

            var input = inputs[i - 1] ?? new CurveInput();
            var curve = new DesignedCurve
            {
                PiIndex = i, Number = design.Curves.Count + 1, Turn = turn, DeltaRadians = delta, Input = input,
            };
            design.Curves.Add(curve);

            var name = "Đ" + curve.Number;
            if (!(input.Radius > 0) || input.SpiralIn < 0 || input.SpiralOut < 0 || delta > Math.PI - 1e-6)
                curve.Issues.Add(new CurveIssue(CurveIssueCode.InvalidInput, $"{name}: cần R > 0, L ≥ 0 và đỉnh không gập ngược."));
            else if ((input.SpiralIn + input.SpiralOut) / (2 * input.Radius) > delta)
                curve.Issues.Add(new CurveIssue(CurveIssueCode.SpiralTooLong, $"{name}: L quá dài so với góc chuyển hướng."));

            if (curve.Issues.Count > 0)
            {
                // Keep stationing going as if the PI had no curve, so later rows still show numbers.
                curve.StationStart = curve.StationArcStart = curve.StationArcEnd = curve.StationEnd = station + legIn - previousT2;
                station = curve.StationEnd;
                previousT2 = 0;
                continue;
            }

            var e = CurveElementsCalculator.Compute(input.Radius, delta, input.SpiralIn, input.SpiralOut);
            curve.Elements = e;
            curve.TangentBefore = legIn - previousT2 - e.T1;
            if (curve.TangentBefore < -Tolerance)
                curve.Issues.Add(new CurveIssue(CurveIssueCode.Overlap, curve.Number == 1
                    ? $"{name}: tiếp tuyến T1 vượt quá điểm đầu tuyến {NumberFormat.Fixed(-curve.TangentBefore, 2)} m."
                    : $"{name}: chồng lên đường cong trước {NumberFormat.Fixed(-curve.TangentBefore, 2)} m."));

            curve.StationStart = station + curve.TangentBefore;
            curve.StationArcStart = curve.StationStart + input.SpiralIn;
            curve.StationArcEnd = curve.StationArcStart + e.K0;
            curve.StationEnd = curve.StationArcEnd + input.SpiralOut;

            if (rules != null)
            {
                var check = CurveRuleChecker.Check(curve.ToGroup(), designSpeed, rules);
                curve.Issues.AddRange(check.Issues);
                curve.SuggestedWidening = check.Widening;
            }

            station = curve.StationEnd;
            previousT2 = e.T2;
        }

        var tail = Distance(pis[pis.Count - 2], pis[pis.Count - 1]) - previousT2;
        if (tail < -Tolerance && design.Curves.Count > 0)
        {
            var last = design.Curves[design.Curves.Count - 1];
            last.Issues.Add(new CurveIssue(CurveIssueCode.Overlap,
                $"Đ{last.Number}: tiếp tuyến T2 vượt quá điểm cuối tuyến {NumberFormat.Fixed(-tail, 2)} m."));
        }

        design.EndStation = station + tail;
        return design;
    }

    private static void Deflection(PlanPoint a, PlanPoint b, PlanPoint c, out double delta, out int turn)
    {
        double x1 = b.X - a.X, y1 = b.Y - a.Y, x2 = c.X - b.X, y2 = c.Y - b.Y;
        var cross = x1 * y2 - y1 * x2;
        var dot = x1 * x2 + y1 * y2;
        delta = Math.Abs(Math.Atan2(cross, dot));
        turn = cross > 0 ? -1 : 1;   // left turn = -1, matching AlignmentSegment.Turn
    }

    private static double Distance(PlanPoint a, PlanPoint b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
