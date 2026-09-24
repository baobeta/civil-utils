using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using C3DTools.Core.Curves;
using AcLine = Autodesk.AutoCAD.DatabaseServices.Line;

namespace C3DTools.Civil2021.Curves;

/// <summary>Stakes (ytc:coc): tick line, station text and stake name on YTC_COC.</summary>
internal static class StakeWriter
{
    public static void Write(RouteDrawing d, IEnumerable<Stake> stakes, double textHeight)
    {
        foreach (var stake in stakes)
        {
            var layout = RouteStakes.Layout(stake, textHeight);
            var tag = new YtcTag { Number = stake.CurveNumber };
            d.Add(new AcLine(P3(layout.TickStart), P3(layout.TickEnd)), RouteDrawing.StakeLayer, tag);
            AddText(d, layout.StationText, layout.StationTextPoint, layout.Rotation, textHeight, tag);
            if (layout.NameText.Length > 0) AddText(d, layout.NameText, layout.NameTextPoint, layout.Rotation, textHeight, tag);
        }
    }

    /// <summary>TEXT justified middle-centre (72 = 1, 73 = 2), as ytc:text makes it.</summary>
    private static void AddText(RouteDrawing d, string value, PlanPoint at, double rotation, double height, YtcTag tag)
    {
        var p = P3(at);
        var text = new DBText
        {
            TextString = value,
            Height = height,
            Rotation = rotation,
            Position = p,
            Justify = AttachmentPoint.MiddleCenter,
            AlignmentPoint = p,
        };
        d.Add(text, RouteDrawing.StakeLayer, tag);
        text.TextStyleId = d.Database.Textstyle;
        text.AdjustAlignment(d.Database);
    }

    private static Point3d P3(PlanPoint p) => new Point3d(p.X, p.Y, 0);
}
