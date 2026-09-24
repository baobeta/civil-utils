using System;
using System.Linq;
using C3DTools.Core.Stations;
using Xunit;

namespace C3DTools.Core.Tests.Stations;

public class StationPlannerTests
{
    [Fact]
    public void Builds_regular_stations_including_start_and_end()
    {
        var plan = StationPlanner.Build(0, 250, 50, null);

        Assert.Equal(new[] { 0.0, 50, 100, 150, 200, 250 }, plan.Select(p => p.Station));
        Assert.Equal(new[] { "Km0", "C1", "H1", "C2", "H2", "C3" }, plan.Select(p => p.Name));
        Assert.All(plan, p => Assert.False(p.IsSpecial));
    }

    [Fact]
    public void Adds_end_when_not_on_interval()
    {
        var plan = StationPlanner.Build(0, 120, 50, null);

        Assert.Equal(new[] { 0.0, 50, 100, 120 }, plan.Select(p => p.Station));
    }

    [Fact]
    public void Merges_special_stations_and_replaces_nearby_regular_ones()
    {
        var specials = new[]
        {
            new SpecialStation(120.5, "TĐ1"),
            new SpecialStation(150.004, "P1"),
        };

        var plan = StationPlanner.Build(0, 250, 50, specials);

        Assert.Equal(new[] { "Km0", "C1", "H1", "TĐ1", "P1", "H2", "C2" }, plan.Select(p => p.Name));
        Assert.True(plan.Single(p => p.Name == "P1").IsSpecial);
    }

    [Fact]
    public void Names_kilometre_and_hundreds_after_first_km()
    {
        var plan = StationPlanner.Build(1000, 1100, 100, null);

        Assert.Equal(new[] { "Km1", "H1" }, plan.Select(p => p.Name));
    }

    [Fact]
    public void Rejects_special_station_outside_range()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            StationPlanner.Build(0, 100, 50, new[] { new SpecialStation(150, "TC1") }));
        Assert.Contains("TC1", ex.Message);
    }

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(0, 100, -5)]
    public void Rejects_non_positive_interval(double start, double end, double interval)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StationPlanner.Build(start, end, interval, null));
    }

    [Fact]
    public void Rejects_end_not_after_start()
    {
        Assert.Throws<ArgumentException>(() => StationPlanner.Build(100, 100, 20, null));
    }
}
