using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Drainage;
using C3DTools.Core.Presets;
using Xunit;

namespace C3DTools.Core.Tests.Drainage;

public class CulvertSessionTests
{
    private static CulvertRecord Record(string key, double station, double skew = 0) => new CulvertRecord
    {
        Key = key,
        PipeName = "Pipe - (" + key + ")",
        NetworkName = "Mạng 1",
        Station = station,
        SkewDeg = skew,
        Kind = Culverts.Circular,
        SizeText = "D1000",
        Length = 12,
        InnerHeight = 1.0,
        WallThickness = 0.1,
        InvertUpstream = 10.06,
        InvertDownstream = 10.0,
        GroundUpstream = 12.0,
        GroundDownstream = 11.9,
    };

    private static CulvertSession Ready(ProjectPreset preset = null)
    {
        var s = new CulvertSession(preset ?? new ProjectPreset());
        s.SetAlignment("Alignment T1");
        s.SetNetworks(new[] { "Mạng 1", "Mạng 2" }, null);
        return s;
    }

    [Fact]
    public void Defaults()
    {
        var s = new CulvertSession(new ProjectPreset());

        Assert.False(s.HasAlignment);
        Assert.Equal("chưa chọn", s.AlignmentText);
        Assert.True(s.WriteTable);
        Assert.False(s.WriteCsv);
        Assert.False(s.WriteXlsx);
        Assert.Equal(new[] { CulvertSession.NoSurface }, s.SurfaceNames);
        Assert.Null(s.Surface);
        Assert.False(s.CanApply);
        Assert.Equal("Chưa chọn tuyến", s.SummaryText);
    }

    [Fact]
    public void Networks_all_checked_when_nothing_remembered()
    {
        var s = Ready();

        Assert.Equal(new[] { "Mạng 1", "Mạng 2" }, s.SelectedNetworks);
        Assert.True(s.CanApply);
    }

    [Fact]
    public void Remembered_networks_stay_checked()
    {
        var s = new CulvertSession(new ProjectPreset());
        s.SetAlignment("A");
        s.SetNetworks(new[] { "Mạng 1", "Mạng 2" }, new[] { "Mạng 2", "Không còn" });

        Assert.Equal(new[] { "Mạng 2" }, s.SelectedNetworks);
    }

    [Fact]
    public void No_network_or_output_disables_apply()
    {
        var s = Ready();
        foreach (var n in s.Networks) n.IsChecked = false;
        Assert.False(s.CanApply);
        Assert.Equal("Chọn ít nhất một mạng cống", s.SummaryText);

        s.Networks[0].IsChecked = true;
        s.WriteTable = false;
        Assert.False(s.CanApply);
        Assert.Equal("Chọn ít nhất một đầu ra", s.SummaryText);
    }

    [Fact]
    public void Drawing_without_networks_says_so()
    {
        var s = new CulvertSession(new ProjectPreset());
        s.SetAlignment("A");
        s.SetNetworks(new string[0], null);

        Assert.False(s.CanApply);
        Assert.Equal("Bản vẽ không có mạng cống", s.SummaryText);
    }

    [Fact]
    public void Records_fill_sorted_rows_with_auto_names()
    {
        var s = Ready();
        s.SetRecords(new[] { Record("a", 250), Record("b", 40) });

        Assert.Equal(new[] { "C1", "C2" }, s.Rows.Select(r => r.Label));
        Assert.Equal(new[] { "Km0+040.00", "Km0+250.00" }, s.Rows.Select(r => r.Station));
        Assert.Equal("0.50", s.Rows[0].Slope);
        Assert.Equal("0.80", s.Rows[0].Cover);
        Assert.False(s.IsStale);
        Assert.Equal("2 cống", s.SummaryText);
    }

    [Fact]
    public void Changing_networks_or_surface_makes_the_preview_stale()
    {
        var s = Ready();
        s.SetSurfaces(new[] { "EG" }, null);
        s.SetRecords(new[] { Record("a", 10) });
        Assert.False(s.IsStale);

        s.Networks[1].IsChecked = false;
        Assert.True(s.IsStale);
        Assert.Equal("Bấm Xem trước để cập nhật bảng", s.SummaryText);

        s.SetRecords(new[] { Record("a", 10) });
        s.SurfaceIndex = 1;
        Assert.Equal("EG", s.Surface);
        Assert.True(s.IsStale);
    }

    [Fact]
    public void Editing_a_name_renumbers_the_others()
    {
        var s = Ready();
        s.SetRecords(new[] { Record("a", 10), Record("b", 20), Record("c", 30) });

        s.Rows[0].Label = "C2";

        Assert.Equal(new[] { "C2", "C1", "C3" }, s.Rows.Select(r => r.Label));

        s.Rows[0].Label = "";
        Assert.Equal(new[] { "C1", "C2", "C3" }, s.Rows.Select(r => r.Label));
    }

    [Fact]
    public void Edits_survive_a_new_preview()
    {
        var s = Ready();
        s.SetRecords(new[] { Record("a", 10), Record("b", 20, skew: 12) });
        Assert.Equal("Chéo 12°", s.Rows[1].Note);

        s.Rows[0].Kind = Culverts.Box;
        s.Rows[0].Note = "Cống cũ";
        s.Rows[1].Label = "CB1";

        s.SetRecords(new[] { Record("b", 20, skew: 12), Record("a", 10), Record("c", 5) });

        Assert.Equal(new[] { "C1", "C2", "CB1" }, s.Rows.Select(r => r.Label));
        Assert.Equal(Culverts.Box, s.Rows[1].Kind);
        Assert.Equal("Cống cũ", s.Rows[1].Note);
        Assert.Equal(Culverts.Circular, s.Rows[0].Kind);
    }

    [Fact]
    public void Table_uses_the_edited_rows()
    {
        var s = Ready();
        s.SetRecords(new[] { Record("a", 10) });
        s.Rows[0].Kind = Culverts.Box;
        s.Rows[0].Note = "Ghi chú";

        var table = s.BuildTable();

        Assert.Equal("Cống hộp", table.Rows[0][3]);
        Assert.Equal("Ghi chú", table.Rows[0][10]);
    }

    [Fact]
    public void Warnings_counted_in_summary_when_rules_exist()
    {
        var s = Ready(new ProjectPreset { PipeRules = new PipeRules(minSlope: 0.01, minCover: 0.5) });
        var flat = Record("b", 20);
        flat.InvertUpstream = 10.24;   // 2 %
        s.SetRecords(new[] { Record("a", 10), flat });

        Assert.True(s.Rows[0].HasWarning);
        Assert.False(s.Rows[1].HasWarning);
        Assert.Equal("2 cống, 1 cống có cảnh báo", s.SummaryText);
    }

    [Fact]
    public void Without_rules_the_summary_says_checks_are_skipped()
    {
        var s = Ready();
        s.SetRecords(new[] { Record("a", 10) });

        Assert.False(s.HasRules);
        Assert.False(s.Rows[0].HasWarning);
        Assert.Equal("", s.Rows[0].Warning);
    }

    [Fact]
    public void Kind_names_include_the_read_kind()
    {
        var s = Ready();
        var odd = Record("a", 10);
        odd.Kind = "Cống đặc biệt";
        s.SetRecords(new[] { odd });

        Assert.Contains(Culverts.Circular, s.KindNames);
        Assert.Contains(Culverts.Box, s.KindNames);
        Assert.Contains("Cống đặc biệt", s.KindNames);
    }

    [Fact]
    public void Surfaces_keep_the_remembered_one()
    {
        var s = Ready();
        s.SetSurfaces(new List<string> { "EG", "FG" }, "FG");

        Assert.Equal("FG", s.Surface);
        Assert.Equal(2, s.SurfaceIndex);
    }
}
