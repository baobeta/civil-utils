using System;
using System.Collections.Generic;
using System.Globalization;
using C3DTools.Core.Stations;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Profiles;

/// <summary>
/// The lines of the vertical curve box above a PVI: "i1=+3.00%  i2=-2.00%", "R=2000  K=100", "T=50  E=0.63",
/// "CĐ đỉnh=12.35" and, when the grades change sign, "Điểm cao=1+010.00  CĐ=11.75" (Điểm thấp on a sag).
/// Numbers trimmed to 2 decimals; grades in percent with 2 decimals and a sign.
/// Without a curve only the grades and CĐ đỉnh; an asymmetric parabola shows T1 and T2.
/// </summary>
public static class VerticalCurveBoxText
{
    public static IReadOnlyList<string> Build(VerticalCurve c)
    {
        if (c == null) throw new ArgumentNullException(nameof(c));
        var lines = new List<string> { "i1=" + Percent(c.GradeIn) + "  i2=" + Percent(c.GradeOut) };
        var e = c.Elements;
        if (e != null)
        {
            lines.Add("R=" + N(e.R) + "  K=" + N(e.K));
            lines.Add(e.IsAsymmetric
                ? "T1=" + N(e.T1) + "  T2=" + N(e.T2) + "  E=" + N(e.E)
                : "T=" + N(e.T1) + "  E=" + N(e.E));
        }

        lines.Add("CĐ đỉnh=" + N(c.PviElevation));
        if (c.HighLowStation.HasValue && c.HighLowElevation.HasValue)
            lines.Add(HighLowName(c) + "=" + StationFormatter.Format(c.HighLowStation.Value, 2, withKmPrefix: false) + "  CĐ=" + N(c.HighLowElevation.Value));
        return lines;
    }

    /// <summary>"Điểm cao" on a crest, "Điểm thấp" on a sag.</summary>
    public static string HighLowName(VerticalCurve c) => c.IsCrest ? "Điểm cao" : "Điểm thấp";

    /// <summary>A fraction as percent with 2 decimals and a sign: 0.03 → "+3.00%", −0.02 → "-2.00%", 0 → "0.00%".</summary>
    public static string Percent(double grade)
    {
        var rounded = Math.Round(grade * 100, 2, MidpointRounding.AwayFromZero);
        if (rounded == 0) return "0.00%";
        return (rounded > 0 ? "+" : "") + rounded.ToString("F2", CultureInfo.InvariantCulture) + "%";
    }

    private static string N(double v) => NumberFormat.Trimmed(v, 2);
}
