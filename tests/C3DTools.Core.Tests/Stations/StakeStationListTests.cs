using System;
using System.Linq;
using C3DTools.Core.Stations;
using Xunit;

namespace C3DTools.Core.Tests.Stations;

public class StakeStationListTests
{
    private static StakeStation Curve(double station, string name) => new StakeStation(station, name, StakeOrigin.Curve);

    [Fact]
    public void Merges_interval_curve_and_extra_stakes_in_station_order()
    {
        var list = StakeStationList.Build(0, 100, 20,
            new[] { Curve(50, "TC1"), Curve(30, "TĐ1"), Curve(40.0005, "P1") },
            new[] { 45.0, 60.0004, 200.0 });

        Assert.Equal(new[] { "Km0", "C1", "TĐ1", "P1", "C2", "TC1", "C3", "C4", "H1" }, list.Select(s => s.Name));
        Assert.Equal(new[] { 0, 20, 30, 40.0005, 45, 50, 60, 80, 100 }, list.Select(s => s.Station));
        Assert.Equal(StakeOrigin.Extra, list[4].Origin);
        Assert.Equal(StakeOrigin.Curve, list[3].Origin);
        Assert.Equal(StakeOrigin.Interval, list[0].Origin);
    }

    [Fact]
    public void Named_stake_wins_within_one_millimetre()
    {
        var list = StakeStationList.Build(0, 100, 20, new[] { Curve(40.0009, "TĐ1") }, new[] { 50.0, 50.0008, 40.0 });

        Assert.Equal(new[] { 0, 20, 40.0009, 50, 60, 80, 100 }, list.Select(s => s.Station));
        Assert.Equal("TĐ1", list[2].Name);
    }

    [Fact]
    public void Stakes_just_over_one_millimetre_apart_are_kept()
    {
        var list = StakeStationList.Build(0, 100, 20, new[] { Curve(40.0015, "TĐ1") }, null);

        Assert.Equal(new[] { "Km0", "C1", "C2", "TĐ1", "C3", "C4", "H1" }, list.Select(s => s.Name));
    }

    [Fact]
    public void Coincident_curve_stakes_share_one_row()
    {
        var list = StakeStationList.Build(0, 100, 50, new[] { Curve(60, "TC1"), Curve(60.0002, "NĐ2") }, null);

        Assert.Equal(new[] { "Km0", "C1", "TC1/NĐ2", "H1" }, list.Select(s => s.Name));
    }

    [Fact]
    public void Km_and_hundred_names_follow_the_station_planner()
    {
        var list = StakeStationList.Build(950, 1250, 50, null, null);

        Assert.Equal(new[] { "C1", "Km1", "C2", "H1", "C3", "H2", "C4" }, list.Select(s => s.Name));
        Assert.Equal(new[] { 950, 1000, 1050, 1100, 1150, 1200, 1250.0 }, list.Select(s => s.Station));
    }

    [Fact]
    public void Curve_stakes_outside_the_route_are_dropped()
    {
        var list = StakeStationList.Build(0, 40, 20, new[] { Curve(-5, "TĐ0"), Curve(45, "TC9") }, null);

        Assert.Equal(new[] { "Km0", "C1", "C2" }, list.Select(s => s.Name));
    }

    [Fact]
    public void Rejects_a_bad_interval_or_range()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StakeStationList.Build(0, 100, 0, null, null));
        Assert.Throws<ArgumentException>(() => StakeStationList.Build(100, 100, 20, null, null));
    }
}
