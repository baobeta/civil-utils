using System;
using System.Linq;
using C3DTools.Core.Curves;
using C3DTools.Core.Stations;
using Xunit;

namespace C3DTools.Core.Tests.Stations;

public class StakePlannerTests
{
    private static RouteStake Key(double s, StakeKind kind, int curve) => new RouteStake(s, StakeRole.CurveKey, "", kind, curve);

    [Fact]
    public void Straight_route_gets_round_stations_H_and_Km_and_both_ends()
    {
        var stakes = StakePlanner.Generate(0, 250, 20, 10, null, null);

        Assert.Equal(new[] { 0.0, 20, 40, 60, 80, 100, 120, 140, 160, 180, 200, 220, 240, 250 }, stakes.Select(s => s.Station));
        Assert.Equal(StakeRole.Km, stakes[0].Role);
        Assert.Equal(StakeRole.Hundred, stakes[5].Role);
        Assert.Equal(StakeRole.Detail, stakes[13].Role);
        Assert.All(stakes, s => Assert.Equal("", s.Name));
    }

    [Fact]
    public void Curve_zone_uses_the_curve_spacing_and_keeps_its_key_stakes()
    {
        var keys = new[] { Key(113.4, StakeKind.Td, 1), Key(140.2, StakeKind.P, 1), Key(167, StakeKind.Tc, 1) };

        var stakes = StakePlanner.Generate(0, 200, 20, 10, new[] { new StationZone(113.4, 167) }, keys);

        Assert.Equal(new[] { 0.0, 20, 40, 60, 80, 100, 113.4, 120, 130, 140, 140.2, 150, 160, 167, 180, 200 },
            stakes.Select(s => Math.Round(s.Station, 3)));
        Assert.Equal(StakeRole.CurveKey, stakes[6].Role);
        Assert.Equal(StakeKind.P, stakes[10].CurveKind);
    }

    [Fact]
    public void A_key_stake_on_a_round_station_wins_over_H()
    {
        var stakes = StakePlanner.Generate(0, 200, 20, 10, null, new[] { Key(100.0004, StakeKind.Nd, 2) });

        var at100 = stakes.Single(s => Math.Abs(s.Station - 100) < 0.01);
        Assert.Equal(StakeRole.CurveKey, at100.Role);
        Assert.Equal(2, at100.CurveNumber);
    }

    [Fact]
    public void Range_limits_stakes_and_keys_outside_are_dropped()
    {
        var stakes = StakePlanner.Generate(35, 95, 20, 10, null, new[] { Key(10, StakeKind.Td, 1), Key(50, StakeKind.P, 1) });

        Assert.Equal(new[] { 35.0, 40, 50, 60, 80, 95 }, stakes.Select(s => s.Station));
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(10, 0, 10)]
    [InlineData(10, 20, -5)]
    public void Invalid_input_throws(double straight, double curve, double to)
    {
        Assert.ThrowsAny<ArgumentException>(() => StakePlanner.Generate(0, to == 0 ? 100 : to, straight, curve, null, null));
    }

    [Fact]
    public void Too_many_stakes_throws()
    {
        Assert.Throws<ArgumentException>(() => StakePlanner.Generate(0, 100000, 1, 1, null, null));
    }

    [Fact]
    public void Replace_keeps_stakes_outside_the_range()
    {
        var existing = new[] { new RouteStake(0, StakeRole.Km, "Km0"), new RouteStake(20, StakeRole.Detail, "C1"), new RouteStake(40, StakeRole.Detail, "C2") };
        var generated = StakePlanner.Generate(30, 60, 10, 10, null, null);

        var merged = StakePlanner.Replace(existing, generated, 30, 60);

        Assert.Equal(new[] { 0.0, 20, 30, 40, 50, 60 }, merged.Select(s => s.Station));
        Assert.Equal("C1", merged[1].Name);
        Assert.Equal("", merged[3].Name);   // 40 was inside the range: replaced
    }

    [Fact]
    public void Insert_names_new_stakes_by_station_or_as_sub_stakes()
    {
        var existing = new[] { new RouteStake(0, StakeRole.Km, "Km0"), new RouteStake(20, StakeRole.Detail, "C1"), new RouteStake(40, StakeRole.Detail, "C2") };

        var byStation = StakePlanner.Insert(existing, new[] { 25.5, 20.0005 }, false, 2, out var skipped);
        var sub = StakePlanner.Insert(existing, new[] { 25.0, 30, 45 }, true, 2, out _);

        Assert.Equal(1, skipped);
        Assert.Equal(new[] { "Km0", "C1", "Km0+025.50", "C2" }, byStation.Select(s => s.Name));
        Assert.Equal(new[] { "Km0", "C1", "C1a", "C1b", "C2", "C2a" }, sub.Select(s => s.Name));
    }

    [Theory]
    [InlineData(0, StakeRole.Km)]
    [InlineData(2000.0004, StakeRole.Km)]
    [InlineData(300, StakeRole.Hundred)]
    [InlineData(950, StakeRole.Detail)]
    [InlineData(500, StakeRole.Hundred)]
    [InlineData(320, StakeRole.Detail)]
    public void RoleOf(double station, StakeRole role) => Assert.Equal(role, StakePlanner.RoleOf(station));

    [Fact]
    public void Classifier_matches_curve_keys_within_a_centimetre()
    {
        var stakes = new[] { new NamedStation(113.404, "X"), new NamedStation(0, "Km0"), new NamedStation(200, "H2"), new NamedStation(120, "C7") };

        var classified = StakeClassifier.Classify(stakes, new[] { Key(113.4, StakeKind.Td, 3) });

        Assert.Equal(new[] { StakeRole.Km, StakeRole.CurveKey, StakeRole.Detail, StakeRole.Hundred }, classified.Select(s => s.Role));
        Assert.Equal("X", classified[1].Name);
        Assert.Equal(3, classified[1].CurveNumber);
        Assert.Equal(StakeKind.Td, classified[1].CurveKind);
    }
}
