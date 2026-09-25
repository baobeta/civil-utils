using System;
using System.Collections.Generic;
using System.Globalization;
using C3DTools.Core.Presets;
using C3DTools.Core.Stations;

namespace C3DTools.Core.Sections;

/// <summary>Where one item (section view with its table) goes: its lower-left corner in drawing units.</summary>
public sealed class SheetPlacement
{
    public SheetPlacement(int item, int sheet, int row, int column, double x, double y, bool fits)
    {
        Item = item;
        Sheet = sheet;
        Row = row;
        Column = column;
        X = x;
        Y = y;
        Fits = fits;
    }

    /// <summary>Index into the items passed to Pack.</summary>
    public int Item { get; }

    /// <summary>0-based sheet.</summary>
    public int Sheet { get; }

    /// <summary>0 = top row.</summary>
    public int Row { get; }

    /// <summary>0 = left column.</summary>
    public int Column { get; }

    public double X { get; }
    public double Y { get; }

    /// <summary>False when the item is larger than its cell (it is still centred on the cell).</summary>
    public bool Fits { get; }
}

/// <summary>One sheet frame in drawing units: the paper outline, the inner (margin) frame and its items.</summary>
public sealed class SheetFrame
{
    public SheetFrame(int index, double x, double y, double width, double height, double innerLeft, double innerBottom, double innerRight,
        double innerTop, int firstItem, int lastItem)
    {
        Index = index;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        InnerLeft = innerLeft;
        InnerBottom = innerBottom;
        InnerRight = innerRight;
        InnerTop = innerTop;
        FirstItem = firstItem;
        LastItem = lastItem;
    }

    public int Index { get; }

    /// <summary>Lower-left corner of the paper.</summary>
    public double X { get; }

    public double Y { get; }
    public double Width { get; }
    public double Height { get; }
    public double InnerLeft { get; }
    public double InnerBottom { get; }
    public double InnerRight { get; }
    public double InnerTop { get; }

    /// <summary>Indexes of the first and last item on this sheet.</summary>
    public int FirstItem { get; }

    public int LastItem { get; }
}

public sealed class SheetPlan
{
    public SheetPlan(IReadOnlyList<SheetPlacement> placements, IReadOnlyList<SheetFrame> sheets, double cellWidth, double cellHeight)
    {
        Placements = placements;
        Sheets = sheets;
        CellWidth = cellWidth;
        CellHeight = cellHeight;
    }

    /// <summary>One per item, in item order.</summary>
    public IReadOnlyList<SheetPlacement> Placements { get; }

    public IReadOnlyList<SheetFrame> Sheets { get; }
    public int SheetCount => Sheets.Count;

    /// <summary>Drawing units.</summary>
    public double CellWidth { get; }

    public double CellHeight { get; }

    public int Oversize
    {
        get
        {
            var n = 0;
            foreach (var p in Placements) if (!p.Fits) n++;
            return n;
        }
    }
}

/// <summary>
/// CTXEPTRANG: puts items (already in station order) onto sheets, row-major from the top-left cell, Columns × Rows
/// per sheet. The inner frame (paper minus margins) is split into equal cells with Gap between them; each item is
/// centred on its cell. Sheets sit left to right from the origin (lower-left of sheet 1) with one Gap·2 between them.
/// Paper sizes, margins and gap are mm; unitsPerMm converts them to drawing units (Scale / 1000 for metres).
/// </summary>
public static class SheetPacker
{
    public static SheetPlan Pack(IReadOnlyList<(double Width, double Height)> items, SheetLayoutOptions layout, double unitsPerMm,
        double originX = 0, double originY = 0)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (layout == null) throw new ArgumentNullException(nameof(layout));
        if (!(unitsPerMm > 0)) throw new ArgumentOutOfRangeException(nameof(unitsPerMm));
        var error = Validate(layout);
        if (error != null) throw new ArgumentException(error, nameof(layout));

        var u = unitsPerMm;
        double w = layout.Width * u, h = layout.Height * u, gap = layout.Gap * u;
        double innerW = (layout.Width - layout.MarginLeft - layout.MarginRight) * u;
        double innerH = (layout.Height - layout.MarginTop - layout.MarginBottom) * u;
        var cellW = (innerW - (layout.Columns - 1) * gap) / layout.Columns;
        var cellH = (innerH - (layout.Rows - 1) * gap) / layout.Rows;
        var perSheet = layout.Columns * layout.Rows;
        var sheetCount = (items.Count + perSheet - 1) / perSheet;

        var sheets = new List<SheetFrame>();
        for (var s = 0; s < sheetCount; s++)
        {
            var x = originX + s * (w + 2 * gap);
            sheets.Add(new SheetFrame(s, x, originY, w, h,
                x + layout.MarginLeft * u, originY + layout.MarginBottom * u, x + w - layout.MarginRight * u, originY + h - layout.MarginTop * u,
                s * perSheet, Math.Min(items.Count, (s + 1) * perSheet) - 1));
        }

        var placements = new List<SheetPlacement>();
        for (var i = 0; i < items.Count; i++)
        {
            var sheet = sheets[i / perSheet];
            var k = i % perSheet;
            int row = k / layout.Columns, column = k % layout.Columns;
            var cellLeft = sheet.InnerLeft + column * (cellW + gap);
            var cellTop = sheet.InnerTop - row * (cellH + gap);
            var (iw, ih) = items[i];
            var fits = iw <= cellW + 1e-9 && ih <= cellH + 1e-9;
            placements.Add(new SheetPlacement(i, sheet.Index, row, column, cellLeft + (cellW - iw) / 2, cellTop - cellH + (cellH - ih) / 2, fits));
        }

        return new SheetPlan(placements, sheets, cellW, cellH);
    }

    /// <summary>
    /// "TRẮC NGANG – Tờ 1/2 – Km0+000.00 … Km0+100.00" (one station when both are the same; no range when either is
    /// unknown, NaN).
    /// </summary>
    public static string Title(int sheet, int count, double firstStation, double lastStation, int stationDecimals = 2)
    {
        var head = "TRẮC NGANG – Tờ " + (sheet + 1).ToString(CultureInfo.InvariantCulture) + "/" + count.ToString(CultureInfo.InvariantCulture);
        if (double.IsNaN(firstStation) || double.IsNaN(lastStation)) return head;
        var from = StationFormatter.Format(firstStation, stationDecimals);
        var to = StationFormatter.Format(lastStation, stationDecimals);
        var range = from == to ? from : from + " … " + to;
        return head + " – " + range;
    }

    /// <summary>A Vietnamese message for the first invalid value, or null.</summary>
    public static string Validate(SheetLayoutOptions layout)
    {
        if (layout == null) return "Chưa có khổ giấy.";
        if (!(layout.Width > 0) || !(layout.Height > 0)) return "Khổ giấy phải lớn hơn 0.";
        if (layout.MarginLeft < 0 || layout.MarginRight < 0 || layout.MarginTop < 0 || layout.MarginBottom < 0) return "Lề không được âm.";
        if (layout.Columns < 1 || layout.Rows < 1) return "Số cột và số hàng phải từ 1 trở lên.";
        if (layout.Gap < 0) return "Khoảng hở không được âm.";
        if (layout.Width - layout.MarginLeft - layout.MarginRight - (layout.Columns - 1) * layout.Gap <= 0) return "Lề trái/phải và khoảng hở rộng hơn khổ giấy.";
        if (layout.Height - layout.MarginTop - layout.MarginBottom - (layout.Rows - 1) * layout.Gap <= 0) return "Lề trên/dưới và khoảng hở cao hơn khổ giấy.";
        return null;
    }
}
