using System;
using C3DTools.Core.Curves;
using C3DTools.Core.Stations;
using Xunit;

namespace C3DTools.Core.Tests.Stations;

public class StakeLabelLayoutTests
{
    private static readonly PlanPoint East = new PlanPoint(1, 0);
    private static readonly PlanPoint Left = new PlanPoint(100, 60);     // route heading east: left is +Y
    private static readonly PlanPoint Right = new PlanPoint(100, -60);

    [Fact]
    public void Name_goes_beyond_the_left_end_and_station_beyond_the_right_end()
    {
        var l = StakeLabelLayout.AtEnds(0, Left, Right, East, 100, "H1", 2, new StakeLabelOptions());

        Assert.Equal("H1", l.NameText);
        Assert.Equal(100, l.NamePoint.X, 9);
        Assert.Equal(60 + 1.5 * 2, l.NamePoint.Y, 9);   // gap of one text height, text middle-centred
        Assert.Equal("0+100.00", l.StationText);
        Assert.Equal(100, l.StationPoint.X, 9);
        Assert.Equal(-60 - 1.5 * 2, l.StationPoint.Y, 9);
        Assert.Equal(0, l.Rotation, 9);   // written along the route
    }

    [Fact]
    public void The_order_of_the_ends_does_not_matter()
    {
        var a = StakeLabelLayout.AtEnds(0, Left, Right, East, 100, "H1", 2, null);
        var b = StakeLabelLayout.AtEnds(0, Right, Left, East, 100, "H1", 2, null);

        Assert.Equal(a.NamePoint.Y, b.NamePoint.Y, 9);
        Assert.Equal(a.StationPoint.Y, b.StationPoint.Y, 9);
    }

    [Fact]
    public void Heading_west_the_left_is_south_and_the_text_is_not_upside_down()
    {
        var l = StakeLabelLayout.AtEnds(0, Left, Right, new PlanPoint(-1, 0), 100, "H1", 2, null);

        Assert.True(l.NamePoint.Y < 0);
        Assert.True(l.StationPoint.Y > 0);
        Assert.Equal(0, l.Rotation, 9);
    }

    [Fact]
    public void Text_follows_a_sloping_route()
    {
        var angle = 20 * Math.PI / 180;
        var direction = new PlanPoint(Math.Cos(angle), Math.Sin(angle));
        var left = new PlanPoint(-Math.Sin(angle) * 60, Math.Cos(angle) * 60);
        var right = new PlanPoint(Math.Sin(angle) * 60, -Math.Cos(angle) * 60);

        var l = StakeLabelLayout.AtEnds(0, left, right, direction, 0, "Km0", 2.5, null);

        Assert.Equal(angle, l.Rotation, 9);
        Assert.Equal(60 + 1.5 * 2.5, Math.Sqrt(l.NamePoint.X * l.NamePoint.X + l.NamePoint.Y * l.NamePoint.Y), 9);
        Assert.Equal("0+000.00", l.StationText);
    }

    [Fact]
    public void Alternating_swaps_the_ends_of_every_second_stake()
    {
        var options = new StakeLabelOptions { AlternateSides = true };

        var first = StakeLabelLayout.AtEnds(0, Left, Right, East, 100, "H1", 2, options);
        var second = StakeLabelLayout.AtEnds(1, Left, Right, East, 120, "C5", 2, options);

        Assert.True(first.NamePoint.Y > 0 && first.StationPoint.Y < 0);
        Assert.True(second.NamePoint.Y < 0 && second.StationPoint.Y > 0);
    }

    [Fact]
    public void Station_can_be_left_out_and_follows_the_decimals()
    {
        Assert.Equal("", StakeLabelLayout.AtEnds(0, Left, Right, East, 100, "H1", 2, new StakeLabelOptions { WithStation = false }).StationText);
        Assert.Equal("1+234.6", StakeLabelLayout.AtEnds(0, Left, Right, East, 1234.56, "X", 2, new StakeLabelOptions { StationDecimals = 1 }).StationText);
    }

    [Fact]
    public void A_line_without_length_or_a_bad_height_throws()
    {
        Assert.Throws<ArgumentException>(() => StakeLabelLayout.AtEnds(0, Left, Left, East, 0, "X", 2, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => StakeLabelLayout.AtEnds(0, Left, Right, East, 0, "X", 0, null));
    }
}
