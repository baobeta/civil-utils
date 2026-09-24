using System;
using System.Collections.Generic;

namespace C3DTools.Core.Tables;

/// <summary>Header + rows of already-formatted cells, shared by CSV, XLSX and AutoCAD Table output.</summary>
public sealed class TableData
{
    private readonly List<string[]> _rows = new List<string[]>();

    public TableData(params string[] headers)
    {
        if (headers == null || headers.Length == 0) throw new ArgumentException("Bảng cần ít nhất một cột.", nameof(headers));
        Headers = headers;
    }

    public IReadOnlyList<string> Headers { get; }
    public IReadOnlyList<string[]> Rows => _rows;

    public void AddRow(params string[] cells)
    {
        if (cells == null || cells.Length != Headers.Count)
            throw new ArgumentException($"Dòng cần {Headers.Count} ô.", nameof(cells));
        _rows.Add(cells);
    }
}
