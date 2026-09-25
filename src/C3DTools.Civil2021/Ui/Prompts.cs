using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

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

    /// <summary>Writes the message on a new command-line line; does nothing without an editor.</summary>
    public static void Say(Editor ed, string message) => ed?.WriteMessage("\n" + message);
}
