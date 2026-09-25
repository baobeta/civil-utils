using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using C3DTools.Civil2021.Drawing;
using C3DTools.Core.Profiles;
using AcLine = Autodesk.AutoCAD.DatabaseServices.Line;

namespace C3DTools.Civil2021.Profiles;

/// <summary>The CTTRACDOC table under a profile view as plain entities: lines on TD_BANG, middle-centre DBText on TD_CHU.</summary>
internal static class ProfileTableWriter
{
    public const string LineLayer = "TD_BANG";
    public const string TextLayer = "TD_CHU";
    public const short TableKind = 1;

    public static void Write(TaggedDrawing d, ProfileTableLayout layout, string tool)
    {
        d.EnsureLayer(LineLayer, 7);
        d.EnsureLayer(TextLayer, 7);
        foreach (var l in layout.Lines)
            d.Add(new AcLine(new Point3d(l.X1, l.Y1, 0), new Point3d(l.X2, l.Y2, 0)), LineLayer, new ToolTag(tool) { Kind = TableKind });

        foreach (var t in layout.Texts)
        {
            if (string.IsNullOrEmpty(t.Text)) continue;
            var p = new Point3d(t.X, t.Y, 0);
            var text = new DBText
            {
                TextString = t.Text,
                Height = t.Height,
                Rotation = t.Rotation,
                Position = p,
                Justify = AttachmentPoint.MiddleCenter,
                AlignmentPoint = p,
            };
            d.Add(text, TextLayer, new ToolTag(tool) { Kind = TableKind });
            text.TextStyleId = d.Database.Textstyle;
            text.AdjustAlignment(d.Database);
        }
    }
}
