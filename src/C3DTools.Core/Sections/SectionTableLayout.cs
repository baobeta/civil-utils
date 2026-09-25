using System;
using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Profiles;

namespace C3DTools.Core.Sections;

/// <summary>
/// The section table in drawing units, sized by ProfileTableLayout's rules: rows downward from top, the label column
/// [left − labelWidth, left], offsets at xOfOffset(offset) inside [left, right]. Row heights: at least rowHeight; a
/// per-offset row with rotated text is as tall as its longest text + h, every other row at least 2.6·h.
/// Offsets are often closer than a text line (curbs, ditches), so a column is kept only when its text clears the
/// previous kept column's text and tick; the others are left out of every per-offset row (SkippedColumns), and
/// khoảng cách lẻ is measured between the kept columns. Diện tích rows are one cell across the table.
/// </summary>
public sealed class SectionTableLayout
{
    private SectionTableLayout() { }

    public List<ProfileTableLine> Lines { get; } = new List<ProfileTableLine>();
    public List<ProfileTableText> Texts { get; } = new List<ProfileTableText>();
    public List<double> RowHeights { get; } = new List<double>();

    public double Top { get; private set; }
    public double Bottom { get; private set; }

    /// <summary>Left edge of the label column.</summary>
    public double Left { get; private set; }

    public double Right { get; private set; }

    /// <summary>Offsets printed, left to right.</summary>
    public List<double> KeptOffsets { get; } = new List<double>();

    /// <summary>Offsets left out because they are too close to the previous one (or outside the view).</summary>
    public int SkippedColumns { get; private set; }

    /// <summary>Span texts left out because the span is too narrow even for rotated text.</summary>
    public int SkippedTexts { get; private set; }

    public static SectionTableLayout Build(SectionTableModel model, Func<double, double> xOfOffset, double top, double left, double right,
        double rowHeight, double textHeight, double labelWidth, bool rotateText)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        if (xOfOffset == null) throw new ArgumentNullException(nameof(xOfOffset));
        if (!(rowHeight > 0) || !(textHeight > 0) || !(labelWidth >= 0)) throw new ArgumentOutOfRangeException(nameof(rowHeight));
        if (!(right > left)) throw new ArgumentOutOfRangeException(nameof(right));

        var h = textHeight;
        var layout = new SectionTableLayout { Top = top, Left = left - labelWidth, Right = right };
        var perOffset = model.Rows.Where(r => r.Kind == SectionTableRowKind.PerOffset).ToList();
        var kept = layout.KeepColumns(model, perOffset, xOfOffset, left, right, h, rotateText);

        foreach (var row in model.Rows)
        {
            var needed = 2.6 * h;
            if (row.Kind == SectionTableRowKind.PerOffset && rotateText)
                needed = kept.Select(i => ProfileTableLayout.TextWidth(Cell(row, i), h)).DefaultIfEmpty(0).Max() + h;
            layout.RowHeights.Add(Math.Max(rowHeight, needed));
        }

        layout.Bottom = top - layout.RowHeights.Sum();

        var y = top;
        layout.Lines.Add(new ProfileTableLine(layout.Left, y, right, y));
        foreach (var rh in layout.RowHeights)
        {
            y -= rh;
            layout.Lines.Add(new ProfileTableLine(layout.Left, y, right, y));
        }

        foreach (var x in new[] { layout.Left, left, right })
            layout.Lines.Add(new ProfileTableLine(x, top, x, layout.Bottom));

        var rowTop = top;
        for (var r = 0; r < model.Rows.Count; r++)
        {
            var row = model.Rows[r];
            var rh = layout.RowHeights[r];
            var rowBottom = rowTop - rh;
            var mid = rowTop - rh / 2;
            layout.Texts.Add(new ProfileTableText(left - labelWidth / 2, mid, row.Label, 0, h));

            if (row.Kind == SectionTableRowKind.PerOffset)
            {
                foreach (var i in kept)
                {
                    var x = xOfOffset(model.Offsets[i]);
                    var text = Cell(row, i);
                    if (rotateText)
                    {
                        if (x > left + 1e-9 && x < right - 1e-9) layout.Lines.Add(new ProfileTableLine(x, rowTop, x, rowBottom));
                        if (text.Length > 0) layout.Texts.Add(new ProfileTableText(x + RotatedSide(x, left, h), mid, text, Math.PI / 2, h));
                    }
                    else if (text.Length > 0)
                    {
                        var half = ProfileTableLayout.TextWidth(text, h) / 2 + 0.1 * h;
                        layout.Texts.Add(new ProfileTableText(Math.Max(left + half, Math.Min(right - half, x)), mid, text, 0, h));
                    }
                }
            }
            else
            {
                var spans = row.Key == SectionTableBuilder.PartialDistance ? KeptGaps(model, kept, row.Decimals) : row.Spans;
                var edges = new HashSet<double>();
                foreach (var span in spans)
                {
                    double x1 = Clamp(xOfOffset(span.From), left, right), x2 = Clamp(xOfOffset(span.To), left, right);
                    if (Math.Abs(x2 - x1) <= 1e-9) continue;
                    foreach (var x in new[] { x1, x2 })
                    {
                        if (x > left + 1e-9 && x < right - 1e-9 && edges.Add(Math.Round(x, 6)))
                            layout.Lines.Add(new ProfileTableLine(x, rowTop, x, rowBottom));
                    }

                    // Diện tích: one cell across the whole table, not just between the outer offsets.
                    var whole = row.Key == SectionTableBuilder.CutArea || row.Key == SectionTableBuilder.FillArea;
                    layout.PlaceSpanText(span, whole ? left : Math.Min(x1, x2), whole ? right : Math.Max(x1, x2), mid, rh, h);
                }
            }

            rowTop = rowBottom;
        }

        return layout;
    }

    /// <summary>Rotated text sits left of its tick, or right of it where that would enter the label column.</summary>
    private static double RotatedSide(double x, double left, double h) => x - 1.25 * h < left ? 0.75 * h : -0.75 * h;

    private List<int> KeepColumns(SectionTableModel model, List<SectionTableRow> perOffset, Func<double, double> xOfOffset, double left, double right,
        double h, bool rotateText)
    {
        var kept = new List<int>();
        double lastHi = double.NegativeInfinity, lastTick = double.NegativeInfinity;
        for (var i = 0; i < model.Offsets.Count; i++)
        {
            var x = xOfOffset(model.Offsets[i]);
            if (x < left - 1e-6 || x > right + 1e-6)
            {
                SkippedColumns++;
                continue;
            }

            double lo, hi;
            if (rotateText)
            {
                var c = x + RotatedSide(x, left, h);
                lo = c - 0.5 * h;
                hi = c + 0.5 * h;
            }
            else
            {
                var width = perOffset.Select(r => ProfileTableLayout.TextWidth(Cell(r, i), h)).DefaultIfEmpty(0).Max();
                var half = width / 2 + 0.1 * h;
                var c = Math.Max(left + half, Math.Min(right - half, x));
                lo = c - half;
                hi = c + half;
            }

            if (kept.Count > 0 && lo < Math.Max(lastHi, lastTick) + 0.1 * h)
            {
                SkippedColumns++;
                continue;
            }

            kept.Add(i);
            KeptOffsets.Add(model.Offsets[i]);
            lastHi = hi;
            lastTick = rotateText ? x : double.NegativeInfinity;
        }

        return kept;
    }

    private static List<ProfileTableSpan> KeptGaps(SectionTableModel model, List<int> kept, int decimals)
    {
        var gaps = new List<ProfileTableSpan>();
        for (var k = 1; k < kept.Count; k++)
        {
            double from = model.Offsets[kept[k - 1]], to = model.Offsets[kept[k]];
            gaps.Add(new ProfileTableSpan(from, to, ProfileTableBuilder.F(to - from, decimals)));
        }

        return gaps;
    }

    private void PlaceSpanText(ProfileTableSpan span, double x1, double x2, double mid, double rowHeight, double h)
    {
        var text = span.Line1;
        if (text.Length == 0) return;
        var width = x2 - x1;
        var cx = (x1 + x2) / 2;
        var w = ProfileTableLayout.TextWidth(text, h);
        if (w + 0.2 * h <= width) Texts.Add(new ProfileTableText(cx, mid, text, 0, h));
        else if (w + 0.2 * h <= rowHeight && 1.2 * h <= width) Texts.Add(new ProfileTableText(cx, mid, text, Math.PI / 2, h));
        else SkippedTexts++;
    }

    private static string Cell(SectionTableRow row, int i) => i < row.Cells.Count ? row.Cells[i] : "";

    private static double Clamp(double x, double lo, double hi) => Math.Max(lo, Math.Min(hi, x));
}
