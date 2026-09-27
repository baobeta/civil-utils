using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Curves;
using C3DTools.Core.Stations;
using Xunit;

namespace C3DTools.Core.Tests.Stations;

public class StakeNamerTests
{
    private static RouteStake D(double s, string name = "") => new RouteStake(s, StakeRole.Detail, name);
    private static RouteStake H(double s) => new RouteStake(s, StakeRole.Hundred);
    private static RouteStake Km(double s) => new RouteStake(s, StakeRole.Km);
    private static RouteStake Key(double s, StakeKind kind, int curve, string name = "") => new RouteStake(s, StakeRole.CurveKey, name, kind, curve);

    /// <summary>0 … 1140 every 20 m on a straight route, with H and Km roles where they fall.</summary>
    private static List<RouteStake> Straight(double end)
    {
        var list = new List<RouteStake>();
        for (var s = 0.0; s <= end + 1e-9; s += 20)
            list.Add(s % 1000 == 0 ? Km(s) : s % 100 == 0 ? H(s) : D(s));
        return list;
    }

    [Fact]
    public void Defaults_number_detail_stakes_through_H_and_restart_after_each_Km()
    {
        var names = StakeNamer.Name(Straight(1140), new StakeNamingOptions());

        Assert.Equal(new[] { "Km0", "C1", "C2", "C3", "C4", "H1", "C5" }, names.Take(7));
        Assert.Equal("H9", names[45]);          // 900
        Assert.Equal("C40", names[49]);         // 980: 4 per 100 m × 10 = 40 detail stakes in km 0
        Assert.Equal("Km1", names[50]);
        Assert.Equal(new[] { "C1", "C2", "C3", "C4", "H1", "C5" }, names.Skip(51).Take(6));
    }

    [Fact]
    public void Without_continuous_numbering_C_restarts_after_each_H()
    {
        var names = StakeNamer.Name(Straight(240), new StakeNamingOptions { DetailContinuousThroughH = false });

        Assert.Equal(new[] { "Km0", "C1", "C2", "C3", "C4", "H1", "C1", "C2", "C3", "C4", "H2", "C1", "C2" }, names);
    }

    [Fact]
    public void Without_restart_per_Km_numbers_run_on()
    {
        var names = StakeNamer.Name(Straight(1040), new StakeNamingOptions { RestartPerKm = false });

        Assert.Equal("Km1", names[50]);
        Assert.Equal(new[] { "C41", "C42" }, names.Skip(51));
    }

    [Fact]
    public void A_Km_does_not_restart_numbers_that_reached_100_when_asked()
    {
        var stakes = new List<RouteStake> { Km(0) };
        for (var i = 1; i <= 100; i++) stakes.Add(D(i * 9.9));
        stakes.Add(Km(1000));
        stakes.Add(D(1010));

        Assert.Equal("C101", StakeNamer.Name(stakes, new StakeNamingOptions())[102]);
        Assert.Equal("C1", StakeNamer.Name(stakes, new StakeNamingOptions { NoRestartFrom100 = false })[102]);
    }

    [Fact]
    public void Without_H_stakes_hundreds_are_detail_stakes()
    {
        var names = StakeNamer.Name(Straight(120), new StakeNamingOptions { CreateHundreds = false });

        Assert.Equal(new[] { "Km0", "C1", "C2", "C3", "C4", "C5", "C6" }, names);
    }

    [Fact]
    public void Prefix_and_first_number_apply_and_the_first_Km_keeps_them()
    {
        var names = StakeNamer.Name(Straight(60), new StakeNamingOptions { DetailPrefix = "D", FirstDetailNumber = 7 });

        Assert.Equal(new[] { "Km0", "D7", "D8", "D9" }, names);
    }

    [Fact]
    public void Name_by_station_gives_Km_plus_metres()
    {
        var stakes = new List<RouteStake> { Km(0), D(20), D(125.5), H(200) };

        var names = StakeNamer.Name(stakes, new StakeNamingOptions { NameByStation = true });

        Assert.Equal(new[] { "Km0", "Km0+020", "Km0+125.50", "H2" }, names);
    }

    [Fact]
    public void Curve_keys_get_curve_numbers_from_the_first_PI_number_and_do_not_use_detail_numbers()
    {
        var stakes = new List<RouteStake> { Km(0), D(20), Key(31, StakeKind.Td, 1, "TĐ9"), Key(55, StakeKind.P, 1), D(60), Key(79, StakeKind.Tc, 1) };

        Assert.Equal(new[] { "Km0", "C1", "TĐ5", "P5", "C2", "TC5" },
            StakeNamer.Name(stakes, new StakeNamingOptions { FirstPiNumber = 5 }));
        Assert.Equal(new[] { "Km0", "C1", "TĐ9", "P1", "C2", "TC1" },
            StakeNamer.Name(stakes, new StakeNamingOptions { RenameCurveKeys = false }));
    }

    [Fact]
    public void Stakes_with_a_kept_prefix_keep_their_name_and_take_no_number()
    {
        var stakes = new List<RouteStake> { Km(0), D(20, "CT1"), D(40, "C9"), D(60, "cong2") };

        var names = StakeNamer.Name(stakes, new StakeNamingOptions { KeepPrefixes = new List<string> { "CT", " cong " } });

        Assert.Equal(new[] { "Km0", "CT1", "C1", "cong2" }, names);
    }

    [Fact]
    public void Only_the_range_is_renamed_and_numbering_starts_there()
    {
        var stakes = new List<RouteStake> { Km(0), D(20, "A"), D(40, "B"), D(60, "C"), D(80, "E") };

        var names = StakeNamer.Name(stakes, new StakeNamingOptions { FirstDetailNumber = 3 }, fromIndex: 2, toIndex: 3);

        Assert.Equal(new[] { "", "A", "C3", "C4", "E" }, names);
    }

    [Fact]
    public void Bad_range_throws()
    {
        var stakes = Straight(60);

        Assert.Throws<System.ArgumentOutOfRangeException>(() => StakeNamer.Name(stakes, null, 3, 1));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => StakeNamer.Name(stakes, null, 0, 9));
    }

    [Theory]
    [InlineData(900, "H9")]
    [InlineData(1900, "H9")]
    [InlineData(1000, "H0")]
    [InlineData(100.0004, "H1")]
    public void Hundred_names_use_the_hundreds_digit(double station, string name) => Assert.Equal(name, StakeNamer.HundredName(station));

    [Theory]
    [InlineData("H1 (Km1)", "H1")]
    [InlineData("C3 (Km0)-2", "C3")]
    [InlineData("TĐ5", "TĐ5")]
    [InlineData("Km0+125.50", "Km0+125.50")]
    [InlineData("Cầu (Km2) cũ", "Cầu (Km2) cũ")]
    [InlineData("X (Kma)", "X (Kma)")]
    [InlineData("", "")]
    public void Display_name_drops_the_uniqueness_suffix(string label, string shown) => Assert.Equal(shown, StakeNamer.DisplayName(label));

    [Fact]
    public void Unique_labels_add_the_km_then_a_counter()
    {
        var labels = StakeNamer.UniqueLabels(new[] { "H1", "C1", "H1", "", "C1" }, new[] { 100.0, 120, 1100, 1125.5, 1120 });

        Assert.Equal(new[] { "H1 (Km0)", "C1 (Km0)", "H1 (Km1)", "Km1+125.50", "C1 (Km1)" }, labels);
        Assert.Equal(new[] { "X (Km0)", "X (Km0)-2" }, StakeNamer.UniqueLabels(new[] { "X", "x" }, new[] { 1.0, 2 }).Select(l => l.Replace("x", "X")));
    }
}
