using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

/// <summary>Checks RouteDesigner's stationing against CSVs produced by YTC.lsp (see Golden/generate_from_lisp.py).</summary>
public class LispGoldenRouteTests
{
    private static PlanPoint P(double x, double y) => new PlanPoint(x, y);

    private static CurveInput In(double r, double l, double wb = 0, double wl = 0) =>
        new CurveInput { Radius = r, SpiralIn = l, SpiralOut = l, Wb = wb, Wl = wl };

    [Fact]
    public void Route_with_collinear_pi_matches_lisp()
    {
        var pis = new[] { P(0, 0), P(200, 0), P(200, 150), P(200, 300), P(350, 400) };
        var inputs = new[] { In(120, 40, 0.5, 0.2), In(0, 0), In(250, 0) };   // middle PI is collinear, its input is ignored

        var d = RouteDesigner.Design(pis, 250, inputs, 60, null);

        AssertMatches("route_with_collinear.csv", d);
        Near(855.81, d.EndStation, "end station");
    }

    [Fact]
    public void Symmetric_scs_matches_lisp()
    {
        var a = 60 * Math.PI / 180;
        var pis = new[] { P(0, 0), P(300, 0), P(300 + 300 * Math.Cos(a), 300 * Math.Sin(a)) };

        var d = RouteDesigner.Design(pis, 1000, new[] { In(200, 50, 0.6, 0.3) }, 60, null);

        AssertMatches("symmetric_scs.csv", d);
    }

    private static void AssertMatches(string fileName, RouteDesign d)
    {
        var rows = ReadRows(fileName);
        Assert.Equal(rows.Count, d.Curves.Count);
        Assert.True(d.CanApply);

        for (var i = 0; i < rows.Count; i++)
        {
            var cols = rows[i];
            var c = d.Curves[i];
            var row = $"{fileName} {cols[0]}";

            Assert.Equal("Đ" + c.Number.ToString(CultureInfo.InvariantCulture), cols[0]);
            Near(Num(cols[4]), c.Elements.T1, row + " T");
            Near(Num(cols[5]), c.Elements.P, row + " P");
            Near(Num(cols[6]), c.Elements.K, row + " K");
            Near(Station(cols[9]), c.StationStart, row + " NĐ");
            Near(Station(cols[10]), c.StationArcStart, row + " TĐ");
            Near(Station(cols[11]), c.StationArcMid, row + " P station");
            Near(Station(cols[12]), c.StationArcEnd, row + " TC");
            Near(Station(cols[13]), c.StationEnd, row + " NC");
        }
    }

    private static List<string[]> ReadRows(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Curves", "Golden", fileName);
        var rows = new List<string[]>();
        var lines = File.ReadAllLines(path);
        for (var i = 1; i < lines.Length; i++)
            if (!string.IsNullOrWhiteSpace(lines[i])) rows.Add(lines[i].Split(','));
        return rows;
    }

    private static void Near(double expected, double actual, string what) =>
        Assert.True(Math.Abs(actual - expected) <= 0.01, $"{what}: {actual} vs {expected}");

    private static double Num(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>Parses the LISP's station text, e.g. "0+309.46" → 309.46, "1+159.24" → 1159.24.</summary>
    private static double Station(string text)
    {
        var parts = text.Split('+');
        return Num(parts[0]) * 1000 + Num(parts[1]);
    }
}
