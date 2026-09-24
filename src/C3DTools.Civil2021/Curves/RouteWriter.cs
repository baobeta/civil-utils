using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;
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

        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            try
            {
                var d = new RouteDrawing(tr, doc.Database, source.TagHandle);
                var recreateAlignment = !source.IsAlignment && session.CreateAlignment;
                d.EraseTagged(new HashSet<ObjectId> { source.Id }, eraseAlignments: recreateAlignment);

                // Curve geometry. "Chỉ cắm cọc + khung" never opens the alignment for write.
                if (!stakesOnly)
                {
                    var alignmentDone = false;
                    if (session.CreateAlignment)
                        alignmentDone = source.IsAlignment
                            ? CurveGeometryWriter.UpdateAlignment(d, source.Id, session, ed)
                            : CurveGeometryWriter.CreateAlignment(d, source.Id, session, ed);
                    if (session.DrawCurves || (session.CreateAlignment && !alignmentDone))
                        CurveGeometryWriter.WritePlain(d, pis, design);
                }

                var measuredOn = stakesOnly ? (Alignment)tr.GetObject(source.Id, OpenMode.ForRead) : null;
                if (session.DrawBoxes) CurveBoxWriter.Write(d, pis, design, boxOptions ?? new CurveBoxOptions(), h, measuredOn, ed);
                if (session.DrawStakes) StakeWriter.Write(d, Stakes(pis, session, measuredOn, ed), h);

                tr.TransactionManager.QueueForGraphicsFlush();
                ed.UpdateScreen();
                keep = !askToKeep || AskToKeep(ed);
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
            ed.UpdateScreen();
            return false;
        }

        if (session.WriteCsv) WriteCsv(ed, design);
        ed.WriteMessage($"\nHoàn thành: {session.SummaryText}.\n");
        return true;
    }

    /// <summary>Polyline mode: points and directions from Core geometry. Alignment mode: measured on the alignment.</summary>
    private static List<Stake> Stakes(IReadOnlyList<PlanPoint> pis, CurveDesignSession session, Alignment alignment, Editor ed)
    {
        var stakes = RouteStakes.Build(pis, session.StartStation, session.Design);
        if (alignment == null) return stakes;

        stakes[0].Station = alignment.StartingStation;
        stakes[stakes.Count - 1].Station = alignment.EndingStation;
        foreach (var s in stakes)
        {
            try
            {
                s.Point = MeasuredElements.Point(alignment, s.Station);
                s.Direction = MeasuredElements.Direction(alignment, s.Station);
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\nCọc {s.Name} tại {NumberFormat.Fixed(s.Station, 2)}: không lấy được điểm trên Alignment ({ex.Message}); dùng hình học tính toán.");
            }
        }

        return stakes;
    }

    private static bool AskToKeep(Editor ed)
    {
        var options = new PromptKeywordOptions("\nGiữ kết quả? [Co/Khong]", "Co Khong") { AllowNone = true };
        options.Keywords.Default = "Co";
        var result = ed.GetKeywords(options);
        if (result.Status == PromptStatus.None) return true;
        return result.Status == PromptStatus.OK && result.StringResult == "Co";
    }

    /// <summary>ytc:csv: &lt;DWGPREFIX&gt;&lt;drawing name&gt;_YEUTOCONG.csv, UTF-8 with BOM.</summary>
    private static void WriteCsv(Editor ed, RouteDesign design)
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
