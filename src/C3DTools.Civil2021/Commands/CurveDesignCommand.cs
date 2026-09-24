using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Runtime;
using C3DTools.Civil2021.Curves;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Curves;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.CurveDesignCommand))]

namespace C3DTools.Civil2021.Commands;

public class CurveDesignCommand
{
    /// <summary>CTYTC: the curve elements dialog (YTC / YTCA). The dialog closes whenever the drawing is needed, then reopens with the same session.</summary>
    [CommandMethod("C3DTOOLS", "CTYTC", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void CurveDesign()
    {
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        var messages = new List<string>();
        var preset = PresetLocator.LoadForDrawing(messages);
        foreach (var m in messages) ed.WriteMessage("\n" + m);

        var session = new CurveDesignSession(preset);
        var source = RouteSource.PickFirst(ed) ?? RouteSource.Prompt(ed);
        if (source == null || !TryLoad(ed, source, session)) return;

        var selectedRow = 0;
        try
        {
            while (true)
            {
                var window = new CurveDesignWindow(session, source.Description, source.IsAlignment, selectedRow);
                AcCoreApp.ShowModalWindow(window);
                selectedRow = Math.Max(0, window.SelectedRow);
                RouteSource.ClearMarker();

                switch (window.Action)
                {
                    case DialogAction.Pick:
                        var picked = RouteSource.Prompt(ed);
                        if (picked != null && TryLoad(ed, picked, session))
                        {
                            source = picked;
                            selectedRow = 0;
                        }
                        else if (picked != null)
                        {
                            TryLoad(ed, source, session);   // keep working on the previous route
                        }

                        continue;
                    case DialogAction.ZoomToPi:
                        if (selectedRow < session.Rows.Count) RouteSource.ZoomAndMark(ed, session.Rows[selectedRow].Pi, ZoomHeight(session, selectedRow));
                        continue;
                    case DialogAction.ReadWidening:
                        (source as AlignmentSource)?.ReadWidening(session);
                        continue;
                    case DialogAction.Preview:
                        if (RouteWriter.Write(doc, source, session, preset.CurveBox, askToKeep: true)) return;
                        continue;   // Khong: rolled back, back to the dialog
                    case DialogAction.Apply:
                        RouteWriter.Write(doc, source, session, preset.CurveBox, askToKeep: false);
                        return;
                    default:
                        return;
                }
            }
        }
        finally
        {
            RouteSource.ClearMarker();
        }
    }

    private static bool TryLoad(Autodesk.AutoCAD.EditorInput.Editor ed, RouteSource source, CurveDesignSession session)
    {
        try
        {
            source.LoadInto(session);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            ed.WriteMessage("\n" + ex.Message);
            return false;
        }
    }

    /// <summary>View height around a PI: three times its longer tangent, at least 100 m.</summary>
    private static double ZoomHeight(CurveDesignSession session, int row)
    {
        var e = session.Design?.Curves[session.Rows[row].Index].Elements;
        return e == null ? 100 : Math.Max(100, 3 * Math.Max(e.T1, e.T2));
    }
}
