using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Services;
using C3DTools.Civil2021.Ui;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.ActiveRouteCommand))]

namespace C3DTools.Civil2021.Commands;

public class ActiveRouteCommand
{
    /// <summary>CTTUYENHH: shows or changes the drawing's active route; Enter keeps the current one.</summary>
    [CommandMethod("C3DTOOLS", "CTTUYENHH", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void SetActiveRoute()
    {
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        try
        {
            // Check that the drawing has at least one alignment.
            var hasAlignments = false;
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var civil = CivilDocument.GetCivilDocument(doc.Database);
                foreach (ObjectId id in civil.GetAlignmentIds())
                {
                    if (!id.IsErased) { hasAlignments = true; break; }
                }
                tr.Commit();
            }
            if (!hasAlignments)
            {
                Prompts.Say(ed, "Bản vẽ không có alignment.");
                return;
            }

            var current = RoutePicker.Resolve(doc, out var message);
            if (message != null)
                Prompts.Say(ed, message);
            else
                Prompts.Say(ed, current.IsNull ? "Chưa có tuyến hiện hành." : "Tuyến đã chọn.");

            var opts = new PromptEntityOptions("\nChọn alignment làm tuyến hiện hành <Enter: dùng tuyến trên>: ");
            opts.SetRejectMessage("Vui lòng chọn một alignment.");
            opts.AddAllowedClass(typeof(Alignment), exactMatch: false);
            opts.AllowNone = true;

            var result = ed.GetEntity(opts);
            ObjectId chosen;
            if (result.Status == PromptStatus.OK)
            {
                chosen = result.ObjectId;
            }
            else if (result.Status == PromptStatus.None)
            {
                // Enter: keep the current route.
                if (current.IsNull) return;
                chosen = current;
            }
            else
            {
                Prompts.Say(ed, "Đã hủy.");
                return;
            }

            string name;
            bool saved;
            using (doc.LockDocument())
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                RoutePicker.Remember(doc, chosen);
                saved = RoutePicker.Save(tr, doc);
                name = ((Alignment)tr.GetObject(chosen, OpenMode.ForRead)).Name;
                tr.Commit();
            }

            if (saved)
                Prompts.Say(ed, "Tuyến hiện hành: " + name);
            else
                Prompts.Say(ed, "Không lưu được tuyến hiện hành vào bản vẽ.");
        }
        catch (System.Exception ex)
        {
            ToolWindow.LogError("CTTUYENHH", ex);
            Prompts.Say(ed, $"Lỗi C3DTools: {ex.Message}");
        }
    }
}
