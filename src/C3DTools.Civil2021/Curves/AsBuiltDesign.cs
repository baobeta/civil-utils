using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Core.Curves;
using C3DTools.Core.Tables;

namespace C3DTools.Civil2021.Curves;

/// <summary>Where a curve box goes: the curve (with the numbers to print), its PI, the tangent directions and the P point.</summary>
internal sealed class BoxSite
{
    public DesignedCurve Curve { get; set; }
    public PlanPoint Pi { get; set; }
    public PlanPoint Before { get; set; }
    public PlanPoint After { get; set; }
    public PlanPoint Mid { get; set; }
}

/// <summary>
/// "Chỉ cắm cọc + khung" (YTCA): the alignment's own curves, stations and measured T1/T2/P. Core's computed values
/// are only used to warn when they differ.
/// </summary>
internal static class AsBuiltDesign
{
    private const double CheckTolerance = 0.01;

    public static RouteDesign Read(Alignment alignment, AlignmentSource source, RouteDesign design, List<BoxSite> sites, Editor ed)
    {
        var asBuilt = new RouteDesign { EndStation = alignment.EndingStation };
        foreach (var c in design.Curves)
        {
            if (c.Elements == null) continue;
            var g = source.GroupFor(c);
            if (g == null)
            {
                ed.WriteMessage($"\nĐ{c.Number}: không có đường cong tương ứng trên Alignment; bỏ qua.");
                continue;
            }

            MeasuredCurve m;
            PlanPoint dirIn, dirOut, mid;
            try
            {
                m = MeasuredElements.Measure(alignment, g.StartStation, g.EndStation, g.ArcMidStation);
                dirIn = MeasuredElements.Direction(alignment, g.StartStation);
                dirOut = MeasuredElements.Direction(alignment, g.EndStation);
                mid = MeasuredElements.Point(alignment, g.ArcMidStation);
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\nĐ{c.Number}: không đo được trên Alignment ({ex.Message}); bỏ qua.");
                continue;
            }

            var curve = new DesignedCurve
            {
                PiIndex = c.PiIndex,
                Number = c.Number,
                Turn = g.Turn,
                DeltaRadians = g.DeltaRadians,
                Input = new CurveInput { Radius = g.Radius, SpiralIn = g.SpiralIn, SpiralOut = g.SpiralOut, Wb = c.Input.Wb, Wl = c.Input.Wl },
                Elements = new CurveElements
                {
                    T1 = m.T1,
                    T2 = m.T2,
                    P = m.P,
                    K = g.EndStation - g.StartStation,
                    K0 = g.ArcEndStation - g.ArcStartStation,
                },
                StationStart = g.StartStation,
                StationArcStart = g.ArcStartStation,
                StationArcEnd = g.ArcEndStation,
                StationEnd = g.EndStation,
            };
            asBuilt.Curves.Add(curve);
            Check(c, curve, ed);
            sites.Add(new BoxSite
            {
                Curve = curve,
                Pi = m.Pi,
                Before = new PlanPoint(m.Pi.X - dirIn.X, m.Pi.Y - dirIn.Y),
                After = new PlanPoint(m.Pi.X + dirOut.X, m.Pi.Y + dirOut.Y),
                Mid = mid,
            });
        }

        return asBuilt;
    }

    private static void Check(DesignedCurve computed, DesignedCurve measured, Editor ed)
    {
        var diffs = new List<string>();
        void Compare(string name, double a, double b)
        {
            if (Math.Abs(a - b) > CheckTolerance) diffs.Add($"{name} đo {NumberFormat.Trimmed(a, 2)} / tính {NumberFormat.Trimmed(b, 2)}");
        }

        Compare("T1", measured.Elements.T1, computed.Elements.T1);
        Compare("T2", measured.Elements.T2, computed.Elements.T2);
        Compare("P", measured.Elements.P, computed.Elements.P);
        Compare("K", measured.Elements.K, computed.Elements.K);
        Compare("lý trình TĐ", measured.StationArcStart, computed.StationArcStart);
        Compare("lý trình TC", measured.StationArcEnd, computed.StationArcEnd);
        if (diffs.Count > 0)
            ed.WriteMessage($"\nĐ{measured.Number}: {string.Join(", ", diffs)}. Khung và cọc dùng giá trị đo trên Alignment.");
    }
}
