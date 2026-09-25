using System;
using System.Collections.Generic;
using System.Linq;
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
        try
        {
            RunCurveDesign();
        }
        catch (System.Exception ex)
        {
            // Last resort: never let an exception reach AutoCAD's unhandled-exception dialog.
            AcCoreApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\nLỗi C3DTools: {ex.Message}");
        }
    }

    private void RunCurveDesign()
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
                        if (picked == null) continue;
                        var snapshot = SessionSnapshot.Take(session);
                        if (TryLoad(ed, picked, session))
                        {
                            source = picked;
                            selectedRow = 0;
                        }
                        else
                        {
                            snapshot.Restore(session);   // keep working on the previous route, edits included
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

    /// <summary>Grid state before a re-pick, restored when the new object cannot be loaded.</summary>
    private sealed class SessionSnapshot
    {
        private IReadOnlyList<PlanPoint> _pis;
        private List<CurveInput> _inputs;
        private double _startStation;
        private bool _readOnly, _createAlignment, _drawCurves;

        public static SessionSnapshot Take(CurveDesignSession s) => new SessionSnapshot
        {
            _pis = s.Pis,
            _inputs = s.Inputs.Select(i => i.Clone()).ToList(),
            _startStation = s.StartStation,
            _readOnly = s.ReadOnlyGeometry,
            _createAlignment = s.CreateAlignment,
            _drawCurves = s.DrawCurves,
        };

        public void Restore(CurveDesignSession s)
        {
            s.ReadOnlyGeometry = _readOnly;
            s.CreateAlignment = _createAlignment;
            s.DrawCurves = _drawCurves;
            s.StartStation = _startStation;
            if (_pis.Count >= 2) s.Load(_pis, _inputs);
        }
    }
}
