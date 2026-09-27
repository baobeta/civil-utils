using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C3DTools.Core.Curves;
using C3DTools.Core.Presets;
using Xunit;

namespace C3DTools.Core.Tests.Presets;

/// <summary>
/// The bundled TCVN 4054:2005 tables. The values were entered from memory (see CurveRules.Source); these tests
/// cannot prove them right, only that they are complete and consistent with each other and with Bảng 11.
/// </summary>
public class TcvnTablesTests
{
    private static readonly double[] Speeds = { 120, 100, 80, 60, 40, 30, 20 };

    /// <summary>Bảng 11: R không cần siêu cao.</summary>
    private static readonly Dictionary<double, double> NoSuperRadius = new Dictionary<double, double>
    {
        [120] = 5500, [100] = 4000, [80] = 2500, [60] = 1500, [40] = 600, [30] = 350, [20] = 250,
    };

    private static ProjectPreset Bundled() =>
        PresetSerializer.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "tcvn4054.preset.json")));

    private static List<RadiusRangeRule> ForSpeed(IEnumerable<RadiusRangeRule> table, double speed) =>
        table.Where(r => r.DesignSpeed == speed).OrderBy(r => r.RadiusFrom).ToList();

    [Theory]
    [MemberData(nameof(AllSpeeds))]
    public void Superelevation_ranges_are_contiguous_from_zero_to_infinity_and_end_with_no_superelevation(double v)
    {
        var rows = ForSpeed(Bundled().CurveRules.Superelevation, v);

        Assert.NotEmpty(rows);
        Assert.Equal(0, rows[0].RadiusFrom);
        for (var i = 1; i < rows.Count; i++) Assert.Equal(rows[i - 1].RadiusTo, rows[i].RadiusFrom);
        Assert.True(rows[rows.Count - 1].RadiusTo >= 1e8);
        Assert.Equal(0, rows[rows.Count - 1].Value);
        Assert.Equal(NoSuperRadius[v], rows[rows.Count - 1].RadiusFrom);
    }

    [Theory]
    [MemberData(nameof(AllSpeeds))]
    public void Superelevation_rate_falls_with_radius_and_never_exceeds_the_maximum(double v)
    {
        var rows = ForSpeed(Bundled().CurveRules.Superelevation, v);

        for (var i = 1; i < rows.Count; i++) Assert.True(rows[i].Value < rows[i - 1].Value, $"V{v}: isc phải giảm dần theo R");
        Assert.True(rows[0].Value <= (v >= 80 ? 8 : 7), $"V{v}: isc max");
        Assert.Equal(2, rows[rows.Count - 2].Value);   // the widest range before "không bố trí" is 2 %
    }

    [Theory]
    [MemberData(nameof(AllSpeeds))]
    public void Runoff_lengths_cover_the_same_ranges_and_fall_with_radius(double v)
    {
        var rules = Bundled().CurveRules;
        var isc = ForSpeed(rules.Superelevation, v).Where(r => r.Value > 0).ToList();
        var lengths = ForSpeed(rules.MinSpiral, v);

        Assert.Equal(isc.Select(r => (r.RadiusFrom, r.RadiusTo)), lengths.Select(r => (r.RadiusFrom, r.RadiusTo)));
        for (var i = 1; i < lengths.Count; i++) Assert.True(lengths[i].Value <= lengths[i - 1].Value, $"V{v}: L phải không tăng theo R");
        Assert.All(lengths, r => Assert.True(r.Value >= 20));
    }

    [Fact]
    public void Runoff_length_grows_with_speed_at_the_same_rate()
    {
        var rules = Bundled().CurveRules;
        double LengthAt(double v, double rate)
        {
            var range = ForSpeed(rules.Superelevation, v).First(r => r.Value == rate);
            return ForSpeed(rules.MinSpiral, v).First(r => r.RadiusFrom == range.RadiusFrom).Value;
        }

        for (var i = 1; i < Speeds.Length; i++) Assert.True(LengthAt(Speeds[i], 2) <= LengthAt(Speeds[i - 1], 2));
    }

    [Fact]
    public void Tables_start_at_the_limit_radius_of_Bang_11()
    {
        var rules = Bundled().CurveRules;
        foreach (var speed in rules.MinRadius)
        {
            var first = ForSpeed(rules.Superelevation, speed.DesignSpeed)[0];
            Assert.True(first.RadiusTo > speed.MinRadius, $"V{speed.DesignSpeed}: dải đầu phải chứa Rmin giới hạn");
            Assert.NotNull(CurveRuleChecker.Find(rules.Superelevation, speed.MinRadius, speed.DesignSpeed));
            Assert.NotNull(CurveRuleChecker.Find(rules.MinSpiral, speed.MinRadius, speed.DesignSpeed));
        }
    }

    [Fact]
    public void Tra_sieu_cao_uses_the_bundled_tables()
    {
        var s = new CurveDesignSession(Bundled());
        s.Load(new[] { new PlanPoint(0, 0), new PlanPoint(1000, 0), new PlanPoint(1000, 1000) }, new[] { new CurveInput { Radius = 250 } });

        Assert.Null(s.SuggestSuperelevation(0));
        Assert.True(s.Inputs[0].Superelevated);
        Assert.Equal(4, s.Inputs[0].SuperRate);   // V60, R = 250: (200, 250]
        Assert.Equal(50, s.Inputs[0].RunoffIn);
        Assert.Equal(25, s.Inputs[0].OffsetIn);

        s.Rows[0].RadiusText = "2000";
        Assert.Null(s.SuggestSuperelevation(0));
        Assert.False(s.Inputs[0].Superelevated);   // beyond R không siêu cao: "Không bố trí"

        s.Rows[0].SpeedText = "80";
        s.Rows[0].RadiusText = "260";
        Assert.Null(s.SuggestSuperelevation(0));
        Assert.Equal(8, s.Inputs[0].SuperRate);
        Assert.Equal(110, s.Inputs[0].RunoffIn);
    }

    public static IEnumerable<object[]> AllSpeeds() => Speeds.Select(v => new object[] { v });
}
