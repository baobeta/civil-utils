using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Core.Curves;
using C3DTools.Core.Tables;
using AcLine = Autodesk.AutoCAD.DatabaseServices.Line;
using AcPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace C3DTools.Civil2021.Curves;

/// <summary>The object CTYTC works on: a polyline (new design) or an existing alignment.</summary>
internal abstract class RouteSource
{
    private static readonly List<AcLine> Marker = new List<AcLine>();

    protected RouteSource(Document document, ObjectId id, string handle)
    {
        Document = document;
        Id = id;
        Handle = handle;
        TagHandle = handle;
    }

    public Document Document { get; }
    public ObjectId Id { get; }
    public string Handle { get; }

    /// <summary>Handle stored in the XData of everything a run creates, so the next run finds and replaces it.</summary>
    public string TagHandle { get; protected set; }

    public abstract bool IsAlignment { get; }
    public abstract string Description { get; }

    /// <summary>Loads the PIs and any existing values into the session. Throws InvalidOperationException with a Vietnamese message.</summary>
    public abstract void LoadInto(CurveDesignSession session);

    /// <summary>The polyline or alignment already selected when CTYTC started, if any.</summary>
    public static RouteSource PickFirst(Editor ed)
    {
        var implied = ed.SelectImplied();
        if (implied.Status != PromptStatus.OK || implied.Value == null) return null;
        var ids = implied.Value.GetObjectIds();
        ed.SetImpliedSelection(new ObjectId[0]);
        return ids.Select(id => FromId(ed.Document, id)).FirstOrDefault(s => s != null);
    }

    public static RouteSource Prompt(Editor ed)
    {
        var options = new PromptEntityOptions("\nChọn polyline hoặc alignment: ");
        options.SetRejectMessage("\nChỉ chọn polyline (LWPOLYLINE) hoặc alignment.");
        options.AddAllowedClass(typeof(AcPolyline), true);
        options.AddAllowedClass(typeof(Alignment), false);
        var result = ed.GetEntity(options);
        return result.Status == PromptStatus.OK ? FromId(ed.Document, result.ObjectId) : null;
    }

    private static RouteSource FromId(Document doc, ObjectId id)
    {
        using (var tr = doc.TransactionManager.StartTransaction())
        {
            var obj = tr.GetObject(id, OpenMode.ForRead);
            RouteSource source = null;
            if (obj is AcPolyline) source = new PolylineSource(doc, id, obj.Handle.ToString());
            else if (obj is Alignment alignment) source = new AlignmentSource(doc, id, obj.Handle.ToString(), alignment.Name);
            tr.Commit();
            return source;
        }
    }

    /// <summary>Centres the view on the PI and draws a temporary X there (the LISP's grvecs marker).</summary>
    public static void ZoomAndMark(Editor ed, PlanPoint pi, double viewHeight)
    {
        ClearMarker();
        using (var view = ed.GetCurrentView())
        {
            // WCS → DCS of the current view (view direction, target and twist).
            var wcsToDcs = (Matrix3d.Rotation(-view.ViewTwist, view.ViewDirection, view.Target)
                            * Matrix3d.Displacement(view.Target - Point3d.Origin)
                            * Matrix3d.PlaneToWorld(view.ViewDirection)).Inverse();
            var centre = new Point3d(pi.X, pi.Y, 0).TransformBy(wcsToDcs);
            var ratio = view.Height > 0 ? view.Width / view.Height : 1;
            view.CenterPoint = new Point2d(centre.X, centre.Y);
            view.Height = viewHeight;
            view.Width = viewHeight * ratio;
            ed.SetCurrentView(view);
        }

        var s = viewHeight / 40;
        Marker.Add(new AcLine(new Point3d(pi.X - s, pi.Y - s, 0), new Point3d(pi.X + s, pi.Y + s, 0)));
        Marker.Add(new AcLine(new Point3d(pi.X - s, pi.Y + s, 0), new Point3d(pi.X + s, pi.Y - s, 0)));
        foreach (var line in Marker)
        {
            line.ColorIndex = 1;
            TransientManager.CurrentTransientManager.AddTransient(line, TransientDrawingMode.DirectShortTerm, 128, new IntegerCollection());
        }

        ed.UpdateScreen();
    }

    public static void ClearMarker()
    {
        if (Marker.Count == 0) return;
        foreach (var line in Marker)
        {
            TransientManager.CurrentTransientManager.EraseTransient(line, new IntegerCollection());
            line.Dispose();
        }

        Marker.Clear();
    }

    /// <summary>Puts values from the previous run's boxes back into the grid, matched by curve number.</summary>
    protected static void ApplyTags(CurveDesignSession session, Dictionary<int, YtcTag> tags, bool withGeometry)
    {
        if (tags.Count == 0) return;
        var inputs = session.Inputs.Select(i => i.Clone()).ToList();
        foreach (var row in session.Rows)
        {
            if (!tags.TryGetValue(row.Index + 1, out var tag)) continue;
            var input = inputs[row.PiIndex - 1];
            input.Wb = tag.Wb;
            input.Wl = tag.Wl;
            if (withGeometry && tag.Radius > 0)
            {
                input.Radius = tag.Radius;
                input.SpiralIn = tag.SpiralIn;
                input.SpiralOut = tag.SpiralOut;
            }
        }

        session.Load(session.Pis, inputs);
    }

    protected void Warn(string message) => Document.Editor.WriteMessage("\n" + message);
}

internal sealed class PolylineSource : RouteSource
{
    private int _vertexCount;

    public PolylineSource(Document document, ObjectId id, string handle) : base(document, id, handle) { }

    public override bool IsAlignment => false;
    public override string Description => $"Polyline ({_vertexCount} đỉnh)";

    public override void LoadInto(CurveDesignSession session)
    {
        using (var tr = Document.TransactionManager.StartTransaction())
        {
            var pline = (AcPolyline)tr.GetObject(Id, OpenMode.ForRead);
            var points = new List<PlanPoint>();
            var hasBulge = false;
            for (var i = 0; i < pline.NumberOfVertices; i++)
            {
                var p = pline.GetPoint3dAt(i);
                points.Add(new PlanPoint(p.X, p.Y));
                if (Math.Abs(pline.GetBulgeAt(i)) > 1e-9 && i < pline.NumberOfVertices - 1) hasBulge = true;
            }

            if (hasBulge) Warn("Polyline có đoạn cong (bulge): chỉ dùng các đỉnh, bỏ qua cung tròn.");
            var pis = RouteDesigner.RemoveDuplicatePoints(points);
            if (pis.Count < 2) throw new InvalidOperationException("Polyline cần ít nhất 2 đỉnh khác nhau.");
            _vertexCount = pis.Count;

            session.ReadOnlyGeometry = false;
            session.CreateAlignment = false;
            session.DrawCurves = true;
            session.Load(pis, null);
            ApplyTags(session, YtcTag.ReadBoxes(tr, Document.Database, TagHandle), withGeometry: true);
            tr.Commit();
        }
    }
}

internal sealed class AlignmentSource : RouteSource
{
    private const double StationTolerance = 0.01;
    private readonly string _name;
    private int _piCount;

    public AlignmentSource(Document document, ObjectId id, string handle, string name) : base(document, id, handle)
    {
        _name = name;
    }

    public override bool IsAlignment => true;
    public override string Description => $"Alignment {_name} ({_piCount} đỉnh)";

    public override void LoadInto(CurveDesignSession session)
    {
        using (var tr = Document.TransactionManager.StartTransaction())
        {
            var alignment = (Alignment)tr.GetObject(Id, OpenMode.ForRead);
            // An alignment created by CTYTC from a polyline keeps that polyline's tag: its boxes are found through it.
            var ownTag = YtcTag.Read(alignment);
            TagHandle = ownTag?.SourceHandle ?? Handle;

            var warnings = new List<string>();
            var segments = AlignmentGeometryReader.Read(alignment, warnings);
            var grouping = CurveGrouper.Group(segments);
            warnings.AddRange(grouping.Warnings);
            var tangents = AlignmentGeometryReader.ReadTangents(alignment);
            if (tangents.Count == 0) throw new InvalidOperationException("Alignment không có đoạn thẳng nào, không dựng được đỉnh.");
            if (segments.Count > 0 && (segments[0].Kind != SegmentKind.Line || segments[segments.Count - 1].Kind != SegmentKind.Line))
                warnings.Add("Alignment bắt đầu hoặc kết thúc bằng đường cong: đường cong đó không có đỉnh và bị bỏ qua.");

            List<PlanPoint> pis;
            try
            {
                pis = PiExtractor.FromTangents(tangents.Select(t => (t.Start, t.End)).ToList());
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException(ex.Message, ex);
            }

            var inputs = new CurveInput[pis.Count - 2];
            for (var j = 1; j < pis.Count - 1; j++)
            {
                double from = tangents[j - 1].EndStation, to = tangents[j].StartStation;
                var group = grouping.Groups.FirstOrDefault(g =>
                    g.StartStation >= from - StationTolerance && g.EndStation <= to + StationTolerance);
                if (group != null)
                    inputs[j - 1] = new CurveInput { Radius = group.Radius, SpiralIn = group.SpiralIn, SpiralOut = group.SpiralOut };
                else if (to - from > StationTolerance)
                    warnings.Add($"Đỉnh {j}: không đọc được đường cong giữa lý trình {NumberFormat.Fixed(from, 2)} và {NumberFormat.Fixed(to, 2)}; dùng giá trị mặc định.");
            }

            foreach (var w in warnings) Warn(w);
            _piCount = pis.Count;

            session.ReadOnlyGeometry = true;   // "Chỉ cắm cọc + khung" by default, like YTCA
            session.CreateAlignment = false;
            session.DrawCurves = false;
            session.StartStation = alignment.StartingStation;
            session.Load(pis, inputs);
            ApplyTags(session, YtcTag.ReadBoxes(tr, Document.Database, TagHandle), withGeometry: false);
            tr.Commit();
        }
    }

    /// <summary>Đọc Wb/Wl từ Offset Alignment…: prompts for the edge alignments and fills Wb/Wl in the grid.</summary>
    public void ReadWidening(CurveDesignSession session)
    {
        var ed = Document.Editor;
        var options = new PromptSelectionOptions
        {
            MessageForAdding = "\nChọn các Offset Alignment (mép trái/phải) có Widening, Enter nếu không có: ",
        };
        var result = ed.GetSelection(options);
        if (result.Status != PromptStatus.OK) return;

        var alignmentClass = RXObject.GetClass(typeof(Alignment));
        using (var tr = Document.TransactionManager.StartTransaction())
        {
            var centreline = (Alignment)tr.GetObject(Id, OpenMode.ForRead);
            var edges = result.Value.GetObjectIds()
                .Where(id => id != Id && id.ObjectClass.IsDerivedFrom(alignmentClass))
                .Select(id => (Alignment)tr.GetObject(id, OpenMode.ForRead))
                .ToList();
            if (edges.Count == 0)
            {
                Warn("Không có Offset Alignment nào trong các đối tượng đã chọn.");
                tr.Commit();
                return;
            }

            var messages = new List<string>();
            var widening = OffsetWideningReader.Read(centreline, edges, session.Design, messages);
            foreach (var m in messages) Warn(m);
            foreach (var row in session.Rows)
            {
                if (!widening.TryGetValue(row.Index + 1, out var w)) continue;
                row.WbText = NumberFormat.Trimmed(w.Wb, 3);
                row.WlText = NumberFormat.Trimmed(w.Wl, 3);
            }

            tr.Commit();
        }
    }
}
