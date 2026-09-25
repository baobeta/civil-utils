using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Curves;
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
                    ? new HashSet<YtcKind> { YtcKind.Box, YtcKind.Stake }
                    : new HashSet<YtcKind> { YtcKind.Unknown, YtcKind.Curve, YtcKind.Box, YtcKind.Stake, YtcKind.Alignment };
                d.EraseTagged(new HashSet<ObjectId> { source.Id }, kinds, recreateAlignment, m => ed.WriteMessage("\n" + m));

                List<BoxSite> sites;
                List<Stake> stakes;
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
                    var alignmentDone = false;
                    if (session.CreateAlignment)
                        alignmentDone = source.IsAlignment
                            ? CurveGeometryWriter.UpdateAlignment(d, source.Id, session, ed)
                            : CurveGeometryWriter.CreateAlignment(d, source.Id, session, ed);
                    if (session.DrawCurves || (session.CreateAlignment && !alignmentDone))
                        CurveGeometryWriter.WritePlain(d, pis, design);
                    sites = CurveBoxWriter.SitesFromGeometry(pis, design);
                    stakes = RouteStakes.Build(pis, session.StartStation, design);
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
        ed.WriteMessage($"\nHoàn thành: {session.SummaryText}.\n");
        return true;
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
    internal static void WriteCsv(Editor ed, RouteDesign design)
    {
        var folder = PresetLocator.DrawingFolder();
        if (folder == null)
        {
            ed.WriteMessage("\nBản vẽ chưa được lưu: bỏ qua xuất CSV.");
            return;
        }

        try
        {
            var name = Path.GetFileNameWithoutExtension(Convert.ToString(AcCoreApp.GetSystemVariable("DWGNAME")));
            var path = Path.Combine(folder, name + "_YEUTOCONG.csv");
            using (var stream = File.Create(path)) CsvTableWriter.Write(CurveTableBuilder.BuildLispCsv(design), stream);
            ed.WriteMessage($"\nĐã xuất bảng yếu tố cong: {path}");
        }
        catch (Exception ex)
        {
            ed.WriteMessage($"\nKhông ghi được CSV: {ex.Message}");
        }
    }
}
