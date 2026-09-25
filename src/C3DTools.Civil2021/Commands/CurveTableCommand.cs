using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Curves;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Curves;
using C3DTools.Core.Presets;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.CurveTableCommand))]

namespace C3DTools.Civil2021.Commands;

public class CurveTableCommand
{
    /// <summary>CTYTCBANG: dialog, then the curve summary AutoCAD Table, the LISP's CSV and/or Excel, for an alignment or a polyline done with CTYTC.</summary>
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

    private const string Command = "CTYTCBANG";

    private void RunCurveTable()
    {
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        var messages = new List<string>();
        var preset = PresetLocator.LoadForDrawing(messages);
        foreach (var m in messages) Prompts.Say(ed, m);

        var memory = ToolWindow.Options;
        var options = new CurveTableOptions
        {
            WriteTable = memory.Get(Command, "Bang", true),
            WriteCsv = memory.Get(Command, "CSV", true),
            WriteXlsx = memory.Get(Command, "Excel", false),
        };

        RouteSource source = null;
        CurveDesignSession session = null;
        RouteDesign design = null;
        var first = RouteSource.PickFirst(ed);
        if (first != null) TryLoad(ed, preset, first, options, ref source, ref session, ref design);

        while (true)
        {
            var window = new CurveTableWindow(options, source?.Description);
            var action = window.ShowModal();
            memory.Set(Command, "Bang", options.WriteTable);
            memory.Set(Command, "CSV", options.WriteCsv);
            memory.Set(Command, "Excel", options.WriteXlsx);
            ToolWindow.SaveOptions();

            switch (action)
            {
                case DialogAction.Pick:
                    var picked = RouteSource.Prompt(ed);
                    if (picked != null) TryLoad(ed, preset, picked, options, ref source, ref session, ref design);
                    continue;
                case DialogAction.Preview:
                case DialogAction.Apply:
                    if (!options.CanApply) continue;
                    if (Write(doc, source, session, design, preset.CurveBox, options, askToKeep: action == DialogAction.Preview)) return;
                    continue;   // Khong or no insertion point: back to the dialog
                default:
                    return;
            }
        }
    }

    /// <summary>Loads picked into a fresh session; on failure the previous route stays.</summary>
    private static void TryLoad(Editor ed, ProjectPreset preset, RouteSource picked, CurveTableOptions options,
        ref RouteSource source, ref CurveDesignSession session, ref RouteDesign design)
    {
        var newSession = new CurveDesignSession(preset);
        var newDesign = Load(ed, picked, newSession);
        if (newDesign == null) return;
        source = picked;
        session = newSession;
        design = newDesign;
        options.CurveCount = newDesign.Curves.Count;
    }

    /// <returns>True when the result was kept.</returns>
    private static bool Write(Document doc, RouteSource source, CurveDesignSession session, RouteDesign design,
        CurveBoxOptions boxOptions, CurveTableOptions options, bool askToKeep)
    {
        var ed = doc.Editor;
        if (options.WriteTable)
        {
            var point = ed.GetPoint("\nĐiểm chèn bảng: ");
            if (point.Status != PromptStatus.OK) return false;
            var h = session.TextHeight > 0 ? session.TextHeight : 2.5;
            var data = CurveTableBuilder.Build(design, boxOptions ?? new CurveBoxOptions());

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
                    keep = !askToKeep || Prompts.AskKeep(ed);
                }
                catch (System.Exception ex)
                {
                    Prompts.Say(ed, $"Lỗi khi tạo bảng yếu tố cong: {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
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
        }

        if (options.WriteCsv) RouteWriter.WriteCsv(ed, design);
        if (options.WriteXlsx) RouteWriter.WriteXlsx(ed, design);
        Prompts.Say(ed, $"Hoàn thành: bảng {design.Curves.Count} đường cong.\n");
        return true;
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
