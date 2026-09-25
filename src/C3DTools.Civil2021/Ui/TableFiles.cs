using System;
using System.IO;
using Autodesk.AutoCAD.EditorInput;
using C3DTools.Civil2021.Curves;
using C3DTools.Core.Tables;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace C3DTools.Civil2021.Ui;

/// <summary>The "CSV" / "Excel" outputs: &lt;DWGPREFIX&gt;&lt;drawing name&gt;_&lt;suffix&gt;.csv|xlsx next to the drawing. Never throws.</summary>
internal static class TableFiles
{
    /// <param name="what">What was written, for the message, e.g. "bảng cong đứng".</param>
    public static void Write(Editor ed, TableData table, string suffix, bool csv, bool xlsx, string sheetName, string what)
    {
        if (!csv && !xlsx) return;
        var folder = PresetLocator.DrawingFolder();
        if (folder == null)
        {
            Prompts.Say(ed, "Bản vẽ chưa được lưu: bỏ qua xuất CSV/Excel.");
            return;
        }

        string drawing;
        try
        {
            drawing = Path.Combine(folder, Convert.ToString(AcCoreApp.GetSystemVariable("DWGNAME")));
        }
        catch (Exception ex)
        {
            Prompts.Say(ed, $"Không lấy được tên bản vẽ ({ex.Message}): bỏ qua xuất CSV/Excel.");
            return;
        }

        if (csv) One(ed, TableExport.SuggestPath(drawing, suffix, "csv"), "CSV", what, p => TableExport.WriteCsv(table, p));
        if (xlsx) One(ed, TableExport.SuggestPath(drawing, suffix, "xlsx"), "Excel", what, p => TableExport.WriteXlsx(table, p, sheetName));
    }

    private static void One(Editor ed, string path, string kind, string what, Action<string> write)
    {
        try
        {
            write(path);
            Prompts.Say(ed, $"Đã xuất {what}: {path}");
        }
        catch (Exception ex)
        {
            Prompts.Say(ed, $"Không ghi được {kind}: {ex.Message}");
        }
    }
}
