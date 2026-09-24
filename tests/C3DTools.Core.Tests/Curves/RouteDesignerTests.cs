using System;
using System.Collections.Generic;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class RouteDesignerTests
{
    private static PlanPoint P(double x, double y) => new PlanPoint(x, y);
    private static CurveInput R(double r, double l1 = 0, double l2 = 0) =>
        new CurveInput { Radius = r, SpiralIn = l1, SpiralOut = l2 };

    private static readonly PlanPoint[] Square = { P(0, 0), P(100, 0), P(100, 100) };

    [Fact]
    public void Circular_curve_on_left_turn()
    {
        var d = RouteDesigner.Design(Square, 0, new[] { R(50) }, 60, null);

        var c = Assert.Single(d.Curves);
        Assert.Equal(1, c.Number);
        Assert.Equal(-1, c.Turn);
        Assert.Equal(50, c.TangentBefore, 4);
        Assert.Equal(50, c.StationStart, 4);            // TĐ
        Assert.Equal(128.5398, c.StationArcEnd, 4);     // TC
        Assert.Equal(178.5398, d.EndStation, 4);
        Assert.True(d.CanApply);
    }

    [Fact]
    public void Asymmetric_spirals_give_nd_td_tc_nc()
    {
        var a = Math.PI / 4;
        var pis = new[] { P(0, 0), P(500, 0), P(500 + 500 * Math.Cos(a), 500 * Math.Sin(a)) };

        var d = RouteDesigner.Design(pis, 0, new[] { R(300, 60, 40) }, 60, null);

        var c = Assert.Single(d.Curves);
        Assert.Equal(345.9315, c.StationStart, 4);      // NĐ
        Assert.Equal(405.9315, c.StationArcStart, 4);   // TĐ
        Assert.Equal(591.5510, c.StationArcEnd, 4);     // TC
        Assert.Equal(631.5510, c.StationEnd, 4);        // NC
        Assert.Equal(986.8052, d.EndStation, 4);
    }

    [Fact]
    public void Start_station_shifts_everything()
    {
        var d = RouteDesigner.Design(Square, 1000, new[] { R(50) }, 60, null);

        Assert.Equal(1050, d.Curves[0].StationStart, 4);
    }

    [Fact]
    public void Collinear_pi_has_no_curve_and_no_number()
    {
        var pis = new[] { P(0, 0), P(50, 0), P(100, 0), P(100, 100) };

        var d = RouteDesigner.Design(pis, 0, new[] { R(50), R(50) }, 60, null);

        var c = Assert.Single(d.Curves);
        Assert.Equal(1, c.Number);
        Assert.Equal(2, c.PiIndex);
        Assert.Equal(50, c.StationStart, 4);
    }

    [Fact]
    public void Overlap_with_route_start_is_an_error()
    {
        var d = RouteDesigner.Design(Square, 0, new[] { R(150) }, 60, null);

        Assert.Contains(d.Curves[0].Issues, i => i.Code == CurveIssueCode.Overlap && i.IsError);
        Assert.False(d.CanApply);
    }

    [Fact]
    public void Spiral_too_long_for_deflection_is_an_error_not_an_exception()
    {
        var d = RouteDesigner.Design(Square, 0, new[] { R(100, 200, 200) }, 60, null);

        Assert.Contains(d.Curves[0].Issues, i => i.Code == CurveIssueCode.SpiralTooLong);
        Assert.Null(d.Curves[0].Elements);
        Assert.False(d.CanApply);
    }

    [Fact]
    public void Zero_radius_is_an_error()
    {
        var d = RouteDesigner.Design(Square, 0, new[] { R(0) }, 60, null);

        Assert.Contains(d.Curves[0].Issues, i => i.Code == CurveIssueCode.InvalidInput);
    }

    [Fact]
    public void Rule_warnings_do_not_block_apply()
    {
        var rules = new CurveRules
        {
            MinRadius = new List<SpeedRadiusRule> { new SpeedRadiusRule { DesignSpeed = 60, MinRadius = 125, NormalRadius = 250 } },
        };

        var d = RouteDesigner.Design(Square, 0, new[] { R(50) }, 60, rules);

        Assert.Contains(d.Curves[0].Issues, i => i.Code == CurveIssueCode.RadiusTooSmall && !i.IsError);
        Assert.True(d.CanApply);
    }

    [Fact]
    public void Input_count_must_match_interior_pis()
    {
        Assert.Throws<ArgumentException>(() => RouteDesigner.Design(Square, 0, new CurveInput[0], 60, null));
    }

    [Fact]
    public void Removes_consecutive_duplicate_points()
    {
        var clean = RouteDesigner.RemoveDuplicatePoints(new[] { P(0, 0), P(0, 0), P(10, 0), P(10, 1e-9) });

        Assert.Equal(2, clean.Count);
    }
}
