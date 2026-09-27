using System.Linq;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class SuperelevationTests
{
    private static readonly PlanPoint[] RightTurn = { new PlanPoint(0, 0), new PlanPoint(300, 0), new PlanPoint(300, -300) };
    private static readonly PlanPoint[] LeftTurn = { new PlanPoint(0, 0), new PlanPoint(300, 0), new PlanPoint(300, 300) };

    private static DesignedCurve Curve(PlanPoint[] pis, CurveInput input) => RouteDesigner.Design(pis, 0, new[] { input }, 60, null).Curves[0];

    [Fact]
    public void Runoff_follows_the_spirals_and_rotates_the_outside_lane_first()
    {
        var c = Curve(RightTurn, new CurveInput { Radius = 100, SpiralIn = 40, SpiralOut = 40, Superelevated = true, SuperRate = 6 });

        var points = SuperelevationPlanner.Plan(c, 2);

        Assert.Equal(8, points.Count);
        Assert.Equal(c.StationStart, points[0].Station, 9);
        Assert.Equal(c.StationStart + 40 * 0.25, points[1].Station, 9);   // level crown at in / (in + isc) = 2/8
        Assert.Equal(c.StationStart + 40 * 0.5, points[2].Station, 9);    // reverse crown at 2in / (in + isc)
        Assert.Equal(c.StationArcStart, points[3].Station, 9);
        Assert.Equal(c.StationArcEnd, points[4].Station, 9);
        Assert.Equal(c.StationEnd, points[7].Station, 9);
        // Right turn: the left side is the outside.
        Assert.Equal((-2.0, -2.0), (points[0].LeftSlope, points[0].RightSlope));
        Assert.Equal((0.0, -2.0), (points[1].LeftSlope, points[1].RightSlope));
        Assert.Equal((2.0, -2.0), (points[2].LeftSlope, points[2].RightSlope));
        Assert.Equal((6.0, -6.0), (points[3].LeftSlope, points[3].RightSlope));
        Assert.True(points.Take(4).All(p => p.IsEntry));
    }

    [Fact]
    public void Left_turn_mirrors_the_slopes()
    {
        var c = Curve(LeftTurn, new CurveInput { Radius = 100, SpiralIn = 40, SpiralOut = 40, Superelevated = true, SuperRate = 6 });

        var full = SuperelevationPlanner.Plan(c, 2)[3];

        Assert.Equal((-6.0, 6.0), (full.LeftSlope, full.RightSlope));
    }

    [Fact]
    public void Without_a_spiral_the_runoff_uses_length_and_offset()
    {
        var c = Curve(RightTurn, new CurveInput { Radius = 200, Superelevated = true, SuperRate = 4, RunoffIn = 50, RunoffOut = 60, OffsetIn = 25, OffsetOut = 30 });

        var entry = SuperelevationPlanner.EntryRange(c);
        var exit = SuperelevationPlanner.ExitRange(c);

        Assert.Equal(c.StationArcStart - 25, entry.Begin, 9);
        Assert.Equal(c.StationArcStart + 25, entry.End, 9);
        Assert.Equal(c.StationArcEnd - 30, exit.Begin, 9);
        Assert.Equal(c.StationArcEnd + 30, exit.End, 9);
    }

    [Fact]
    public void Rate_below_the_crown_slope_is_raised_to_it_and_no_superelevation_gives_nothing()
    {
        var c = Curve(RightTurn, new CurveInput { Radius = 100, SpiralIn = 40, SpiralOut = 40, Superelevated = true, SuperRate = 1 });

        Assert.Equal(2, SuperelevationPlanner.Plan(c, 2)[3].LeftSlope);
        Assert.Empty(SuperelevationPlanner.Plan(Curve(RightTurn, new CurveInput { Radius = 100 }), 2));
    }

    [Fact]
    public void Widening_factor_ramps_on_the_runoffs()
    {
        var c = Curve(RightTurn, new CurveInput { Radius = 100, SpiralIn = 40, SpiralOut = 40, Wb = 1 });

        Assert.Equal(0, SuperelevationPlanner.WideningFactor(c, c.StationStart - 1));
        Assert.Equal(0.5, SuperelevationPlanner.WideningFactor(c, c.StationStart + 20), 9);
        Assert.Equal(1, SuperelevationPlanner.WideningFactor(c, c.StationArcMid));
        Assert.Equal(0.25, SuperelevationPlanner.WideningFactor(c, c.StationEnd - 10), 9);
    }

    [Fact]
    public void Check_warns_about_missing_overlapping_and_outside_runoffs()
    {
        var pis = new[] { new PlanPoint(0, 0), new PlanPoint(100, 0), new PlanPoint(100, -100), new PlanPoint(200, -100) };
        var inputs = new[]
        {
            new CurveInput { Radius = 30, Superelevated = true, RunoffIn = 80, RunoffOut = 30, OffsetIn = 80, OffsetOut = 30 },
            new CurveInput { Radius = 30, Wb = 0.5, RunoffIn = 40, RunoffOut = 0, OffsetIn = 20 },
        };
        var design = RouteDesigner.Design(pis, 0, inputs, 60, null);

        SuperelevationPlanner.Check(design.Curves, 0, design.EndStation);

        var first = design.Curves[0].Issues.Select(i => i.Message).ToList();
        Assert.Contains(first, m => m.StartsWith("Đ1: đoạn nối vượt ra ngoài"));
        Assert.Contains(first, m => m.StartsWith("Đ1: đoạn nối cuối chồng lên đoạn nối đầu của Đ2"));
        Assert.Contains(design.Curves[1].Issues, i => i.Message == "Đ2: chưa có chiều dài nối (siêu cao / mở rộng).");
        Assert.All(design.Curves.SelectMany(c => c.Issues), i => Assert.False(i.IsError));
        Assert.True(design.CanApply);
    }

    [Fact]
    public void Table_lists_points_in_station_order()
    {
        var c = Curve(RightTurn, new CurveInput { Radius = 100, SpiralIn = 40, SpiralOut = 40, Superelevated = true, SuperRate = 6 });

        var table = SuperelevationPlanner.Table(SuperelevationPlanner.Plan(c, 2), 2);

        Assert.Equal(8, table.Rows.Count);
        Assert.Equal(new[] { "Đ1", "Đầu đoạn nối", "Km0+" + (c.StationStart).ToString("000.00", System.Globalization.CultureInfo.InvariantCulture), "-2.00", "-2.00" }, table.Rows[0]);
        Assert.Equal("Bắt đầu siêu cao", table.Rows[3][1]);
    }

    [Fact]
    public void Per_curve_speed_is_used_for_the_TCVN_check()
    {
        var rules = new CurveRules();
        rules.MinRadius.Add(new SpeedRadiusRule { DesignSpeed = 60, MinRadius = 125, NormalRadius = 250 });
        rules.MinRadius.Add(new SpeedRadiusRule { DesignSpeed = 40, MinRadius = 60, NormalRadius = 125 });

        var at60 = RouteDesigner.Design(RightTurn, 0, new[] { new CurveInput { Radius = 100 } }, 60, rules).Curves[0];
        var at40 = RouteDesigner.Design(RightTurn, 0, new[] { new CurveInput { Radius = 100, DesignSpeed = 40 } }, 60, rules).Curves[0];

        Assert.Contains(at60.Issues, i => i.Code == CurveIssueCode.RadiusTooSmall);
        Assert.Contains(at40.Issues, i => i.Code == CurveIssueCode.RadiusBelowNormal);
    }
}
