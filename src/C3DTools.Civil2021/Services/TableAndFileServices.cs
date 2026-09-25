using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using C3DTools.Core.Tables;

namespace C3DTools.Civil2021.Services;

internal static class TableBuilder
{
    public static ObjectId Add(
        Transaction transaction,
        Database database,
        TableData data,
        Point3d position,
        string layerName = "0")
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        var rowCount = data.Rows.Count + 1;
        var columnCount = data.Headers.Count;
        var table = new Table();
        table.SetSize(rowCount, columnCount);
        table.Position = position;
        table.Layer = layerName;
        table.GenerateLayout();
        for (var row = 0; row < rowCount; row++)
        for (var column = 0; column < columnCount; column++)
        {
            var cell = table.Cells[row, column];
            cell.TextHeight = row == 0 ? 3.0 : 2.5;
            if (cell.Contents.Count > 0) cell.Contents[0].IsAutoScale = true;
        }
        table.SetRowHeight(6.0);
        table.SetColumnWidth(Math.Max(18.0, 120.0 / Math.Max(1, columnCount)));

        for (var column = 0; column < data.Headers.Count; column++)
            table.Cells[0, column].TextString = data.Headers[column];
        for (var row = 0; row < data.Rows.Count; row++)
        for (var column = 0; column < data.Headers.Count; column++)
            table.Cells[row + 1, column].TextString = data.Rows[row][column] ?? "";

        var modelSpace = (BlockTableRecord)transaction.GetObject(
            SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        modelSpace.AppendEntity(table);
        transaction.AddNewlyCreatedDBObject(table, true);
        return table.ObjectId;
    }
}

internal static class CsvExportService
{
    public static void Save(TableData data, string path, char delimiter = ',')
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Chưa chọn đường dẫn CSV.", nameof(path));
        var fullPath = Path.GetFullPath(path);
        var folder = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(folder);
        using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        CsvTableWriter.Write(data, stream, delimiter);
    }
}

internal static class TextFileReader
{
    public static string[] ReadAllLines(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2).Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2).Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

        try
        {
            return new UTF8Encoding(false, true).GetString(bytes)
                .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1258).GetString(bytes)
                .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        }
    }
}
