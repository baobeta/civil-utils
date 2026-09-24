using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using C3DTools.Core.Tables;

namespace C3DTools.Civil2021.Curves;

/// <summary>The curve summary (CurveTableBuilder) as an AutoCAD Table on YTC_BANG, tagged so a rerun replaces it.</summary>
internal static class CurveTableWriter
{
    /// <summary>Erases this source's previous table, then writes the new one with its top-left corner at position.</summary>
    public static void Write(RouteDrawing d, ObjectId sourceId, TableData data, Point3d position, double textHeight, Action<string> warn)
    {
        d.EraseTagged(new HashSet<ObjectId> { sourceId }, new HashSet<YtcKind> { YtcKind.Table }, false, warn);

        var columns = data.Headers.Count;
        var table = new Table { TableStyle = d.Database.Tablestyle };
        table.SetSize(data.Rows.Count + 1, columns);
        try
        {
            // Row 0 is the header: keep it as separate cells even if the table style merges its title row.
            table.UnmergeCells(CellRange.Create(table, 0, 0, 0, columns - 1));
        }
        catch (Exception)
        {
            // Nothing merged.
        }

        table.SetRowHeight(textHeight * 2);
        for (var c = 0; c < columns; c++)
        {
            var maxChars = data.Headers[c].Length;
            foreach (var row in data.Rows) maxChars = Math.Max(maxChars, row[c]?.Length ?? 0);
            table.Columns[c].Width = 0.7 * textHeight * maxChars + 2 * textHeight;
            SetCell(table, 0, c, data.Headers[c], textHeight);
        }

        for (var r = 0; r < data.Rows.Count; r++)
            for (var c = 0; c < columns; c++)
                SetCell(table, r + 1, c, data.Rows[r][c] ?? "", textHeight);

        table.Position = position;
        d.Add(table, RouteDrawing.BoxLayer, new YtcTag { Kind = YtcKind.Table });
        table.GenerateLayout();
    }

    private static void SetCell(Table table, int row, int column, string text, double textHeight)
    {
        var cell = table.Cells[row, column];
        cell.TextString = text;
        cell.TextHeight = textHeight;
    }
}
