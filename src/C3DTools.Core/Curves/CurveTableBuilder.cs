using System;
using System.Globalization;
using C3DTools.Core.Stations;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Curves;

/// <summary>Curve summary table (AutoCAD Table / CSV), plus the exact CSV layout written by YTC.lsp.</summary>
public static class CurveTableBuilder
{
    public static TableData Build(RouteDesign d, CurveBoxOptions o)
    {
        if (d == null) throw new ArgumentNullException(nameof(d));
        if (o == null) throw new ArgumentNullException(nameof(o));

        var table = new TableData("Đỉnh", "A", "R", "L1", "L2", "T1", "T2", "P", "K", "Wb", "Wl",
            "Lý trình NĐ", "Lý trình TĐ", "Lý trình P", "Lý trình TC", "Lý trình NC");
        foreach (var c in d.Curves)
        {
            var input = c.Input ?? new CurveInput();
            var e = c.Elements;
            string N(double value) => NumberFormat.Fixed(value, 2);
            string E(Func<CurveElements, double> pick) => e == null ? "" : N(pick(e));
            string S(double station) => StationFormatter.Format(station, 2, withKmPrefix: false);

            table.AddRow(Name(c), AngleFormatter.Dms(Degrees(c), o.AngleSecondDecimals),
                N(input.Radius), N(input.SpiralIn), N(input.SpiralOut),
                E(x => x.T1), E(x => x.T2), E(x => x.P), E(x => x.K), N(input.Wb), N(input.Wl),
                input.SpiralIn > 0 ? S(c.StationStart) : "", S(c.StationArcStart), S(c.StationArcMid),
                S(c.StationArcEnd), input.SpiralOut > 0 ? S(c.StationEnd) : "");
        }

        return table;
    }

    /// <summary>ytc:csv: ASCII headers, one L and one T (L1, T1), angle as 90d00'00, numbers trimmed, stations always filled.</summary>
    public static TableData BuildLispCsv(RouteDesign d)
    {
        if (d == null) throw new ArgumentNullException(nameof(d));

        var table = new TableData("Dinh", "A", "R", "L", "T", "P", "K", "Wb", "Wl",
            "Ly trinh ND", "Ly trinh TD", "Ly trinh P", "Ly trinh TC", "Ly trinh NC");
        foreach (var c in d.Curves)
        {
            var input = c.Input ?? new CurveInput();
            var e = c.Elements;
            string N(double value) => NumberFormat.Trimmed(value, 2);
            string E(Func<CurveElements, double> pick) => e == null ? "" : N(pick(e));
            string S(double station) => StationFormatter.Format(station, 2, withKmPrefix: false);

            table.AddRow(Name(c), AngleFormatter.Dms(Degrees(c)).Replace('°', 'd').TrimEnd('"'),
                N(input.Radius), N(input.SpiralIn), E(x => x.T1), E(x => x.P), E(x => x.K), N(input.Wb), N(input.Wl),
                S(c.StationStart), S(c.StationArcStart), S(c.StationArcMid), S(c.StationArcEnd), S(c.StationEnd));
        }

        return table;
    }

    private static string Name(DesignedCurve c) => "Đ" + c.Number.ToString(CultureInfo.InvariantCulture);

    private static double Degrees(DesignedCurve c) => c.DeltaRadians * 180 / Math.PI;
}
