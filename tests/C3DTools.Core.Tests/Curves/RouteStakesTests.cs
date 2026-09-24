using System;
using System.Linq;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class RouteStakesTests
{
    private static PlanPoint P(double x, double y) => new PlanPoint(x, y);

    private static readonly PlanPoint[] Square = { P(0, 0), P(100, 0), P(100, 100) };

    private static RouteDesign Design(PlanPoint[] pis, CurveInput input) => RouteDesigner.Design(pis, 0, new[] { input }, 60, null);

    [Fact]
    public void Circular_curve_gives_start_td_tc_p_and_end_stakes()
    {
        var stakes = RouteStakes.Build(Square, 0, Design(Square, new CurveInput { Radius = 50 }));

        Assert.Equal(new[] { "", "TĐ1", "TC1", "P1", "" }, stakes.Select(s => s.Name));
        var td = stakes[1];
        Assert.Equal(50, td.Station, 6);
        Assert.Equal(50, td.Point.X, 6);
        Assert.Equal(0, td.Point.Y, 6);
        Assert.Equal(1, td.Direction.X, 6);
        Assert.Equal(-1, td.Side);   // left turn: tick away from the centre, to the right
        Assert.Equal(1, stakes[3].Side);   // P stake points towards the centre
        Assert.Equal(stakes[4].Station, 50 + 50 * Math.PI / 2 + 50, 6);
    }

    [Fact]
    public void Spirals_add_nd_and_nc_with_rotated_directions_at_td_and_tc()
    {
        var stakes = RouteStakes.Build(Square, 0, Design(Square, new CurveInput { Radius = 50, SpiralIn = 20, SpiralOut = 20 }));

        Assert.Equal(new[] { "", "NĐ1", "TĐ1", "TC1", "NC1", "P1", "" }, stakes.Select(s => s.Name));
        var td = stakes[2];
        var beta = 20.0 / (2 * 50);
        Assert.Equal(Math.Cos(beta), td.Direction.X, 9);   // left turn rotates the tangent counter-clockwise
        Assert.Equal(Math.Sin(beta), td.Direction.Y, 9);
    }

    [Fact]
    public void Layout_follows_ytc_coc()
    {
        var d = RouteStakes.Layout(new Stake { Kind = StakeKind.Start, Point = P(0, 0), Direction = P(1, 0), Side = 1, Station = 131.09 }, 2);

        Assert.Equal(0, d.TickStart.X, 9);
        Assert.Equal(-2, d.TickStart.Y, 9);
        Assert.Equal(20, d.TickEnd.Y, 9);
        Assert.Equal(1.8, d.StationTextPoint.X, 9);
        Assert.Equal(11, d.StationTextPoint.Y, 9);
        Assert.Equal(-1.8, d.NameTextPoint.X, 9);
        Assert.Equal(Math.PI / 2, d.Rotation, 9);
        Assert.Equal("0+131.09", d.StationText);
        Assert.Equal("", d.NameText);
    }

    [Fact]
    public void Layout_keeps_text_readable_when_the_tick_points_down()
    {
        var d = RouteStakes.Layout(new Stake { Point = P(0, 0), Direction = P(1, 0), Side = -1 }, 1);
        Assert.Equal(-10, d.TickEnd.Y, 9);
        Assert.Equal(Math.PI / 2, d.Rotation, 9);
    }
}
