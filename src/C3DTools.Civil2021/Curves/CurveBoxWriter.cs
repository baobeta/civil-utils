using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using C3DTools.Core.Curves;
using AcLine = Autodesk.AutoCAD.DatabaseServices.Line;

namespace C3DTools.Civil2021.Curves;

/// <summary>The TCVN curve box (ytc:bang): MText, frame and leader on YTC_BANG.</summary>
internal static class CurveBoxWriter
{
    /// <summary>Box sites for a designed route: PI polygon and Core geometry (polyline mode, "Thiết kế lại cong").</summary>
    public static List<BoxSite> SitesFromGeometry(IReadOnlyList<PlanPoint> pis, RouteDesign design)
    {
        var sites = new List<BoxSite>();
        foreach (var c in design.Curves)
        {
            if (c.Elements == null) continue;
            PlanPoint before = pis[c.PiIndex - 1], pi = pis[c.PiIndex], after = pis[c.PiIndex + 1];
            var input = c.Input;
            var mid = CurveGeometryBuilder.Build(pi, before, after, input.Radius, input.SpiralIn, input.SpiralOut, c.Turn, c.Elements).Mid;
            sites.Add(new BoxSite { Curve = c, Pi = pi, Before = before, After = after, Mid = mid });
        }

        return sites;
    }

    public static void Write(RouteDrawing d, IEnumerable<BoxSite> sites, CurveBoxOptions options, double textHeight)
    {
        foreach (var site in sites)
        {
            var c = site.Curve;
            var lines = CurveBoxText.Build(c, options);
            var text = new MText();
            d.Add(text, RouteDrawing.BoxLayer, YtcTag.For(c, YtcKind.Box));
            text.TextStyleId = d.Database.Textstyle;
            text.TextHeight = textHeight;
            text.LineSpacingStyle = LineSpacingStyle.Exactly;
            text.LineSpaceDistance = CurveBoxPlacement.LineSpacing(textHeight);
            text.Attachment = AttachmentPoint.MiddleCenter;
            text.Rotation = 0;
            text.Location = new Point3d(site.Pi.X, site.Pi.Y, 0);
            text.Contents = string.Join("\\P", lines);

            // Measure the text first, then place box, frame and leader around it.
            var box = CurveBoxPlacement.Place(site.Pi, site.Before, site.After, site.Mid,
                CurveBoxPlacement.FrameWidth(text.ActualWidth, textHeight),
                CurveBoxPlacement.FrameHeight(lines.Count, textHeight), textHeight);
            text.Location = new Point3d(box.Centre.X, box.Centre.Y, 0);
            text.Rotation = box.Rotation;

            d.Add(CurveGeometryWriter.Polyline(box.Corners, true), RouteDrawing.BoxLayer, YtcTag.For(c, YtcKind.Box));
            d.Add(new AcLine(new Point3d(box.LeaderStart.X, box.LeaderStart.Y, 0), new Point3d(box.LeaderEnd.X, box.LeaderEnd.Y, 0)),
                RouteDrawing.BoxLayer, YtcTag.For(c, YtcKind.Box));
        }
    }
}
