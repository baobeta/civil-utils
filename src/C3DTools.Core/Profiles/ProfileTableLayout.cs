using System;
using System.Collections.Generic;
using System.Linq;

namespace C3DTools.Core.Profiles;

public sealed class ProfileTableLine
{
    public ProfileTableLine(double x1, double y1, double x2, double y2)
    {
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }

    public double X1 { get; }
    public double Y1 { get; }
    public double X2 { get; }
    public double Y2 { get; }
}

/// <summary>A text centred (middle-centre) at X, Y; Rotation in radians.</summary>
public sealed class ProfileTableText
{
    public ProfileTableText(double x, double y, string text, double rotation, double height)
    {
        X = x;
        Y = y;
        Text = text;
        Rotation = rotation;
        Height = height;
    }

    public double X { get; }
    public double Y { get; }
    public string Text { get; }
    public double Rotation { get; }
    public double Height { get; }
}

/// <summary>
/// The table in drawing units: rows downward from top, the label column [left − labelWidth, left], stations at
/// xOfStation(station). Text width is estimated as 0.7·h per character (TextWidth).
/// Row heights: at least rowHeight; a per-station row with rotated text is as tall as its longest text + h, every
/// other row at least 2.6·h (room for two span lines at mid ± 0.65·h).
/// Per-station rows: a tick at each station and the text beside it (rotated 90° along the tick, left of it except at
/// the first station), or horizontal text centred on the station without ticks, kept inside the table. Span rows: one line at each distinct span edge, text centred in the span; a span too
/// narrow for its text gets it rotated when that fits the row, otherwise the text is left out (SkippedTexts).
/// </summary>
public sealed class ProfileTableLayout
{
    public const double CharWidth = 0.7;

    private ProfileTableLayout() { }

    public List<ProfileTableLine> Lines { get; } = new List<ProfileTableLine>();
    public List<ProfileTableText> Texts { get; } = new List<ProfileTableText>();

    /// <summary>Height of each model row, top to bottom.</summary>
    public List<double> RowHeights { get; } = new List<double>();

    public double Top { get; private set; }
    public double Bottom { get; private set; }

    /// <summary>Left edge of the label column.</summary>
    public double Left { get; private set; }

    public double Right { get; private set; }

    /// <summary>Span texts left out because the span is too narrow even for rotated text.</summary>
    public int SkippedTexts { get; private set; }

    /// <summary>Estimated width of a single-line text of this height.</summary>
    public static double TextWidth(string text, double height) => CharWidth * height * (text ?? "").Length;

    public static ProfileTableLayout Build(ProfileTableModel model, Func<double, double> xOfStation, double top, double left, double right,
        double rowHeight, double textHeight, double labelWidth, bool rotateStationText)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        if (xOfStation == null) throw new ArgumentNullException(nameof(xOfStation));
        if (!(rowHeight > 0) || !(textHeight > 0) || !(labelWidth >= 0)) throw new ArgumentOutOfRangeException(nameof(rowHeight));

        var h = textHeight;
        var layout = new ProfileTableLayout { Top = top, Left = left - labelWidth, Right = right };
        foreach (var row in model.Rows)
        {
            var needed = 2.6 * h;
            if (row.Kind == ProfileTableRowKind.PerStation && rotateStationText)
                needed = row.Cells.Select(c => TextWidth(c, h)).DefaultIfEmpty(0).Max() + h;
            layout.RowHeights.Add(Math.Max(rowHeight, needed));
        }

        layout.Bottom = top - layout.RowHeights.Sum();
        var xs = model.Stations.Select(s => xOfStation(s.Station)).ToList();

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

            if (row.Kind == ProfileTableRowKind.PerStation)
            {
                for (var i = 0; i < xs.Count && i < row.Cells.Count; i++)
                {
                    if (rotateStationText)
                    {
                        layout.Lines.Add(new ProfileTableLine(xs[i], rowTop, xs[i], rowBottom));
                        // Left of the tick; right of it where that would enter the label column (first station).
                        var side = xs[i] - 1.25 * h < left ? 0.75 * h : -0.75 * h;
                        if (row.Cells[i].Length > 0)
                            layout.Texts.Add(new ProfileTableText(xs[i] + side, mid, row.Cells[i], Math.PI / 2, h));
                    }
                    else if (row.Cells[i].Length > 0)
                    {
                        // Centred on the station, kept inside the table at both ends.
                        var half = TextWidth(row.Cells[i], h) / 2 + 0.1 * h;
                        var x = Math.Max(left + half, Math.Min(right - half, xs[i]));
                        layout.Texts.Add(new ProfileTableText(x, mid, row.Cells[i], 0, h));
                    }
                }
            }
            else
            {
                var edges = new HashSet<double>();
                foreach (var span in row.Spans)
                {
                    double x1 = xOfStation(span.From), x2 = xOfStation(span.To);
                    foreach (var x in new[] { x1, x2 })
                    {
                        if (edges.Add(Math.Round(x, 6))) layout.Lines.Add(new ProfileTableLine(x, rowTop, x, rowBottom));
                    }

                    layout.PlaceSpanText(span, Math.Min(x1, x2), Math.Max(x1, x2), mid, rh, h);
                }
            }

            rowTop = rowBottom;
        }

        return layout;
    }

    private void PlaceSpanText(ProfileTableSpan span, double x1, double x2, double mid, double rowHeight, double h)
    {
        var lines = new[] { span.Line1, span.Line2 }.Where(t => t.Length > 0).ToList();
        if (lines.Count == 0) return;
        var width = x2 - x1;
        var cx = (x1 + x2) / 2;
        var longest = lines.Max(t => TextWidth(t, h));
        // Line k of n is offset (k − (n − 1)/2)·1.3h from the centre: mid ± 0.65h for two lines.
        double Offset(int k) => (k - (lines.Count - 1) / 2.0) * 1.3 * h;

        if (longest + 0.2 * h <= width)
        {
            for (var k = 0; k < lines.Count; k++) Texts.Add(new ProfileTableText(cx, mid - Offset(k), lines[k], 0, h));
            return;
        }

        if (longest + 0.2 * h <= rowHeight && lines.Count * 1.3 * h - 0.3 * h + 0.2 * h <= width)
        {
            // Rotated 90°: the first line on the left, reading upwards.
            for (var k = 0; k < lines.Count; k++) Texts.Add(new ProfileTableText(cx + Offset(k), mid, lines[k], Math.PI / 2, h));
            return;
        }

        SkippedTexts += lines.Count;
    }
}
