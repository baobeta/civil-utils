using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Presets;
using C3DTools.Core.Stations;
using Xunit;

namespace C3DTools.Core.Tests.Stations;

public class StakeTableSessionTests
{
    private static StakeTableSession Loaded()
    {
        var s = new StakeTableSession(new ProjectPreset());
        s.SetSource("Alignment T1", 0, 100);
        return s;
    }

    [Fact]
    public void Defaults()
    {
        var s = new StakeTableSession(new ProjectPreset());

        Assert.Equal("20", s.IntervalText);
        Assert.Equal(20, s.Interval);
        Assert.True(s.IncludeCurveStakes);
        Assert.False(s.IncludeGeometryPoints);
        Assert.True(s.WriteTable);
        Assert.False(s.WriteCsv);
        Assert.False(s.WriteXlsx);
        Assert.False(s.WriteCogo);
        Assert.Equal(new[] { "(Không lấy Z)" }, s.SurfaceNames);
        Assert.Null(s.Surface);
        Assert.False(s.CanApply);
        Assert.Equal("Chưa chọn tuyến", s.SummaryText);
    }

    [Theory]
    [InlineData("12,5", true, 12.5)]
    [InlineData("25", true, 25)]
    [InlineData("0", false, 0)]
    [InlineData("-5", false, 0)]
    [InlineData("abc", false, 0)]
    public void Interval_accepts_comma_or_dot_and_must_be_positive(string text, bool valid, double value)
    {
        var s = Loaded();

        s.IntervalText = text;

        Assert.Equal(valid, s.IsIntervalValid);
        Assert.Equal(valid, s.CanApply);
        if (valid) Assert.Equal(value, s.Interval);
        else Assert.Equal("Khoảng cách cọc phải là số lớn hơn 0", s.SummaryText);
    }

    [Fact]
    public void Extra_stations_accept_station_text_and_plain_numbers()
    {
        var s = Loaded();

        s.ExtraStationsText = "Km0+012,5; 0+045.25  60,75\n 99";

        Assert.True(s.IsExtrasValid);
        Assert.Equal(new[] { 12.5, 45.25, 60.75, 99 }, s.ExtraStations);
    }

    [Theory]
    [InlineData("12;abc")]
    [InlineData("150")]
    [InlineData("0+200")]
    public void Bad_or_out_of_route_extra_stations_block_apply(string text)
    {
        var s = Loaded();

        s.ExtraStationsText = text;

        Assert.False(s.IsExtrasValid);
        Assert.False(s.CanApply);
        Assert.Equal("Cọc thêm: lý trình không hợp lệ hoặc nằm ngoài tuyến", s.SummaryText);
    }

    [Fact]
    public void Needs_an_output()
    {
        var s = Loaded();

        s.WriteTable = false;

        Assert.False(s.CanApply);
        Assert.Equal("Chọn ít nhất một đầu ra", s.SummaryText);
        s.WriteCogo = true;
        Assert.True(s.CanApply);
    }

    [Fact]
    public void Stations_follow_the_options()
    {
        var s = Loaded();
        s.IntervalText = "50";
        s.ExtraStationsText = "10";
        var curve = new[] { new StakeStation(30, "TĐ1", StakeOrigin.Curve) };
        var geometry = new[] { 70.0 };

        Assert.Equal(new[] { "Km0", "C1", "TĐ1", "C2", "H1" }, s.Stations(curve, geometry).Select(x => x.Name));

        s.IncludeGeometryPoints = true;
        Assert.Equal(new[] { "Km0", "C1", "TĐ1", "C2", "C3", "H1" }, s.Stations(curve, geometry).Select(x => x.Name));

        s.IncludeCurveStakes = false;
        Assert.Equal(new[] { 0, 10, 50, 70, 100.0 }, s.Stations(curve, geometry).Select(x => x.Station));
    }

    [Fact]
    public void Preview_fills_the_grid_and_clears_stale()
    {
        var s = Loaded();
        Assert.True(s.IsStale);
        Assert.Equal("Bấm Xem trước để cập nhật bảng", s.SummaryText);

        s.SetPreview(new[] { new StakePoint("Km0", 0, 1, 2, null), new StakePoint("H1", 100, 3, 4, null) });

        Assert.False(s.IsStale);
        Assert.False(s.HasZ);
        Assert.Equal(2, s.PreviewRows.Count);
        Assert.Equal("H1", s.PreviewRows[1].Name);
        Assert.Equal("0+100.00", s.PreviewRows[1].Station);
        Assert.Equal("4.000", s.PreviewRows[1].X);   // X = Bắc (northing) by default
        Assert.Equal("", s.PreviewRows[1].Z);
        Assert.Equal(5, s.Table.Headers.Count);
        Assert.Equal("2 cọc", s.SummaryText);

        s.IntervalText = "10";
        Assert.True(s.IsStale);
    }

    [Fact]
    public void Preview_with_z_shows_the_surface()
    {
        var s = Loaded();
        s.SetSurfaces(new[] { "EG", "TN" });
        s.SurfaceIndex = 2;

        s.SetPreview(new[] { new StakePoint("Km0", 0, 1, 2, 5.5), new StakePoint("H1", 100, 3, 4, null) });

        Assert.Equal("TN", s.Surface);
        Assert.True(s.HasZ);
        Assert.Equal("5.50", s.PreviewRows[0].Z);
        Assert.Equal("2 cọc, Z từ mặt phủ TN (1 cọc ngoài mặt phủ)", s.SummaryText);
    }

    [Fact]
    public void SetSurfaces_keeps_the_chosen_surface_by_name()
    {
        var s = Loaded();
        s.SetSurfaces(new[] { "EG", "TN" });
        s.SurfaceIndex = 2;

        s.SetSurfaces(new[] { "TN", "XYZ" });
        Assert.Equal(1, s.SurfaceIndex);

        s.SetSurfaces(new[] { "XYZ" });
        Assert.Equal(0, s.SurfaceIndex);
        Assert.Null(s.Surface);
    }

    [Fact]
    public void Raises_summary_when_options_change()
    {
        var s = Loaded();
        var names = new List<string>();
        s.PropertyChanged += (o, e) => names.Add(e.PropertyName);

        s.IncludeCurveStakes = false;

        Assert.Contains(nameof(StakeTableSession.IncludeCurveStakes), names);
        Assert.Contains(nameof(StakeTableSession.SummaryText), names);
        Assert.Contains(nameof(StakeTableSession.CanApply), names);
    }

    [Fact]
    public void NorthingAsX_follows_the_preset_and_rebuilds_the_preview()
    {
        var s = new StakeTableSession(new ProjectPreset { StakeTable = new StakeTableOptions { NorthingAsX = false } });
        s.SetSource("T1", 0, 100);
        s.SetPreview(new[] { new StakePoint("Km0", 0, 1, 2, null) });
        Assert.False(s.NorthingAsX);
        Assert.Equal("1.000", s.PreviewRows[0].X);
        Assert.Equal("X (Đông)", s.XHeader);

        s.NorthingAsX = true;

        Assert.Equal("2.000", s.PreviewRows[0].X);
        Assert.Equal("1.000", s.PreviewRows[0].Y);
        Assert.Equal("X (Bắc)", s.Table.Headers[3]);
        Assert.Equal("Y (Đông)", s.YHeader);
        Assert.False(s.IsStale);
    }

    [Fact]
    public void MissingZ_counts_stakes_outside_the_surface()
    {
        var s = Loaded();
        s.SetSurfaces(new[] { "TN" });
        s.SurfaceIndex = 1;

        s.SetPreview(new[] { new StakePoint("Km0", 0, 1, 2, null), new StakePoint("H1", 100, 3, 4, 1) });

        Assert.Equal(1, s.MissingZ);
    }
}
