using System;
using System.IO;
using System.Linq;
using System.Text;

namespace C3DTools.Core.Tables;

public static class CsvTableWriter
{
    public static void Write(TableData table, Stream stream, char delimiter = ',')
    {
        if (table == null) throw new ArgumentNullException(nameof(table));
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        // BOM so Excel opens Vietnamese text as UTF-8.
        using var writer = new StreamWriter(stream, new UTF8Encoding(true), 4096, leaveOpen: true) { NewLine = "\r\n" };
        writer.WriteLine(string.Join(delimiter.ToString(), table.Headers.Select(h => Escape(h, delimiter))));
        foreach (var row in table.Rows)
            writer.WriteLine(string.Join(delimiter.ToString(), row.Select(c => Escape(c, delimiter))));
    }

    private static string Escape(string value, char delimiter)
    {
        value ??= "";
        var needsQuotes = value.IndexOf(delimiter) >= 0 || value.IndexOfAny(new[] { '"', '\r', '\n' }) >= 0;
        return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
