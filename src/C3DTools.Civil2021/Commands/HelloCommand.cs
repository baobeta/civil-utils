using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using C3DTools.Core.Stations;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.HelloCommand))]

namespace C3DTools.Civil2021.Commands;

public class HelloCommand
{
    [CommandMethod("C3DTOOLS", "CTHELLO", CommandFlags.Modal)]
    public void Hello()
    {
        var editor = AcApp.DocumentManager.MdiActiveDocument.Editor;
        var alignments = CivilApplication.ActiveDocument.GetAlignmentIds().Count;
        var version = GetType().Assembly.GetName().Version?.ToString() ?? "local";
        editor.WriteMessage(
            $"\nC3DTools {version} — bản vẽ có {alignments} tuyến. Ví dụ lý trình: {StationFormatter.Format(1234.5, 2)}");
        editor.WriteMessage(
            "\nLệnh: CTCONFIG, CTALIGN, CTDIEM, CTPROFILE, CTEXPORT, CTNHAC, CTCOC, CTKHOILUONG, CTCONG\n");
    }
}
