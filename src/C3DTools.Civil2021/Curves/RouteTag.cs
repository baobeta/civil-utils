using Autodesk.AutoCAD.DatabaseServices;
using C3DTools.Civil2021.Drawing;

namespace C3DTools.Civil2021.Curves;

/// <summary>
/// What CTTUYEN remembers on the alignment it creates: regapp C3DTOOLS_TUYEN, (1040 plan scale, 1040 design speed,
/// 1000 assembly name). CTYTC reads the scale for its text height.
/// </summary>
internal static class RouteTag
{
    public const string Tool = "TUYEN";

    public static void Write(Transaction tr, Database db, DBObject alignment, double scale, double speed, string assembly)
    {
        TaggedDrawing.EnsureRegApp(tr, db, ToolTag.RegAppName(Tool));
        var tag = new ToolTag(Tool) { SourceHandle = alignment.Handle.ToString(), Kind = 1, Text = assembly ?? "" };
        tag.Values.Add(scale);
        tag.Values.Add(speed);
        using (var rb = tag.ToXData()) alignment.XData = rb;
    }

    public static bool TryRead(DBObject alignment, out double scale, out double speed, out string assembly)
    {
        scale = speed = 0;
        assembly = null;
        var tag = ToolTag.Read(alignment, Tool);
        if (tag == null) return false;
        scale = tag.Values.Count > 0 ? tag.Values[0] : 0;
        speed = tag.Values.Count > 1 ? tag.Values[1] : 0;
        assembly = string.IsNullOrEmpty(tag.Text) ? null : tag.Text;
        return true;
    }
}
