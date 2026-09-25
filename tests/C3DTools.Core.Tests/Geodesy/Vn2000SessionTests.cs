using System.Collections.Generic;
using System.IO;
using System.Linq;
using C3DTools.Core.Geodesy;
using C3DTools.Core.Presets;
using Xunit;

namespace C3DTools.Core.Tests.Geodesy;

public class Vn2000SessionTests
{
    private static ProjectPreset Preset() => new ProjectPreset
    {
        Vn2000 = new Vn2000Options
        {
            Provinces = new List<Vn2000Province>
            {
                new Vn2000Province { Province = "Hải Phòng", MeridianDeg = 105, MeridianMin = 45 },
                new Vn2000Province { Province = "Thái Nguyên", MeridianDeg = 106, MeridianMin = 30 },
            },
        },
    };

    private static List<Vn2000Item> Items(int count) =>
        Enumerable.Range(0, count).Select(i => new Vn2000Item("P" + i, 600000 + 100 * i, 2300000 + 50 * i)).ToList();

    [Fact]
    public void Province_choices_resolve_to_their_meridian()
    {
        var s = new Vn2000Session(Preset());

        s.FromText = s.MeridianChoices[1];
        s.ToText = "hải phòng";

        Assert.Equal(106.5, s.FromMeridian);
        Assert.Equal(105.75, s.ToMeridian);
    }

    [Fact]
    public void Typed_meridian_is_parsed_and_invalid_text_blocks_apply()
    {
        var s = new Vn2000Session(Preset());
        s.SetSelection("3 đối tượng", Items(3));
        s.FromText = "105 45";
        s.ToText = "106°15'";
        Assert.True(s.CanApply);

        s.ToText = "106°75'";

        Assert.True(s.ToIsInvalid);
        Assert.Null(s.ToMeridian);
        Assert.False(s.CanApply);
        Assert.Empty(s.Rows);
    }

    [Fact]
    public void Preview_shows_the_first_five_points_and_the_max_shift()
    {
        var s = new Vn2000Session(Preset());
        s.FromText = "105°45'";
        s.ToText = "106°15'";

        s.SetSelection("8 đối tượng", Items(8));

        Assert.Equal(5, s.Rows.Count);
        Assert.Equal("P0", s.Rows[0].Name);
        Assert.Equal("600000.000", s.Rows[0].XBefore);
        var expected = Vn2000.Convert(600000, 2300000, 105.75, 3, 106.25, 3);
        Assert.Equal(expected.X.ToString("F3", System.Globalization.CultureInfo.InvariantCulture), s.Rows[0].XAfter);
        Assert.True(s.MaxShift > 50000);
        Assert.Contains("8 điểm", s.SummaryText);
    }

    [Fact]
    public void Same_meridian_and_zone_cannot_be_applied()
    {
        var s = new Vn2000Session(Preset());
        s.SetSelection("1", Items(1));
        s.FromText = "105°45'";
        s.ToText = "105.75";

        Assert.False(s.CanApply);
        Assert.Contains("trùng", s.SummaryText);

        s.ToZoneWidth = 6;
        Assert.True(s.CanApply);
    }

    [Fact]
    public void Target_switches_between_selection_and_cogo_points()
    {
        var s = new Vn2000Session(Preset()) { FromText = "105°45'", ToText = "106°15'" };
        s.SetSelection("2", Items(2));
        s.SetCogo(Items(4));

        Assert.Equal(2, s.Rows.Count);
        s.TargetCogo = true;

        Assert.Equal(Vn2000Target.Cogo, s.Target);
        Assert.False(s.TargetSelection);
        Assert.Equal(4, s.Rows.Count);
    }

    [Fact]
    public void Arc_radius_error_is_reported()
    {
        var s = new Vn2000Session(Preset()) { FromText = "105°45'", ToText = "106°15'" };
        var items = Items(1);
        items.Add(new Vn2000Item("Cung", 600000, 2300000, 1000));

        s.SetSelection("2", items);

        Assert.True(s.MaxRadiusError > 0);
        Assert.Contains("bán kính", s.DistortionText);
        Assert.Contains("ppm", s.DistortionText);
    }

    [Fact]
    public void Bundled_preset_seeds_unverified_province_meridians()
    {
        var preset = PresetSerializer.Load(File.ReadAllText(Path.Combine("Resources", "tcvn4054.preset.json")));

        var provinces = preset.Vn2000.Provinces;
        Assert.Equal(25, provinces.Count);
        Assert.All(provinces, p => Assert.False(p.Verified));
        Assert.All(provinces, p => Assert.False(string.IsNullOrEmpty(p.Source)));
        Assert.Equal(105.0, provinces.Single(p => p.Province == "Hà Nội").Meridian);
        Assert.Equal(105.75, provinces.Single(p => p.Province == "TP. Hồ Chí Minh").Meridian);
        Assert.Equal(50, preset.Surface.MaxEdgeLength);
    }

    [Fact]
    public void Typing_a_meridian_updates_the_preview_rows_and_defers_the_statistics()
    {
        var s = new Vn2000Session(Preset()) { FromText = "105°45'", ToText = "106°15'" };
        s.SetSelection("8 đối tượng", Items(8));
        var before = s.MaxShift;
        var rowBefore = s.Rows[0].XAfter;

        s.ToText = "107°45'";

        Assert.Equal(5, s.Rows.Count);
        Assert.NotEqual(rowBefore, s.Rows[0].XAfter);
        Assert.Equal(before, s.MaxShift);
        Assert.Contains("Xem trước", s.DistortionText);

        s.UpdateStatistics();

        Assert.True(s.MaxShift > before);
        Assert.Contains("ppm", s.DistortionText);
    }
}
