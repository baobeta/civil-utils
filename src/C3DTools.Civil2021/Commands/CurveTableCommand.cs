using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Curves;
using C3DTools.Core.Curves;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.CurveTableCommand))]

namespace C3DTools.Civil2021.Commands;

public class CurveTableCommand
{
    /// <summary>CTYTCBANG: curve summary AutoCAD Table plus the LISP's CSV, for an alignment or a polyline done with CTYTC.</summary>
    [CommandMethod("C3DTOOLS", "CTYTCBANG", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void CurveTable()
    {
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        var messages = new List<string>();
        var preset = PresetLocator.LoadForDrawing(messages);
        foreach (var m in messages) ed.WriteMessage("\n" + m);

        var source = RouteSource.PickFirst(ed) ?? RouteSource.Prompt(ed);
        if (source == null) return;
        var session = new CurveDesignSession(preset);
        var design = Load(ed, source, session);
        if (design == null) return;
        if (design.Curves.Count == 0)
        {
            ed.WriteMessage("\nTuyến không có đường cong nào: không có gì để lập bảng.");
            return;
        }

        var point = ed.GetPoint("\nĐiểm chèn bảng: ");
        if (point.Status != PromptStatus.OK) return;
        var h = session.TextHeight > 0 ? session.TextHeight : 2.5;
        var data = CurveTableBuilder.Build(design, preset.CurveBox ?? new CurveBoxOptions());

        bool keep;
        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            try
            {
                var d = new RouteDrawing(tr, doc.Database, source.TagHandle);
                CurveTableWriter.Write(d, source.Id, data, point.Value.TransformBy(ed.CurrentUserCoordinateSystem), h,
                    m => ed.WriteMessage("\n" + m));
                tr.TransactionManager.QueueForGraphicsFlush();
                ed.UpdateScreen();
                keep = RouteWriter.AskToKeep(ed);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nLỗi khi tạo bảng yếu tố cong: {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
                return;
            }

            if (keep) tr.Commit();
            else tr.Abort();
        }

        if (!keep)
        {
            ed.Regen();
            return;
        }

        RouteWriter.WriteCsv(ed, design);
        ed.WriteMessage($"\nHoàn thành: bảng {design.Curves.Count} đường cong.\n");
    }

    /// <summary>
    /// Alignment: its own curves as built (YTCA). Polyline: RouteDesigner on the values CTYTC stored in its tags.
    /// Null (with a message) when there is nothing to tabulate.
    /// </summary>
    private static RouteDesign Load(Editor ed, RouteSource source, CurveDesignSession session)
    {
        try
        {
            source.LoadInto(session);
        }
        catch (System.InvalidOperationException ex)
        {
            ed.WriteMessage("\n" + ex.Message);
            return null;
        }

        if (session.Design == null) return null;
        using (var tr = source.Document.TransactionManager.StartTransaction())
        {
            RouteDesign design;
            if (source.IsAlignment)
            {
                var alignment = (Alignment)tr.GetObject(source.Id, OpenMode.ForRead);
                design = AsBuiltDesign.Read(alignment, (AlignmentSource)source, session.Design, new List<BoxSite>(), ed);
            }
            else if (YtcTag.ReadCurveInputs(tr, source.Document.Database, source.TagHandle).Count == 0)
            {
                ed.WriteMessage("\nPolyline này chưa được CTYTC xử lý (không có thông số R, L đã lưu). Chạy CTYTC trước.");
                design = null;
            }
            else
            {
                design = session.Design;
            }

            tr.Commit();
            return design;
        }
    }
}
