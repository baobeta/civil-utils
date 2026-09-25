using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using C3DTools.Civil2021.Curves;
using C3DTools.Civil2021.Drawing;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Curves;
using C3DTools.Core.Surfaces;
using C3DTools.Core.Tables;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using CivilEntity = Autodesk.Civil.DatabaseServices.Entity;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.SurfaceCleanupCommand))]

namespace C3DTools.Civil2021.Commands;

public class SurfaceCleanupCommand
{
    private const string Command = "CTMATDIA";
    private const string Tool = "MATDIA";
    private const string LabelLayer = "DH_CAODO";
    private const short LabelKind = 2;

    /// <summary>CTMATDIA: drops long/outside TIN triangles, or labels contour elevations where picked lines cross them.</summary>
    [CommandMethod("C3DTOOLS", "CTMATDIA", CommandFlags.Modal)]
    public void Surface()
    {
        try
        {
            Run();
        }
        catch (System.Exception ex)
        {
            // Last resort: never let an exception reach AutoCAD's unhandled-exception dialog.
            Prompts.Say(AcCoreApp.DocumentManager.MdiActiveDocument?.Editor, $"Lỗi C3DTools: {ex.Message}");
        }
    }

    /// <summary>A picked line of tab 2: handle for the tag, plan points for the planner.</summary>
    private sealed class CutLine
    {
        public string Handle { get; set; }
        public List<PlanPoint> Points { get; set; }
    }

    private static void Run()
    {
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        var messages = new List<string>();
        var preset = PresetLocator.LoadForDrawing(messages);
        foreach (var m in messages) Prompts.Say(ed, m);

        var memory = ToolWindow.Options;
        var session = new SurfaceSession(preset);
        var surfaces = ReadSurfaces(doc);
        session.SetSurfaces(surfaces.Select(s => s.name), memory.Get(Command, "Surface", ""));
        session.TabIndex = memory.Get(Command, "Tab", 0);
        session.LabelMinor = memory.Get(Command, "Minor", true);

        var boundaryId = ObjectId.Null;
        List<PlanPoint> boundary = null;
        var lines = new List<CutLine>();
        string styleReadFor = null;

        while (true)
        {
            if (session.Surface != null && session.Surface != styleReadFor)
            {
                styleReadFor = session.Surface;
                ReadStyleIntervals(doc, SurfaceId(surfaces, session), session);
            }

            var window = new SurfaceWindow(session);
            var action = window.ShowModal();
            memory.Set(Command, "Surface", session.Surface ?? "");
            memory.Set(Command, "Tab", session.TabIndex);
            memory.Set(Command, "Minor", session.LabelMinor);
            ToolWindow.SaveOptions();
            if (!session.HasBoundary)
            {
                boundaryId = ObjectId.Null;
                boundary = null;
            }

            var surfaceId = SurfaceId(surfaces, session);
            switch (action)
            {
                case DialogAction.Pick:
                    if (session.TabIndex == SurfaceSession.CleanupTab)
                    {
                        var picked = Prompts.PickEntity<Polyline>(ed, "Chọn polyline ranh giới (kín): ");
                        if (picked.IsNull) continue;
                        var points = ReadBoundary(doc, picked);
                        if (points == null) continue;
                        boundaryId = picked;
                        boundary = points;
                        session.SetBoundary($"polyline {picked.Handle} ({points.Count} đỉnh)");
                    }
                    else
                    {
                        var picked = PickLines(doc);
                        if (picked == null) continue;
                        lines = picked;
                        session.SetLines($"{lines.Count} đường", lines.Count);
                    }

                    continue;
                case DialogAction.Count:
                    if (session.TabIndex == SurfaceSession.CleanupTab && session.CanApply) Count(doc, surfaceId, session, boundary);
                    continue;
                case DialogAction.Preview:
                case DialogAction.Apply:
                    if (!session.CanApply) continue;
                    var ask = action == DialogAction.Preview;
                    var done = session.TabIndex == SurfaceSession.CleanupTab
                        ? Cleanup(doc, surfaceId, session, boundaryId, boundary, ask)
                        : Label(doc, surfaceId, session, lines, ask);
                    if (done) return;
                    continue;   // Khong or an error: back to the dialog
                default:
                    return;
            }
        }
    }

    private static ObjectId SurfaceId(List<(ObjectId id, string name)> surfaces, SurfaceSession session) =>
        surfaces.FirstOrDefault(s => s.name == session.Surface).id;

    /// <summary>TIN surfaces of the drawing (triangles, DeleteLines and ExtractContours are TinSurface members).</summary>
    private static List<(ObjectId id, string name)> ReadSurfaces(Document doc)
    {
        var result = new List<(ObjectId id, string name)>();
        try
        {
            var civil = CivilDocument.GetCivilDocument(doc.Database);
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in civil.GetSurfaceIds())
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is TinSurface surface) result.Add((id, surface.Name));
                }

                tr.Commit();
            }
        }
        catch (System.Exception ex)
        {
            Prompts.Say(doc.Editor, $"Không đọc được danh sách mặt phủ: {ex.Message}");
        }

        return result;
    }

    /// <summary>Major/minor contour interval of the surface style; the preset stays when it can't be read.</summary>
    private static void ReadStyleIntervals(Document doc, ObjectId surfaceId, SurfaceSession session)
    {
        if (surfaceId.IsNull) return;
        try
        {
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var surface = (CivilEntity)tr.GetObject(surfaceId, OpenMode.ForRead);
                if (tr.GetObject(surface.StyleId, OpenMode.ForRead) is SurfaceStyle style)
                    session.SetIntervalsFromStyle(style.ContourStyle.MajorContourInterval, style.ContourStyle.MinorContourInterval);
                tr.Commit();
            }
        }
        catch (System.Exception ex)
        {
            Prompts.Say(doc.Editor, $"Không đọc được khoảng cao đều từ kiểu mặt phủ ({ex.Message}); dùng giá trị preset.");
        }
    }

    /// <summary>Plan vertices of the boundary polyline; null (with a message) when it can't be used.</summary>
    private static List<PlanPoint> ReadBoundary(Document doc, ObjectId id)
    {
        try
        {
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var pl = (Polyline)tr.GetObject(id, OpenMode.ForRead);
                var points = PlanPoints(pl);
                tr.Commit();
                if (points.Count < 3)
                {
                    Prompts.Say(doc.Editor, "Polyline ranh giới cần ít nhất 3 đỉnh.");
                    return null;
                }

                if (!pl.Closed) Prompts.Say(doc.Editor, "Polyline ranh giới chưa đóng; coi như đóng từ đỉnh cuối về đỉnh đầu.");
                return points;
            }
        }
        catch (System.Exception ex)
        {
            Prompts.Say(doc.Editor, $"Không đọc được polyline ranh giới: {ex.Message}");
            return null;
        }
    }

    /// <summary>Lines, polylines and arcs crossing the contours; null when cancelled or nothing usable.</summary>
    private static List<CutLine> PickLines(Document doc)
    {
        var ed = doc.Editor;
        var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LINE,LWPOLYLINE,POLYLINE,ARC") });
        var result = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nChọn line/polyline cắt qua đồng mức: " }, filter);
        if (result.Status != PromptStatus.OK || result.Value == null) return null;
        var lines = new List<CutLine>();
        try
        {
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                foreach (var id in result.Value.GetObjectIds())
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Curve curve)) continue;
                    var points = PlanPoints(curve);
                    if (points.Count >= 2) lines.Add(new CutLine { Handle = curve.Handle.ToString(), Points = points });
                }

                tr.Commit();
            }
        }
        catch (System.Exception ex)
        {
            Prompts.Say(ed, $"Không đọc được đường đã chọn: {ex.Message}");
            return null;
        }

        return lines.Count > 0 ? lines : null;
    }

    /// <summary>
    /// Plan points of a curve: a line's ends, a straight polyline's vertices (closing vertex added), anything else sampled
    /// about every 0.5 m (at most 5000 points) along its length.
    /// </summary>
    private static List<PlanPoint> PlanPoints(Curve curve)
    {
        var points = new List<PlanPoint>();
        switch (curve)
        {
            case Line l:
                points.Add(new PlanPoint(l.StartPoint.X, l.StartPoint.Y));
                points.Add(new PlanPoint(l.EndPoint.X, l.EndPoint.Y));
                return points;
            case Polyline pl when !pl.HasBulges:
                for (var i = 0; i < pl.NumberOfVertices; i++)
                {
                    var v = pl.GetPoint3dAt(i);
                    points.Add(new PlanPoint(v.X, v.Y));
                }

                if (pl.Closed && points.Count > 2) points.Add(points[0]);
                return points;
        }

        var length = curve.GetDistanceAtParameter(curve.EndParam) - curve.GetDistanceAtParameter(curve.StartParam);
        var n = (int)System.Math.Min(5000, System.Math.Max(2, System.Math.Ceiling(length / 0.5)));
        for (var i = 0; i <= n; i++)
        {
            var p = curve.GetPointAtDist(length * i / n);
            points.Add(new PlanPoint(p.X, p.Y));
        }

        return points;
    }

    /// <summary>
    /// Triangles and their edges, and which edges the filter deletes. Owns the triangle collection: the edge wrappers are
    /// only used (DeleteLines) before it is disposed.
    /// </summary>
    private sealed class TinScan : System.IDisposable
    {
        public TinScan(TinSurfaceTriangleCollection triangles) => Collection = triangles;

        public TinSurfaceTriangleCollection Collection { get; }
        public int Triangles { get; set; }
        public int Flagged { get; set; }
        public List<TinSurfaceEdge> Edges { get; } = new List<TinSurfaceEdge>();
        public List<int> Delete { get; set; } = new List<int>();

        public void Dispose() => Collection.Dispose();
    }

    /// <summary>Visible triangles (GetTriangles(false)), each edge read from Edge1..3 with its vertex locations. Dispose the result.</summary>
    private static TinScan Scan(TinSurface surface, double maxEdge, List<PlanPoint> boundary)
    {
        var scan = new TinScan(surface.GetTriangles(false));
        try
        {
            var ends = new List<(PlanPoint a, PlanPoint b)>();
            foreach (var t in scan.Collection)
            {
                scan.Triangles++;
                var flag = TriangleFilter.Check(P(t.Vertex1), P(t.Vertex2), P(t.Vertex3), maxEdge, boundary);
                if (flag != TriangleFlag.None) scan.Flagged++;
                foreach (var e in new[] { t.Edge1, t.Edge2, t.Edge3 })
                {
                    scan.Edges.Add(e);
                    ends.Add((P(e.Vertex1), P(e.Vertex2)));
                }
            }

            scan.Delete = TriangleFilter.EdgesToDelete(ends, maxEdge, boundary);
            return scan;
        }
        catch
        {
            scan.Dispose();
            throw;
        }
    }

    private static PlanPoint P(TinSurfaceVertex v) => new PlanPoint(v.Location.X, v.Location.Y);

    /// <summary>"Đếm": counts only, the drawing is not changed.</summary>
    private static void Count(Document doc, ObjectId surfaceId, SurfaceSession session, List<PlanPoint> boundary)
    {
        try
        {
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var surface = (TinSurface)tr.GetObject(surfaceId, OpenMode.ForRead);
                using (var scan = Scan(surface, session.MaxEdge, boundary))
                    session.SetCount(scan.Triangles, scan.Flagged, scan.Delete.Count);
                tr.Commit();
            }
        }
        catch (System.Exception ex)
        {
            Prompts.Say(doc.Editor, $"Không đọc được tam giác của mặt phủ (GetTriangles): {ex.Message}");
        }
    }

    /// <summary>Deletes the flagged edges in one transaction (one undo). Fallback: outer boundary / maximum triangle length. True when kept.</summary>
    private static bool Cleanup(Document doc, ObjectId surfaceId, SurfaceSession session, ObjectId boundaryId, List<PlanPoint> boundary, bool askToKeep)
    {
        var ed = doc.Editor;
        bool keep;
        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            try
            {
                var surface = (TinSurface)tr.GetObject(surfaceId, OpenMode.ForWrite);
                TinScan scan = null;
                try
                {
                    scan = Scan(surface, session.MaxEdge, boundary);
                }
                catch (System.Exception ex)
                {
                    Prompts.Say(ed, $"Không đọc được tam giác của mặt phủ (GetTriangles): {ex.Message}.");
                }

                if (scan != null && scan.Delete.Count == 0)
                {
                    Prompts.Say(ed, "Không có tam giác nào cần xoá.");
                    session.SetCount(scan.Triangles, scan.Flagged, 0);
                    scan.Dispose();
                    return false;
                }

                var deleted = false;
                if (scan != null)
                {
                    using (scan)
                    {
                        try
                        {
                            surface.DeleteLines(scan.Delete.Select(i => scan.Edges[i]).ToList());
                            deleted = true;
                            Prompts.Say(ed, $"Đã xoá {scan.Delete.Count} cạnh ({scan.Flagged} / {scan.Triangles} tam giác bị loại) trên {session.Surface}.");
                        }
                        catch (System.Exception ex)
                        {
                            Prompts.Say(ed, $"TinSurface.DeleteLines không chạy được ({ex.Message}); dùng cách thay thế.");
                        }
                    }
                }

                if (!deleted && !Fallback(ed, surface, session, boundaryId)) return false;

                tr.TransactionManager.QueueForGraphicsFlush();
                ed.UpdateScreen();
                keep = !askToKeep || Prompts.AskKeep(ed);
            }
            catch (System.Exception ex)
            {
                Prompts.Say(ed, $"Lỗi khi xoá tam giác: {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
                return false;
            }

            if (keep) tr.Commit();
            else tr.Abort();
        }

        if (!keep)
        {
            ed.Regen();
            return false;
        }

        Prompts.Say(ed, $"Hoàn thành: {session.SummaryText}.\n");
        return true;
    }

    /// <summary>Picked polyline → outer boundary; max edge → the surface's maximum triangle length build option. False when neither worked.</summary>
    private static bool Fallback(Editor ed, TinSurface surface, SurfaceSession session, ObjectId boundaryId)
    {
        var any = false;
        if (!boundaryId.IsNull)
        {
            try
            {
                surface.BoundariesDefinition.AddBoundaries(new ObjectIdCollection { boundaryId }, 0.1, SurfaceBoundaryType.Outer, true);
                Prompts.Say(ed, "Đã thêm polyline làm ranh giới ngoài (Outer) của mặt phủ thay cho xoá cạnh.");
                any = true;
            }
            catch (System.Exception ex)
            {
                Prompts.Say(ed, $"Không thêm được ranh giới ngoài: {ex.Message}");
            }
        }

        if (session.MaxEdge > 0)
        {
            try
            {
                surface.BuildOptions.UseMaximumTriangleLength = true;
                surface.BuildOptions.MaximumTriangleLength = session.MaxEdge;
                surface.Rebuild();
                Prompts.Say(ed, $"Đã đặt chiều dài tam giác lớn nhất {NumberFormat.Trimmed(session.MaxEdge, 3)} m trong tuỳ chọn dựng mặt phủ thay cho xoá cạnh.");
                any = true;
            }
            catch (System.Exception ex)
            {
                Prompts.Say(ed, $"Không đặt được chiều dài tam giác lớn nhất: {ex.Message}");
            }
        }

        if (!any) Prompts.Say(ed, "Không xoá được tam giác; bản vẽ không thay đổi.");
        return any;
    }

    /// <summary>
    /// Contours at the minor interval (ExtractContours, erased again in the same transaction), crossings with each picked
    /// line, TEXT on DH_CAODO tagged with the line's handle (a rerun for the same line replaces them). One undo. True when kept.
    /// </summary>
    private static bool Label(Document doc, ObjectId surfaceId, SurfaceSession session, List<CutLine> lines, bool askToKeep)
    {
        var ed = doc.Editor;
        var options = session.LabelOptions();
        var height = session.TextHeight;
        var total = 0;
        bool keep;
        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            try
            {
                var surface = (TinSurface)tr.GetObject(surfaceId, OpenMode.ForRead);
                ObjectIdCollection extracted;
                try
                {
                    extracted = surface.ExtractContours(session.MinorInterval);
                }
                catch (System.Exception ex)
                {
                    Prompts.Say(ed, $"Không trích được đường đồng mức (TinSurface.ExtractContours): {ex.Message}. Bản vẽ không thay đổi.");
                    return false;
                }

                var contours = new List<ContourLine>();
                foreach (ObjectId id in extracted)
                {
                    // The extracted polylines are only needed for their geometry: read, then erase in this transaction.
                    var entity = tr.GetObject(id, OpenMode.ForWrite);
                    if (entity is Curve curve)
                    {
                        var points = PlanPoints(curve);
                        if (points.Count >= 2) contours.Add(new ContourLine(Elevation(curve), points));
                    }

                    entity.Erase();
                }

                if (contours.Count == 0)
                {
                    Prompts.Say(ed, "Mặt phủ không có đường đồng mức với khoảng cao đều này.");
                    return false;
                }

                foreach (var line in lines)
                {
                    var d = new TaggedDrawing(tr, doc.Database, Tool, line.Handle);
                    d.EraseTagged(null, (id, tag) => tag.Kind == LabelKind);
                    d.EnsureLayer(LabelLayer, 3);
                    foreach (var label in ContourLabelPlanner.Plan(line.Points, contours, options))
                    {
                        var p = new Point3d(label.Position.X, label.Position.Y, 0);
                        var text = new DBText
                        {
                            TextString = label.Text,
                            Height = height,
                            Rotation = label.Rotation,
                            Position = p,
                            Justify = AttachmentPoint.MiddleCenter,
                            AlignmentPoint = p,
                        };
                        var tag = new ToolTag(Tool) { Kind = LabelKind, Number = label.IsMajor ? 1 : 0 };
                        tag.Values.Add(label.Elevation);
                        d.Add(text, LabelLayer, tag);
                        text.TextStyleId = doc.Database.Textstyle;
                        text.AdjustAlignment(doc.Database);
                        total++;
                    }
                }

                tr.TransactionManager.QueueForGraphicsFlush();
                ed.UpdateScreen();
                Prompts.Say(ed, $"{total.ToString(CultureInfo.InvariantCulture)} nhãn cao độ trên {lines.Count} đường ({contours.Count} đường đồng mức).");
                keep = !askToKeep || Prompts.AskKeep(ed);
            }
            catch (System.Exception ex)
            {
                Prompts.Say(ed, $"Lỗi khi ghi cao độ đồng mức: {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
                return false;
            }

            if (keep) tr.Commit();
            else tr.Abort();
        }

        if (!keep)
        {
            ed.Regen();
            return false;
        }

        Prompts.Say(ed, $"Hoàn thành: {session.SummaryText}.\n");
        return true;
    }

    private static double Elevation(Curve curve)
    {
        switch (curve)
        {
            case Polyline pl: return pl.Elevation;
            case Polyline2d pl2: return pl2.Elevation;
            default: return curve.StartPoint.Z;
        }
    }
}
