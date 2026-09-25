using C3DTools.Core.Presets;
using C3DTools.Core.Surfaces;
using Xunit;

namespace C3DTools.Core.Tests.Surfaces;

public class SurfaceSessionTests
{
    private static SurfaceSession Session()
    {
        var s = new SurfaceSession(new ProjectPreset());
        s.SetSurfaces(new[] { "EG", "TN" }, "TN");
        return s;
    }

    [Fact]
    public void Defaults_come_from_the_preset()
    {
        var preset = new ProjectPreset { Surface = new SurfaceOptions { MaxEdgeLength = 35, MajorInterval = 2, MinorInterval = 0.5, ContourTextHeight = 1.8, ContourLabelSpacing = 10 } };

        var s = new SurfaceSession(preset);

        Assert.Equal("35", s.MaxEdgeText);
        Assert.Equal("2", s.MajorIntervalText);
        Assert.Equal("0.5", s.MinorIntervalText);
        Assert.Equal("1.8", s.TextHeightText);
        Assert.Equal(10, s.LabelOptions().Spacing);
    }

    [Fact]
    public void Preferred_surface_is_chosen()
    {
        Assert.Equal("TN", Session().Surface);
    }

    [Fact]
    public void Cleanup_needs_a_valid_max_edge_or_a_boundary()
    {
        var s = Session();
        Assert.True(s.CanApply);

        s.MaxEdgeText = "abc";
        Assert.False(s.IsMaxEdgeValid);
        Assert.False(s.CanApply);

        s.UseMaxEdge = false;
        Assert.False(s.CanApply);
        Assert.Contains("ranh giới", s.SummaryText);

        s.SetBoundary("Polyline 2A");
        Assert.True(s.CanApply);
        Assert.Equal(0, s.MaxEdge);
    }

    [Fact]
    public void Max_edge_accepts_a_comma()
    {
        var s = Session();
        s.MaxEdgeText = "42,5";

        Assert.Equal(42.5, s.MaxEdge);
    }

    [Fact]
    public void Count_is_reset_when_an_option_changes()
    {
        var s = Session();
        s.SetCount(1000, 12, 20);
        Assert.Contains("12 / 1000", s.CountText);

        s.MaxEdgeText = "30";

        Assert.Contains("Bấm Đếm", s.CountText);
    }

    [Fact]
    public void Label_tab_needs_lines_and_valid_numbers()
    {
        var s = Session();
        s.TabIndex = SurfaceSession.LabelTab;
        Assert.False(s.CanApply);
        Assert.Contains("chưa chọn", s.SourceText);

        s.SetLines("2 đường", 2);
        Assert.True(s.CanApply);

        s.TextHeightText = "0";
        Assert.False(s.CanApply);
        s.TextHeightText = "2,5";
        s.SpacingText = "-1";
        Assert.False(s.CanApply);
    }

    [Fact]
    public void Style_intervals_replace_the_preset_until_typed()
    {
        var s = Session();

        s.SetIntervalsFromStyle(10, 2);

        Assert.Equal("10", s.MajorIntervalText);
        Assert.Equal("2", s.MinorIntervalText);
        Assert.Contains("kiểu hiển thị", s.IntervalSourceText);
        s.MinorIntervalText = "1";
        Assert.Contains("preset", s.IntervalSourceText);
        Assert.Equal(10, s.LabelOptions().MajorInterval);
    }

    [Fact]
    public void No_surfaces_blocks_apply()
    {
        var s = new SurfaceSession(new ProjectPreset());
        s.SetSurfaces(new string[0]);

        Assert.False(s.CanApply);
        Assert.Contains("không có mặt phủ", s.SummaryText);
    }
}
