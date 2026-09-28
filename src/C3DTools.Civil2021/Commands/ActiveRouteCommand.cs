using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Services;
using C3DTools.Civil2021.Ui;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.ActiveRouteCommand))]

namespace C3DTools.Civil2021.Commands;

public class ActiveRouteCommand
{
    /// <summary>CTTUYENHH: picks the drawing's active route; Enter shows the current one.</summary>
    [CommandMethod("C3DTOOLS", "CTTUYENHH", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void SetActiveRoute()
    {
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        try
        {
            var current = RoutePicker.Resolve(doc, out var message);
            Prompts.Say(ed, message ?? (current.IsNull ? "Chưa có tuyến hiện hành." : "Tuyến đã chọn."));
            var picked = Prompts.PickEntity<Alignment>(ed, "Chọn alignment làm tuyến hiện hành <Enter: giữ nguyên>: ");
            if (picked.IsNull) picked = current;
            if (picked.IsNull) return;
            RoutePicker.Use(doc, picked);
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                Prompts.Say(ed, "Tuyến hiện hành: " + ((Alignment)tr.GetObject(picked, OpenMode.ForRead)).Name + "\n");
                tr.Commit();
            }
        }
        catch (System.Exception ex)
        {
            ToolWindow.LogError("CTTUYENHH", ex);
            Prompts.Say(ed, $"Lỗi C3DTools: {ex.Message}");
        }
    }
}
