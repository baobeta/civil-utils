using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Curves;
using C3DTools.Civil2021.Ui;
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
        try
        {
            RunCurveTable();
        }
        catch (System.Exception ex)
        {
            // Last resort: never let an exception reach AutoCAD's unhandled-exception dialog.
            Prompts.Say(AcCoreApp.DocumentManager.MdiActiveDocument?.Editor, $"Lỗi C3DTools: {ex.Message}");
        }
    }

    private void RunCurveTable()
    {
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        var messages = new List<string>();
        var preset = PresetLocator.LoadForDrawing(messages);
        foreach (var m in messages) Prompts.Say(ed, m);

        var source = RouteSource.PickFirst(ed) ?? RouteSource.Prompt(ed);
        if (source == null) return;
        var session = new CurveDesignSession(preset);
        var design = Load(ed, source, session);
        if (design == null) return;
        if (design.Curves.Count == 0)
        {
            Prompts.Say(ed, "Tuyến không có đường cong nào: không có gì để lập bảng.");
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
                    m => Prompts.Say(ed, m));
                tr.TransactionManager.QueueForGraphicsFlush();
                ed.UpdateScreen();
                keep = Prompts.AskKeep(ed);
            }
            catch (System.Exception ex)
            {
                Prompts.Say(ed, $"Lỗi khi tạo bảng yếu tố cong: {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
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
        Prompts.Say(ed, $"Hoàn thành: bảng {design.Curves.Count} đường cong.\n");
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
            Prompts.Say(ed, ex.Message);
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
                Prompts.Say(ed, "Polyline này chưa được CTYTC xử lý (không có thông số R, L đã lưu). Chạy CTYTC trước.");
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
