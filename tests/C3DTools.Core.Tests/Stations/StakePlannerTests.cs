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
    public void Spacing_100_puts_C_stakes_between_the_H_stakes_from_Km0_050()
    {
        var stakes = StakePlanner.Generate(0, 1200, 100, 10, null, null);
        var names = StakeNamer.Name(stakes, new StakeNamingOptions());

        Assert.Equal(new[] { 0.0, 50, 100, 150, 200 }, stakes.Take(5).Select(s => s.Station));
        Assert.Equal(new[] { "Km0", "C1", "H1", "C2", "H2" }, names.Take(5));
        var at1000 = stakes.FindIndex(s => s.Station == 1000);
        Assert.Equal(new[] { "C10", "Km1", "C11", "H1", "C12", "H2" }, names.Skip(at1000 - 1).Take(6));   // 950 … 1200
        Assert.Single(stakes, s => s.Station == 1000);   // no C or H on the Km stake
    }

    [Fact]
    public void Spacing_20_starts_C1_at_Km0_020_and_skips_H_and_Km_positions()
    {
        var stakes = StakePlanner.Generate(0, 1040, 20, 10, null, null);
        var names = StakeNamer.Name(stakes, new StakeNamingOptions());

        Assert.Equal(20, stakes[1].Station);
        Assert.Equal("C1", names[1]);
        Assert.Equal(new[] { "C40", "Km1", "C41", "C42" }, names.Skip(49));
        Assert.Equal(stakes.Count, stakes.Select(s => s.Station).Distinct().Count());
    }

    [Fact]
    public void C_stakes_run_through_curves_and_curve_stakes_are_only_added_when_asked()
    {
        var keys = new[] { Key(830, StakeKind.Td, 1), Key(1100, StakeKind.Tc, 1) };
        var zone = new[] { new StationZone(830, 1100) };

        var plain = StakePlanner.Generate(700, 1200, 100, 0, null, keys, detailStart: 20);
        var dense = StakePlanner.Generate(700, 1200, 100, 50, zone, keys, detailStart: 20);

        // C at 720, 820, 920, 1020, 1120: the ones inside the curve stay.
        Assert.Equal(new[] { 700.0, 720, 800, 820, 830, 900, 920, 1000, 1020, 1100, 1120, 1200 }, plain.Select(s => s.Station));
        Assert.Equal(new[] { "H7", "C1", "H8", "C2", "TĐ1", "H9", "C3", "Km1", "C4", "TC1", "C5", "H2" }, StakeNamer.Name(plain, new StakeNamingOptions()));
        Assert.Equal(new[] { 850.0, 950, 1050 }, dense.Select(s => s.Station).Except(plain.Select(s => s.Station)));
    }

    [Fact]
    public void A_chosen_start_station_shifts_the_C_stakes()
    {
        var stakes = StakePlanner.Generate(0, 100, 20, 10, null, null, detailStart: 5);

        Assert.Equal(new[] { 0.0, 5, 25, 45, 65, 85, 100 }, stakes.Select(s => s.Station));
        Assert.Equal(new[] { 45.0, 65, 85, 100 }, StakePlanner.Generate(40, 100, 20, 10, null, null, detailStart: 5).Skip(1).Select(s => s.Station));
    }

    [Theory]
    [InlineData(20, 20)]
    [InlineData(100, 50)]
    [InlineData(25, 25)]
    public void Default_detail_start(double spacing, double start) => Assert.Equal(start, StakePlanner.DefaultDetailStart(spacing));

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
        Assert.ThrowsAny<ArgumentException>(() => StakePlanner.Generate(0, to == 0 ? 100 : to, straight, curve, new[] { new StationZone(1, 5) }, null));
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
