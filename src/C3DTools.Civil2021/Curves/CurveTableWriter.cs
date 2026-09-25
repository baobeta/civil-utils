using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using C3DTools.Core.Tables;

namespace C3DTools.Civil2021.Curves;

/// <summary>The curve summary (CurveTableBuilder) as an AutoCAD Table on YTC_BANG, tagged so a rerun replaces it.</summary>
internal static class CurveTableWriter
{
    private const string Title = "BẢNG YẾU TỐ CONG";

    /// <summary>Erases this source's previous table, then writes the new one with its top-left corner at position.</summary>
    public static void Write(RouteDrawing d, ObjectId sourceId, TableData data, Point3d position, double textHeight, Action<string> warn)
    {
        d.EraseTagged(new HashSet<ObjectId> { sourceId }, new HashSet<YtcKind> { YtcKind.Table }, false, warn);

        var columns = data.Headers.Count;
        var table = new Table { TableStyle = d.Database.Tablestyle };
        table.SetSize(data.Rows.Count + 2, columns);
        table.SetRowHeight(textHeight * 2);

        // Standard table style: row 0 _TITLE (merged across), row 1 _HEADER, then _DATA rows.
        SetRowStyle(table, 0, "_TITLE");
        try
        {
            table.MergeCells(CellRange.Create(table, 0, 0, 0, columns - 1));
        }
        catch (Exception)
        {
            // Already merged by the table style.
        }

        SetCell(table, 0, 0, Title, textHeight);
        SetRowStyle(table, 1, "_HEADER");
        for (var c = 0; c < columns; c++)
        {
            var maxChars = data.Headers[c].Length;
            foreach (var row in data.Rows) maxChars = Math.Max(maxChars, row[c]?.Length ?? 0);
            table.Columns[c].Width = 0.7 * textHeight * maxChars + 2 * textHeight;
            SetCell(table, 1, c, data.Headers[c], textHeight);
        }

        for (var r = 0; r < data.Rows.Count; r++)
        {
            SetRowStyle(table, r + 2, "_DATA");
            for (var c = 0; c < columns; c++)
                SetCell(table, r + 2, c, data.Rows[r][c] ?? "", textHeight);
        }

        table.Position = position;
        d.Add(table, RouteDrawing.BoxLayer, new YtcTag { Kind = YtcKind.Table });
        table.GenerateLayout();
    }

    /// <summary>A table style without this cell style keeps the row as it is.</summary>
    private static void SetRowStyle(Table table, int row, string style)
    {
        try
        {
            table.Rows[row].Style = style;
        }
        catch (Exception)
        {
            // Style not in the table style.
        }
    }

    private static void SetCell(Table table, int row, int column, string text, double textHeight)
    {
        var cell = table.Cells[row, column];
        cell.TextString = text;
        cell.TextHeight = textHeight;
    }
}
