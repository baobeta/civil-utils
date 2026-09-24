using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Core.Curves;
using C3DTools.Core.Tables;
using AcLine = Autodesk.AutoCAD.DatabaseServices.Line;

namespace C3DTools.Civil2021.Curves;

/// <summary>The TCVN curve box (ytc:bang): MText, frame and leader on YTC_BANG.</summary>
internal static class CurveBoxWriter
{
    private const double CheckTolerance = 0.01;

    /// <param name="measuredOn">Existing alignment in "Chỉ cắm cọc + khung" mode: T1, T2, P are measured on it (YTCA). Null otherwise.</param>
    public static void Write(RouteDrawing d, IReadOnlyList<PlanPoint> pis, RouteDesign design, CurveBoxOptions options,
        double textHeight, Alignment measuredOn, Editor ed)
    {
        foreach (var c in design.Curves)
        {
            if (c.Elements == null) continue;
            PlanPoint before = pis[c.PiIndex - 1], pi = pis[c.PiIndex], after = pis[c.PiIndex + 1];
            var shown = c;
            PlanPoint mid;
            if (measuredOn != null && TryMeasure(measuredOn, c, ed, out var measured, out mid))
            {
                pi = measured.Pi;
                shown = WithMeasured(c, measured);
            }
            else
            {
                var input = c.Input;
                mid = CurveGeometryBuilder.Build(pi, before, after, input.Radius, input.SpiralIn, input.SpiralOut, c.Turn, c.Elements).Mid;
            }

            var lines = CurveBoxText.Build(shown, options);
            var tag = YtcTag.For(c);
            var text = new MText();
            d.Add(text, RouteDrawing.BoxLayer, tag);
            text.TextStyleId = d.Database.Textstyle;
            text.TextHeight = textHeight;
            text.LineSpacingStyle = LineSpacingStyle.Exactly;
            text.LineSpaceDistance = CurveBoxPlacement.LineSpacing(textHeight);
            text.Attachment = AttachmentPoint.MiddleCenter;
            text.Rotation = 0;
            text.Location = new Point3d(pi.X, pi.Y, 0);
            text.Contents = string.Join("\\P", lines);

            // Measure the text first, then place box, frame and leader around it.
            var box = CurveBoxPlacement.Place(pi, before, after, mid,
                CurveBoxPlacement.FrameWidth(text.ActualWidth, textHeight),
                CurveBoxPlacement.FrameHeight(lines.Count, textHeight), textHeight);
            text.Location = new Point3d(box.Centre.X, box.Centre.Y, 0);
            text.Rotation = box.Rotation;

            d.Add(CurveGeometryWriter.Polyline(box.Corners, true), RouteDrawing.BoxLayer, YtcTag.For(c));
            d.Add(new AcLine(new Point3d(box.LeaderStart.X, box.LeaderStart.Y, 0), new Point3d(box.LeaderEnd.X, box.LeaderEnd.Y, 0)),
                RouteDrawing.BoxLayer, YtcTag.For(c));
        }
    }

    private static bool TryMeasure(Alignment alignment, DesignedCurve c, Editor ed, out MeasuredCurve measured, out PlanPoint mid)
    {
        try
        {
            measured = MeasuredElements.Measure(alignment, c);
            mid = MeasuredElements.Point(alignment, c.StationArcMid);
        }
        catch (Exception ex)
        {
            ed.WriteMessage($"\nĐ{c.Number}: không đo được T, P trên Alignment ({ex.Message}); dùng giá trị tính theo R, L.");
            measured = null;
            mid = default;
            return false;
        }

        var e = c.Elements;
        if (Math.Abs(measured.T1 - e.T1) > CheckTolerance || Math.Abs(measured.T2 - e.T2) > CheckTolerance || Math.Abs(measured.P - e.P) > CheckTolerance)
            ed.WriteMessage($"\nĐ{c.Number}: đo trên Alignment T1={N(measured.T1)} T2={N(measured.T2)} P={N(measured.P)}; " +
                            $"theo công thức T1={N(e.T1)} T2={N(e.T2)} P={N(e.P)}. Khung dùng giá trị đo.");
        return true;
    }

    private static DesignedCurve WithMeasured(DesignedCurve c, MeasuredCurve m) => new DesignedCurve
    {
        PiIndex = c.PiIndex,
        Number = c.Number,
        Turn = c.Turn,
        DeltaRadians = c.DeltaRadians,
        Input = c.Input,
        Elements = new CurveElements
        {
            T1 = m.T1,
            T2 = m.T2,
            P = m.P,
            K = c.Elements.K,
            K0 = c.Elements.K0,
            Shift1 = c.Elements.Shift1,
            Shift2 = c.Elements.Shift2,
            TangentOffset1 = c.Elements.TangentOffset1,
            TangentOffset2 = c.Elements.TangentOffset2,
        },
    };

    private static string N(double v) => NumberFormat.Trimmed(v, 2);
}
