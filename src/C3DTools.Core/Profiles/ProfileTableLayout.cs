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
/// xOfStation(station). Per-station rows: a tick at each station and the text beside it (rotated 90° along the tick,
/// or horizontal and centred on the station). Span rows: a line at each span end, text centred in the span
/// (two lines at ⅓ and ⅔ of the row).
/// </summary>
public sealed class ProfileTableLayout
{
    private ProfileTableLayout() { }

    public List<ProfileTableLine> Lines { get; } = new List<ProfileTableLine>();
    public List<ProfileTableText> Texts { get; } = new List<ProfileTableText>();
    public double Top { get; private set; }
    public double Bottom { get; private set; }

    /// <summary>Left edge of the label column.</summary>
    public double Left { get; private set; }

    public double Right { get; private set; }

    public static ProfileTableLayout Build(ProfileTableModel model, Func<double, double> xOfStation, double top, double left, double right,
        double rowHeight, double textHeight, double labelWidth, bool rotateStationText)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        if (xOfStation == null) throw new ArgumentNullException(nameof(xOfStation));
        if (!(rowHeight > 0) || !(textHeight > 0) || !(labelWidth >= 0)) throw new ArgumentOutOfRangeException(nameof(rowHeight));

        var layout = new ProfileTableLayout
        {
            Top = top,
            Bottom = top - model.Rows.Count * rowHeight,
            Left = left - labelWidth,
            Right = right,
        };
        var xs = model.Stations.Select(s => xOfStation(s.Station)).ToList();

        for (var r = 0; r <= model.Rows.Count; r++)
            layout.Lines.Add(new ProfileTableLine(layout.Left, top - r * rowHeight, right, top - r * rowHeight));
        foreach (var x in new[] { layout.Left, left, right })
            layout.Lines.Add(new ProfileTableLine(x, top, x, layout.Bottom));

        for (var r = 0; r < model.Rows.Count; r++)
        {
            var row = model.Rows[r];
            var rowTop = top - r * rowHeight;
            var rowBottom = rowTop - rowHeight;
            var mid = rowTop - rowHeight / 2;
            layout.Texts.Add(new ProfileTableText(left - labelWidth / 2, mid, row.Label, 0, textHeight));

            if (row.Kind == ProfileTableRowKind.PerStation)
            {
                for (var i = 0; i < xs.Count && i < row.Cells.Count; i++)
                {
                    if (rotateStationText)
                    {
                        layout.Lines.Add(new ProfileTableLine(xs[i], rowTop, xs[i], rowBottom));
                        if (row.Cells[i].Length > 0)
                            layout.Texts.Add(new ProfileTableText(xs[i] - 0.75 * textHeight, mid, row.Cells[i], Math.PI / 2, textHeight));
                    }
                    else if (row.Cells[i].Length > 0)
                    {
                        layout.Texts.Add(new ProfileTableText(xs[i], mid, row.Cells[i], 0, textHeight));
                    }
                }

                continue;
            }

            foreach (var span in row.Spans)
            {
                double x1 = xOfStation(span.From), x2 = xOfStation(span.To);
                layout.Lines.Add(new ProfileTableLine(x1, rowTop, x1, rowBottom));
                layout.Lines.Add(new ProfileTableLine(x2, rowTop, x2, rowBottom));
                var cx = (x1 + x2) / 2;
                if (span.Line2.Length == 0)
                {
                    layout.Texts.Add(new ProfileTableText(cx, mid, span.Line1, 0, textHeight));
                }
                else
                {
                    layout.Texts.Add(new ProfileTableText(cx, rowTop - rowHeight / 3, span.Line1, 0, textHeight));
                    layout.Texts.Add(new ProfileTableText(cx, rowTop - 2 * rowHeight / 3, span.Line2, 0, textHeight));
                }
            }
        }

        return layout;
    }
}
