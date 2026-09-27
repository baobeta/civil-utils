using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Curves;
using C3DTools.Core.Profiles;
using C3DTools.Core.Tables;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace C3DTools.Civil2021.Curves;

/// <summary>Áp dụng / Xem trước: writes the whole design in one transaction, so the result is one undo.</summary>
internal static class RouteWriter
{
    private const double DefaultTextHeight = 2.5;

    /// <returns>True when the result was kept (committed).</returns>
    public static bool Write(Document doc, RouteSource source, CurveDesignSession session, CurveBoxOptions boxOptions, bool askToKeep)
    {
        var ed = doc.Editor;
        if (!session.CanApply || session.Design == null)
        {
            ed.WriteMessage("\nCòn lỗi trong bảng (dòng đỏ): sửa trước khi áp dụng.");
            return false;
        }

        var design = session.Design;
        var pis = session.Pis;
        var h = session.TextHeight > 0 ? session.TextHeight : DefaultTextHeight;
        var stakesOnly = source.IsAlignment && session.ReadOnlyGeometry;
        bool keep;
        var written = design;

        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            try
            {
                var d = new RouteDrawing(tr, doc.Database, source.TagHandle);
                var recreateAlignment = !source.IsAlignment && session.CreateAlignment;
                // "Chỉ cắm cọc + khung" replaces only boxes and stakes; curves of an earlier run stay.
                // The table (CTYTCBANG / "Bảng") is replaced only when a new one is written.
                var kinds = stakesOnly
                    ? new HashSet<YtcKind> { YtcKind.Box, YtcKind.Stake, YtcKind.Edge }
                    : new HashSet<YtcKind> { YtcKind.Unknown, YtcKind.Curve, YtcKind.Box, YtcKind.Stake, YtcKind.Alignment, YtcKind.Edge };
                d.EraseTagged(new HashSet<ObjectId> { source.Id }, kinds, recreateAlignment, m => ed.WriteMessage("\n" + m));

                List<BoxSite> sites;
                List<Stake> stakes;
                var alignmentId = source.IsAlignment ? source.Id : ObjectId.Null;
                if (stakesOnly)
                {
                    // The alignment is only read: its own curve groups give stations, points and measured T, P.
                    var alignment = (Alignment)tr.GetObject(source.Id, OpenMode.ForRead);
                    sites = new List<BoxSite>();
                    written = AsBuiltDesign.Read(alignment, (AlignmentSource)source, design, sites, ed);
                    stakes = OnAlignment(RouteStakes.FromStations(written, alignment.StartingStation, alignment.EndingStation), alignment, ed);
                }
                else
                {
                    if (session.PisEdited && !source.IsAlignment) MovePolylineVertices(tr, source.Id, pis);
                    // Before an update, the alignment's own curves: where its stations were, for "Dồn dịch đỉnh trắc dọc".
                    var oldCurves = source.IsAlignment && session.CreateAlignment && session.ShiftProfiles
                        ? OldCurves((Alignment)tr.GetObject(source.Id, OpenMode.ForRead), (AlignmentSource)source, design)
                        : null;
                    var alignmentDone = false;
                    if (session.CreateAlignment)
                        alignmentDone = source.IsAlignment
                            ? CurveGeometryWriter.UpdateAlignment(d, source.Id, session, ed, out alignmentId)
                            : CurveGeometryWriter.CreateAlignment(d, source.Id, session, ed, out alignmentId);
                    if (alignmentDone && oldCurves != null)
                        ProfileShifter.Shift(tr, (Alignment)tr.GetObject(alignmentId, OpenMode.ForRead), new StationShift(oldCurves, design.Curves), ed);
                    if (session.DrawCurves || (session.CreateAlignment && !alignmentDone))
                        CurveGeometryWriter.WritePlain(d, pis, design);
                    sites = CurveBoxWriter.SitesFromGeometry(pis, design);
                    stakes = RouteStakes.Build(pis, session.StartStation, design);
                }

                if (session.DrawEdges)
                {
                    List<EdgeLine> lines;
                    if (stakesOnly)
                    {
                        var alignment = (Alignment)tr.GetObject(source.Id, OpenMode.ForRead);
                        lines = EdgeLineBuilder.Build(EdgeWriter.OnAlignment(alignment), alignment.StartingStation, alignment.EndingStation,
                            written, session.PavementHalfWidth, session.EdgeStep);
                    }
                    else
                    {
                        lines = EdgeLineBuilder.Build(new RouteGeometry(pis, session.StartStation, design), design, session.PavementHalfWidth, session.EdgeStep);
                    }

                    if (EdgeWriter.Write(d, lines) == 0) ed.WriteMessage("\nKhông có đường cong nào có mở rộng (Wb, Wl): không vẽ polyline đoạn nối.");
                }

                if (session.WriteSuperelevation && session.HasSuperelevation)
                {
                    if (alignmentId.IsNull) ed.WriteMessage("\nSiêu cao chỉ ghi được vào Alignment: chọn Tạo/Cập nhật Alignment, hoặc chạy trên Alignment.");
                    else SuperelevationWriter.Write(tr, alignmentId, SuperelevationPlan(session, written), ed);
                }

                if (session.DrawBoxes) CurveBoxWriter.Write(d, sites, boxOptions ?? new CurveBoxOptions(), h);
                if (session.DrawStakes) StakeWriter.Write(d, stakes, written, h);
                if (session.WriteTable)
                {
                    var last = pis[pis.Count - 1];
                    CurveTableWriter.Write(d, source.Id, CurveTableBuilder.Build(written, boxOptions ?? new CurveBoxOptions()),
                        new Autodesk.AutoCAD.Geometry.Point3d(last.X + 10 * h, last.Y, 0), h, m => ed.WriteMessage("\n" + m));
                }

                tr.TransactionManager.QueueForGraphicsFlush();
                ed.UpdateScreen();
                keep = !askToKeep || Prompts.AskKeep(ed);
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\nLỗi khi vẽ yếu tố cong: {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
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

        if (session.WriteCsv) WriteCsv(ed, written);
        if (session.WriteXlsx) WriteXlsx(ed, written);
        if ((session.WriteCsv || session.WriteXlsx) && session.HasSuperelevation)
            WriteSuperelevationFiles(ed, SuperelevationPlanner.Table(SuperelevationPlan(session, written), session.StationDecimals), session.WriteCsv, session.WriteXlsx);
        ed.WriteMessage($"\nHoàn thành: {session.SummaryText}.\n");
        return true;
    }

    /// <summary>
    /// The alignment's curves before an update, by PI, with measured T1/T2: what StationShift needs. Quiet, unlike
    /// AsBuiltDesign.Read, which would compare the old alignment with the new design and warn about every difference.
    /// </summary>
    private static List<DesignedCurve> OldCurves(Alignment alignment, AlignmentSource source, RouteDesign design)
    {
        var old = new List<DesignedCurve>();
        foreach (var c in design.Curves)
        {
            var g = source.GroupFor(c);
            if (g == null) continue;
            var m = MeasuredElements.Measure(alignment, g.StartStation, g.EndStation, g.ArcMidStation);
            old.Add(new DesignedCurve
            {
                PiIndex = c.PiIndex,
                Number = c.Number,
                Input = c.Input,
                Elements = new CurveElements { T1 = m.T1, T2 = m.T2, P = m.P, K = g.EndStation - g.StartStation },
                StationStart = g.StartStation,
                StationArcStart = g.ArcStartStation,
                StationArcEnd = g.ArcEndStation,
                StationEnd = g.EndStation,
            });
        }

        return old;
    }

    /// <summary>Critical stations of the curves as written (measured ones in "Chỉ cắm cọc + khung").</summary>
    private static List<SuperelevationPoint> SuperelevationPlan(CurveDesignSession session, RouteDesign written) =>
        written.Curves.SelectMany(c => SuperelevationPlanner.Plan(c, session.CrossSlope)).OrderBy(p => p.Station).ToList();

    /// <summary>"Hiệu chỉnh góc chuyển hướng" on a polyline: its vertices become the edited PIs (arcs were already ignored).</summary>
    private static void MovePolylineVertices(Transaction tr, ObjectId polylineId, IReadOnlyList<PlanPoint> pis)
    {
        var pline = (Autodesk.AutoCAD.DatabaseServices.Polyline)tr.GetObject(polylineId, OpenMode.ForWrite);
        while (pline.NumberOfVertices > pis.Count) pline.RemoveVertexAt(pline.NumberOfVertices - 1);
        for (var i = 0; i < pis.Count; i++)
        {
            var p = new Autodesk.AutoCAD.Geometry.Point2d(pis[i].X, pis[i].Y);
            if (i < pline.NumberOfVertices) pline.SetPointAt(i, p);
            else pline.AddVertexAt(i, p, 0, 0, 0);
            pline.SetBulgeAt(i, 0);
        }
    }

    /// <summary>&lt;DWGPREFIX&gt;&lt;drawing name&gt;_SIEUCAO.csv / .xlsx.</summary>
    private static void WriteSuperelevationFiles(Editor ed, TableData table, bool csv, bool xlsx)
    {
        var folder = PresetLocator.DrawingFolder();
        if (folder == null)
        {
            ed.WriteMessage("\nBản vẽ chưa được lưu: bỏ qua xuất bảng siêu cao.");
            return;
        }

        var name = Path.Combine(folder, Convert.ToString(AcCoreApp.GetSystemVariable("DWGNAME")));
        try
        {
            if (csv) TableExport.WriteCsv(table, TableExport.SuggestPath(name, "SIEUCAO", "csv"));
            if (xlsx) TableExport.WriteXlsx(table, TableExport.SuggestPath(name, "SIEUCAO", "xlsx"), "Siêu cao");
            ed.WriteMessage($"\nĐã xuất bảng siêu cao: {TableExport.SuggestPath(name, "SIEUCAO", csv ? "csv" : "xlsx")}");
        }
        catch (Exception ex)
        {
            ed.WriteMessage($"\nKhông ghi được bảng siêu cao: {ex.Message}");
        }
    }

    /// <summary>Puts each stake on the alignment at its station; a stake that can't be located is skipped with a message.</summary>
    private static List<Stake> OnAlignment(List<Stake> stakes, Alignment alignment, Editor ed)
    {
        var placed = new List<Stake>();
        foreach (var s in stakes)
        {
            try
            {
                s.Point = MeasuredElements.Point(alignment, s.Station);
                s.Direction = MeasuredElements.Direction(alignment, s.Station);
                placed.Add(s);
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\nCọc {s.Name} tại {NumberFormat.Fixed(s.Station, 2)}: không lấy được điểm trên Alignment ({ex.Message}); bỏ qua.");
            }
        }

        return placed;
    }

    /// <summary>ytc:csv: &lt;DWGPREFIX&gt;&lt;drawing name&gt;_YEUTOCONG.csv, UTF-8 with BOM.</summary>
    internal static void WriteCsv(Editor ed, RouteDesign design) =>
        WriteFile(ed, design, "csv", "CSV", (table, path) => TableExport.WriteCsv(table, path));

    /// <summary>"Excel": the same table as the CSV in &lt;DWGPREFIX&gt;&lt;drawing name&gt;_YEUTOCONG.xlsx.</summary>
    internal static void WriteXlsx(Editor ed, RouteDesign design) =>
        WriteFile(ed, design, "xlsx", "Excel", (table, path) => TableExport.WriteXlsx(table, path, "Yếu tố cong"));

    private static void WriteFile(Editor ed, RouteDesign design, string ext, string kind, Action<TableData, string> write)
    {
        var folder = PresetLocator.DrawingFolder();
        if (folder == null)
        {
            ed.WriteMessage($"\nBản vẽ chưa được lưu: bỏ qua xuất {kind}.");
            return;
        }

        try
        {
            var name = Convert.ToString(AcCoreApp.GetSystemVariable("DWGNAME"));
            var path = TableExport.SuggestPath(Path.Combine(folder, name), "YEUTOCONG", ext);
            write(CurveTableBuilder.BuildLispCsv(design), path);
            ed.WriteMessage($"\nĐã xuất bảng yếu tố cong: {path}");
        }
        catch (Exception ex)
        {
            ed.WriteMessage($"\nKhông ghi được {kind}: {ex.Message}");
        }
    }
}
