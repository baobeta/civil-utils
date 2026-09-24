using System;
using System.IO;
using System.Linq;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class CurveTableBuilderTests
{
    private static PlanPoint P(double x, double y) => new PlanPoint(x, y);

    private static CurveInput In(double r, double l, double wb = 0, double wl = 0) =>
        new CurveInput { Radius = r, SpiralIn = l, SpiralOut = l, Wb = wb, Wl = wl };

    private static readonly PlanPoint[] Route = { P(0, 0), P(200, 0), P(200, 150), P(200, 300), P(350, 400) };

    private static RouteDesign Collinear() =>
        RouteDesigner.Design(Route, 250, new[] { In(120, 40, 0.5, 0.2), In(0, 0), In(250, 0) }, 60, null);

    [Fact]
    public void Two_curves_give_two_rows_with_two_decimals()
    {
        var t = CurveTableBuilder.Build(Collinear(), new CurveBoxOptions());

        Assert.Equal(new[] { "Đỉnh", "A", "R", "L1", "L2", "T1", "T2", "P", "K", "Wb", "Wl",
            "Lý trình NĐ", "Lý trình TĐ", "Lý trình P", "Lý trình TC", "Lý trình NC" }, t.Headers);
        Assert.Equal(2, t.Rows.Count);
        Assert.Equal(new[] { "Đ1", "90°00'00\"", "120.00", "40.00", "40.00", "140.54", "140.54", "50.49", "228.50",
            "0.50", "0.20", "0+309.46", "0+349.46", "0+423.71", "0+497.96", "0+537.96" }, t.Rows[0]);
        Assert.Equal("Đ2", t.Rows[1][0]);
        Assert.Equal("", t.Rows[1][11]);   // no spiral: no NĐ / NC
        Assert.Equal("0+563.63", t.Rows[1][12]);
        Assert.Equal("", t.Rows[1][15]);
    }

    [Fact]
    public void Ignores_vietnamese_culture()
    {
        TestCulture.Run("vi-VN", () =>
            Assert.Equal("140.54", CurveTableBuilder.Build(Collinear(), new CurveBoxOptions()).Rows[0][5]));
    }

    [Fact]
    public void Error_row_is_kept_with_empty_elements()
    {
        var d = RouteDesigner.Design(Route, 250, new[] { In(120, 400), In(0, 0), In(250, 0) }, 60, null);
        Assert.Null(d.Curves[0].Elements);

        var t = CurveTableBuilder.Build(d, new CurveBoxOptions());

        Assert.Equal(2, t.Rows.Count);
        var row = t.Rows[0];
        Assert.Equal("Đ1", row[0]);
        Assert.Equal("120.00", row[2]);
        Assert.All(new[] { 5, 6, 7, 8 }, i => Assert.Equal("", row[i]));
    }

    [Fact]
    public void Lisp_csv_layout_matches_the_golden_file_cell_by_cell()
    {
        var t = CurveTableBuilder.BuildLispCsv(Collinear());

        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Curves", "Golden", "route_with_collinear.csv"))
            .Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        Assert.Equal(lines[0].Split(','), t.Headers);
        Assert.Equal(lines.Length - 1, t.Rows.Count);
        for (var i = 1; i < lines.Length; i++)
            Assert.Equal(lines[i].Split(','), t.Rows[i - 1]);
    }
}
