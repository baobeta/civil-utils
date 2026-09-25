using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using C3DTools.Civil2021.Drawing;
using C3DTools.Core.Curves;
using C3DTools.Core.Profiles;
using AcLine = Autodesk.AutoCAD.DatabaseServices.Line;
using AcPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace C3DTools.Civil2021.Profiles;

/// <summary>The CTCONGDUNG box above each PVI in the profile view: plain MText, closed frame and a leader down to the PVI.</summary>
internal static class ProfileBoxWriter
{
    public const string Layer = "TD_YTC";
    public const short BoxKind = 1;

    /// <param name="gap">Distance from the PVI up to the bottom of the frame, drawing units.</param>
    public static void Write(TaggedDrawing d, ProfileViewFrame frame, IEnumerable<VerticalCurve> curves, double textHeight, double gap, string tool)
    {
        foreach (var c in curves)
        {
            var lines = VerticalCurveBoxText.Build(c);
            var pvi = frame.ToXY(c.PviStation, c.PviElevation);
            ToolTag Tag() => new ToolTag(tool) { Kind = BoxKind, Number = c.Number, Values = { c.PviStation } };

            var text = new MText();
            d.Add(text, Layer, Tag());
            text.TextStyleId = d.Database.Textstyle;
            text.TextHeight = textHeight;
            text.LineSpacingStyle = LineSpacingStyle.Exactly;
            text.LineSpaceDistance = CurveBoxPlacement.LineSpacing(textHeight);
            text.Attachment = AttachmentPoint.MiddleCenter;
            text.Location = new Point3d(pvi.X, pvi.Y, 0);
            text.Contents = string.Join("\\P", lines);

            // Measure the text first, then put frame and text above the PVI.
            var width = CurveBoxPlacement.FrameWidth(text.ActualWidth, textHeight);
            var height = CurveBoxPlacement.FrameHeight(lines.Count, textHeight);
            var bottom = pvi.Y + gap;
            text.Location = new Point3d(pvi.X, bottom + height / 2, 0);

            var box = new AcPolyline();
            box.AddVertexAt(0, new Point2d(pvi.X - width / 2, bottom), 0, 0, 0);
            box.AddVertexAt(1, new Point2d(pvi.X + width / 2, bottom), 0, 0, 0);
            box.AddVertexAt(2, new Point2d(pvi.X + width / 2, bottom + height), 0, 0, 0);
            box.AddVertexAt(3, new Point2d(pvi.X - width / 2, bottom + height), 0, 0, 0);
            box.Closed = true;
            d.Add(box, Layer, Tag());
            d.Add(new AcLine(new Point3d(pvi.X, pvi.Y, 0), new Point3d(pvi.X, bottom, 0)), Layer, Tag());
        }
    }
}
