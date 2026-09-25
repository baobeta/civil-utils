using System;
using System.Collections.Generic;
using C3DTools.Core.Stations;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Profiles;

/// <summary>The CTCONGDUNG table (dialog grid, AutoCAD Table, CSV, Excel): one row per PVI. "Điểm cao/thấp" = "station / elevation".</summary>
public static class VerticalCurveTableBuilder
{
    public static readonly string[] Headers =
    {
        "Đỉnh", "Lý trình", "CĐ đỉnh", "i1 (%)", "i2 (%)", "A (%)", "R", "K", "T", "E", "Lý trình TĐ", "Lý trình TC", "Điểm cao/thấp", "Cảnh báo",
    };

    /// <param name="warning">The warning text of a PVI (null = none).</param>
    public static TableData Build(IEnumerable<VerticalCurve> curves, Func<VerticalCurve, string> warning, int stationDecimals = 2)
    {
        if (curves == null) throw new ArgumentNullException(nameof(curves));
        var table = new TableData((string[])Headers.Clone());
        foreach (var c in curves) table.AddRow(Row(c, warning?.Invoke(c), stationDecimals));
        return table;
    }

    public static string[] Row(VerticalCurve c, string warning, int stationDecimals = 2)
    {
        if (c == null) throw new ArgumentNullException(nameof(c));
        // Half away from zero, as the box (E 0.625 → 0.63 in both).
        string N(double v) => NumberFormat.Fixed(Math.Round(v, 2, MidpointRounding.AwayFromZero), 2);
        string S(double station) => StationFormatter.Format(station, stationDecimals, withKmPrefix: false);
        string G(double grade) => VerticalCurveBoxText.Percent(grade).TrimEnd('%');
        var e = c.Elements;
        var t = e == null ? "" : e.IsAsymmetric ? N(e.T1) + "/" + N(e.T2) : N(e.T1);
        return new[]
        {
            c.Name, S(c.PviStation), N(c.PviElevation), G(c.GradeIn), G(c.GradeOut), N(c.A),
            e == null ? "" : N(e.R), e == null ? "" : N(e.K), t, e == null ? "" : N(e.E),
            e == null ? "" : S(c.StartStation), e == null ? "" : S(c.EndStation),
            c.HighLowStation.HasValue && c.HighLowElevation.HasValue ? S(c.HighLowStation.Value) + " / " + N(c.HighLowElevation.Value) : "",
            warning ?? "",
        };
    }
}
