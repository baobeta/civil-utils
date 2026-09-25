using System;
using System.IO;

namespace C3DTools.Core.Tables;

/// <summary>Writes a TableData to the files the "CSV" / "Excel" output checkboxes produce.</summary>
public static class TableExport
{
    /// <summary>UTF-8 CSV with BOM (CsvTableWriter); overwrites an existing file.</summary>
    public static void WriteCsv(TableData table, string path)
    {
        if (table == null) throw new ArgumentNullException(nameof(table));
        if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
        using var stream = File.Create(path);
        CsvTableWriter.Write(table, stream);
    }

    /// <summary>One-sheet .xlsx (MinimalXlsxWriter); overwrites an existing file. sheetName is sanitised to Excel's rules.</summary>
    public static void WriteXlsx(TableData table, string path, string sheetName)
    {
        if (table == null) throw new ArgumentNullException(nameof(table));
        if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
        using var stream = File.Create(path);
        MinimalXlsxWriter.Write(table, stream, sheetName);
    }

    /// <summary>&lt;drawing folder&gt;/&lt;drawing name&gt;_&lt;suffix&gt;.&lt;ext&gt;, e.g. C:\Du an\Tuyen.dwg → C:\Du an\Tuyen_TOADO.csv.</summary>
    public static string SuggestPath(string drawingPath, string suffix, string ext)
    {
        if (string.IsNullOrEmpty(drawingPath)) throw new ArgumentNullException(nameof(drawingPath));
        var folder = Path.GetDirectoryName(drawingPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(drawingPath);
        var extension = (ext ?? "").TrimStart('.');
        var file = string.IsNullOrEmpty(suffix) ? name : name + "_" + suffix;
        return Path.Combine(folder, extension.Length == 0 ? file : file + "." + extension);
    }
}
