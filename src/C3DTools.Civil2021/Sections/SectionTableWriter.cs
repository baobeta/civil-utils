using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using C3DTools.Civil2021.Drawing;
using C3DTools.Core.Sections;
using AcLine = Autodesk.AutoCAD.DatabaseServices.Line;
using SectionViewStyle = Autodesk.Civil.DatabaseServices.Styles.SectionViewStyle;

namespace C3DTools.Civil2021.Sections;

/// <summary>The CTTRACNGANG table under a section view as plain entities: lines on TN_BANG, middle-centre DBText on TN_CHU.</summary>
internal static class SectionTableWriter
{
    public const string Tool = "TRACNGANG";
    public const string LineLayer = "TN_BANG";
    public const string TextLayer = "TN_CHU";
    public const short TableKind = 1;

    public static void Write(TaggedDrawing d, SectionTableLayout layout)
    {
        d.EnsureLayer(LineLayer, 7);
        d.EnsureLayer(TextLayer, 7);
        foreach (var l in layout.Lines)
            d.Add(new AcLine(new Point3d(l.X1, l.Y1, 0), new Point3d(l.X2, l.Y2, 0)), LineLayer, new ToolTag(Tool) { Kind = TableKind });

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
            d.Add(text, TextLayer, new ToolTag(Tool) { Kind = TableKind });
            text.TextStyleId = d.Database.Textstyle;
            text.AdjustAlignment(d.Database);
        }
    }
}

/// <summary>
/// Offset/elevation → drawing XY in a section view. First SectionView.FindXYAtOffsetAndElevation; when that fails or
/// returns false, Location taken as the grid's lower-left corner (OffsetLeft, ElevationMin): Location + (offset − OffsetLeft)·1
/// and (elevation − ElevationMin)·VerticalExaggeration of the view style's GraphStyle (1 when unreadable). What Location
/// marks on a 2021 section view is not verified, hence UsedFallback and the command's message.
/// </summary>
internal sealed class SectionViewFrame
{
    private readonly SectionView _view;
    private bool _findFailed;

    public SectionViewFrame(Transaction tr, SectionView view)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        Origin = view.Location;
        OffsetLeft = view.OffsetLeft;
        OffsetRight = view.OffsetRight;
        ElevationMin = view.ElevationMin;
        VerticalExaggeration = 1;
        try
        {
            if (tr.GetObject(view.StyleId, OpenMode.ForRead) is SectionViewStyle style && style.GraphStyle.VerticalExaggeration > 0)
                VerticalExaggeration = style.GraphStyle.VerticalExaggeration;
        }
        catch (Exception)
        {
            // Only the fallback needs it.
        }
    }

    public Point3d Origin { get; }
    public double OffsetLeft { get; }
    public double OffsetRight { get; }
    public double ElevationMin { get; }
    public double VerticalExaggeration { get; }

    public bool UsedFallback => _findFailed;

    public Point2d ToXY(double offset, double elevation)
    {
        try
        {
            double x = 0, y = 0;
            if (_view.FindXYAtOffsetAndElevation(offset, elevation, ref x, ref y)) return new Point2d(x, y);
        }
        catch (Exception)
        {
            // e.g. offset outside the view: computed position below.
        }

        _findFailed = true;
        return new Point2d(Origin.X + (offset - OffsetLeft), Origin.Y + (elevation - ElevationMin) * VerticalExaggeration);
    }
}
