using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;

namespace C3DTools.Civil2021.Ui;

/// <summary>The only command-line interaction a tool has besides its dialog: picks and the "Giữ kết quả?" question.</summary>
internal static class Prompts
{
    /// <summary>One object of type T (or derived); ObjectId.Null when cancelled.</summary>
    public static ObjectId PickEntity<T>(Editor ed, string message) where T : Entity
    {
        var options = new PromptEntityOptions("\n" + message);
        options.SetRejectMessage($"\nChỉ chọn {typeof(T).Name}.");
        options.AddAllowedClass(typeof(T), false);
        var result = ed.GetEntity(options);
        return result.Status == PromptStatus.OK ? result.ObjectId : ObjectId.Null;
    }

    /// <summary>A point in WCS, or null when cancelled.</summary>
    public static Point3d? PickPoint(Editor ed, string message)
    {
        var result = ed.GetPoint(new PromptPointOptions("\n" + message));
        if (result.Status != PromptStatus.OK) return null;
        return result.Value.TransformBy(ed.CurrentUserCoordinateSystem);
    }

    /// <summary>After a preview: "Giữ kết quả? [Co/Khong]", Enter = Co. False on Khong or Esc.</summary>
    public static bool AskKeep(Editor ed)
    {
        var options = new PromptKeywordOptions("\nGiữ kết quả? [Co/Khong]", "Co Khong") { AllowNone = true };
        options.Keywords.Default = "Co";
        var result = ed.GetKeywords(options);
        if (result.Status == PromptStatus.None) return true;
        return result.Status == PromptStatus.OK && result.StringResult == "Co";
    }

    /// <summary>
    /// "Chỉ điểm…": points in WCS, one after another with a rubber band, Enter to finish (at least two), "Lui" to drop
    /// the last one. Null when cancelled. The picked legs show as temporary red lines until the prompt ends.
    /// </summary>
    public static List<Point3d> PickPoints(Editor ed, string first, string next)
    {
        var points = new List<Point3d>();
        var legs = new List<Line>();
        try
        {
            while (true)
            {
                var options = new PromptPointOptions("\n" + (points.Count == 0 ? first : next)) { AllowNone = points.Count >= 2 };
                if (points.Count > 0)
                {
                    options.UseBasePoint = true;
                    options.BasePoint = points[points.Count - 1].TransformBy(ed.CurrentUserCoordinateSystem.Inverse());
                    options.UseDashedLine = true;
                    options.Keywords.Add("Lui");
                }

                var result = ed.GetPoint(options);
                if (result.Status == PromptStatus.None) return points;
                if (result.Status == PromptStatus.Keyword)
                {
                    points.RemoveAt(points.Count - 1);
                    if (legs.Count > 0)
                    {
                        Erase(legs[legs.Count - 1]);
                        legs.RemoveAt(legs.Count - 1);
                    }

                    continue;
                }

                if (result.Status != PromptStatus.OK) return null;
                var p = result.Value.TransformBy(ed.CurrentUserCoordinateSystem);
                if (points.Count > 0)
                {
                    var leg = new Line(points[points.Count - 1], p) { ColorIndex = 1 };
                    TransientManager.CurrentTransientManager.AddTransient(leg, TransientDrawingMode.DirectShortTerm, 128, new IntegerCollection());
                    legs.Add(leg);
                }

                points.Add(p);
            }
        }
        finally
        {
            foreach (var leg in legs) Erase(leg);
        }
    }

    /// <summary>A point picked near the alignment, as its station; null when cancelled or off the alignment.</summary>
    public static double? PickStation(Editor ed, Autodesk.Civil.DatabaseServices.Alignment alignment, string message)
    {
        var p = PickPoint(ed, message);
        if (p == null) return null;
        try
        {
            double station = 0, offset = 0;
            alignment.StationOffset(p.Value.X, p.Value.Y, ref station, ref offset);
            return station;
        }
        catch (System.Exception)
        {
            Say(ed, "Điểm chọn nằm ngoài phạm vi alignment.");
            return null;
        }
    }

    private static void Erase(Line line)
    {
        TransientManager.CurrentTransientManager.EraseTransient(line, new IntegerCollection());
        line.Dispose();
    }

    /// <summary>Writes the message on a new command-line line; does nothing without an editor.</summary>
    public static void Say(Editor ed, string message) => ed?.WriteMessage("\n" + message);
}
