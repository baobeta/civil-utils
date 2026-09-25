using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Drawing;
using C3DTools.Core.Presets;
using C3DTools.Core.Sections;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace C3DTools.Civil2021.Sections;

/// <summary>A section view to arrange: its station, current extents (view + its CTTRACNGANG table) and the table's entities.</summary>
internal sealed class SheetItem
{
    public ObjectId ViewId { get; set; }
    public string Handle { get; set; }
    public string Name { get; set; }
    public double Station { get; set; }
    public Point2d Min { get; set; }
    public Point2d Max { get; set; }
    public List<ObjectId> TableIds { get; } = new List<ObjectId>();
    public double Width => Max.X - Min.X;
    public double Height => Max.Y - Min.Y;
}

/// <summary>
/// CTXEPTRANG output: moves each SectionView (Graph.Location setter) and its CTTRACNGANG table by the same displacement
/// onto its packed cell, then draws each sheet's frame on TN_KHUNG: Resources\A3.dwg inserted as a block (drawn in mm,
/// base point at the paper's lower-left, scaled by drawing units per mm) when that file exists, else the paper outline
/// and the margin frame as polylines; plus the title "TRẮC NGANG – Tờ n/N – Km a … Km b". Frames are tagged XEPTRANG kind 1
/// (Number = sheet index, Values = sheet lower-left) so a rerun replaces them and starts from the same place.
/// </summary>
internal static class SheetWriter
{
    public const string Tool = "XEPTRANG";
    public const string FrameLayer = "TN_KHUNG";
    public const short FrameKind = 1;
    private const string FrameBlock = "C3DT_KHUNG_A3";

    /// <summary>The view's extents together with its table; null (with a message) when the view has no extents.</summary>
    public static SheetItem Measure(Transaction tr, Database db, SectionView view, double station, Action<string> say)
    {
        var item = new SheetItem { ViewId = view.ObjectId, Handle = view.Handle.ToString(), Name = view.Name, Station = station };
        Extents3d extents;
        try
        {
            extents = view.GeometricExtents;
        }
        catch (Exception ex)
        {
            say?.Invoke($"Không lấy được khung bao của trắc ngang {view.Name} ({ex.Message}); bỏ qua.");
            return null;
        }

        foreach (var (id, tag) in TaggedDrawing.FindTagged(tr, db, SectionTableWriter.Tool, item.Handle))
        {
            if (tag.Kind != SectionTableWriter.TableKind) continue;
            item.TableIds.Add(id);
            try
            {
                extents.AddExtents(((Entity)tr.GetObject(id, OpenMode.ForRead)).GeometricExtents);
            }
            catch (Exception)
            {
                // An entity without extents (empty text) does not change the size.
            }
        }

        item.Min = new Point2d(extents.MinPoint.X, extents.MinPoint.Y);
        item.Max = new Point2d(extents.MaxPoint.X, extents.MaxPoint.Y);
        return item;
    }

    /// <summary>Lower-left of sheet 1 from the previous run for this tag, or null.</summary>
    public static Point2d? PreviousOrigin(Transaction tr, Database db, string tagHandle)
    {
        foreach (var (_, tag) in TaggedDrawing.FindTagged(tr, db, Tool, tagHandle))
        {
            if (tag.Kind == FrameKind && tag.Number == 0 && tag.Values.Count >= 2) return new Point2d(tag.Values[0], tag.Values[1]);
        }

        return null;
    }

    /// <summary>Moves the items to their placements. Throws (the caller aborts) when a view refuses the new Location.</summary>
    public static void Move(Transaction tr, IReadOnlyList<SheetItem> items, SheetPlan plan)
    {
        foreach (var p in plan.Placements)
        {
            var item = items[p.Item];
            var shift = new Vector3d(p.X - item.Min.X, p.Y - item.Min.Y, 0);
            if (shift.Length < 1e-9) continue;
            var view = (SectionView)tr.GetObject(item.ViewId, OpenMode.ForWrite);
            view.Location = view.Location + shift;
            var move = Matrix3d.Displacement(shift);
            foreach (var id in item.TableIds) ((Entity)tr.GetObject(id, OpenMode.ForWrite, false, true)).TransformBy(move);
        }
    }

    /// <summary>Frames and titles; titles[i] is sheet i's title. Returns true when the A3.dwg block was used.</summary>
    public static bool WriteFrames(TaggedDrawing d, SheetPlan plan, SheetLayoutOptions layout, double unitsPerMm, IReadOnlyList<string> titles, Action<string> say)
    {
        d.EnsureLayer(FrameLayer, 4);
        var block = FrameBlockId(d, say);
        foreach (var sheet in plan.Sheets)
        {
            ToolTag Tag()
            {
                var tag = new ToolTag(Tool) { Kind = FrameKind, Number = sheet.Index };
                tag.Values.Add(sheet.X);
                tag.Values.Add(sheet.Y);
                return tag;
            }

            if (!block.IsNull)
            {
                d.Add(new BlockReference(new Point3d(sheet.X, sheet.Y, 0), block) { ScaleFactors = new Scale3d(unitsPerMm) }, FrameLayer, Tag());
            }
            else
            {
                d.Add(Rectangle(sheet.X, sheet.Y, sheet.X + sheet.Width, sheet.Y + sheet.Height), FrameLayer, Tag());
                d.Add(Rectangle(sheet.InnerLeft, sheet.InnerBottom, sheet.InnerRight, sheet.InnerTop), FrameLayer, Tag());
            }

            // In the bottom margin when it has room, else just inside the frame.
            var h = 3.5 * unitsPerMm;
            var y = layout.MarginBottom >= 6 ? sheet.Y + layout.MarginBottom * unitsPerMm / 2 : sheet.InnerBottom + h;
            var p = new Point3d((sheet.InnerLeft + sheet.InnerRight) / 2, y, 0);
            var text = new DBText { TextString = titles[sheet.Index], Height = h, Position = p, Justify = AttachmentPoint.MiddleCenter, AlignmentPoint = p };
            d.Add(text, FrameLayer, Tag());
            text.TextStyleId = d.Database.Textstyle;
            text.AdjustAlignment(d.Database);
        }

        return !block.IsNull;
    }

    private static Polyline Rectangle(double x1, double y1, double x2, double y2)
    {
        var pl = new Polyline();
        pl.AddVertexAt(0, new Point2d(x1, y1), 0, 0, 0);
        pl.AddVertexAt(1, new Point2d(x2, y1), 0, 0, 0);
        pl.AddVertexAt(2, new Point2d(x2, y2), 0, 0, 0);
        pl.AddVertexAt(3, new Point2d(x1, y2), 0, 0, 0);
        pl.Closed = true;
        return pl;
    }

    /// <summary>The frame block: already in the drawing, or read from Resources\A3.dwg next to the add-in; Null when neither.</summary>
    private static ObjectId FrameBlockId(TaggedDrawing d, Action<string> say)
    {
        var table = (BlockTable)d.Transaction.GetObject(d.Database.BlockTableId, OpenMode.ForRead);
        if (table.Has(FrameBlock)) return table[FrameBlock];

        string path;
        try
        {
            path = Path.Combine(Path.GetDirectoryName(typeof(SheetWriter).Assembly.Location) ?? "", "Resources", "A3.dwg");
        }
        catch (Exception)
        {
            return ObjectId.Null;
        }

        if (!File.Exists(path)) return ObjectId.Null;
        try
        {
            using (var source = new Database(false, true))
            {
                source.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, true, null);
                return d.Database.Insert(FrameBlock, source, true);
            }
        }
        catch (Exception ex)
        {
            say?.Invoke($"Không chèn được khung {path} ({ex.Message}); vẽ khung bằng polyline.");
            return ObjectId.Null;
        }
    }

    /// <summary>Tag handle for the frames: the sample line group shared by the views, else the first view.</summary>
    public static string TagHandle(IEnumerable<string> groupHandles, string firstViewHandle)
    {
        var groups = groupHandles.Where(h => !string.IsNullOrEmpty(h)).Distinct().ToList();
        return groups.Count == 1 ? groups[0] : firstViewHandle;
    }
}
