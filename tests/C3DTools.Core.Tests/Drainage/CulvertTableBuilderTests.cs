using System.Linq;
using C3DTools.Core.Drainage;
using C3DTools.Core.Presets;
using Xunit;

namespace C3DTools.Core.Tests.Drainage;

public class CulvertTableBuilderTests
{
    private static CulvertRecord Record(string key, double station, string name = "") => new CulvertRecord
    {
        Key = key,
        PipeName = "Pipe - (" + key + ")",
        Name = name,
        Station = station,
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

    [Fact]
    public void Headers_are_the_vietnamese_schedule_columns()
    {
        var table = CulvertTableBuilder.Build(new[] { Record("1", 10) }, new ProjectPreset());

        Assert.Equal(new[]
        {
            "STT", "Tên", "Lý trình", "Loại", "Khẩu độ", "Chiều dài (m)", "CĐ đáy thượng lưu", "CĐ đáy hạ lưu",
            "Độ dốc (%)", "Chiều sâu chôn min (m)", "Ghi chú", "Cảnh báo",
        }, table.Headers);
    }

    [Fact]
    public void Rows_are_sorted_by_station_and_named_in_order()
    {
        var table = CulvertTableBuilder.Build(new[] { Record("a", 250), Record("b", 40), Record("c", 1125.5) }, new ProjectPreset());

        Assert.Equal(new[] { "1", "2", "3" }, table.Rows.Select(r => r[0]));
        Assert.Equal(new[] { "C1", "C2", "C3" }, table.Rows.Select(r => r[1]));
        Assert.Equal(new[] { "Km0+040.00", "Km0+250.00", "Km1+125.50" }, table.Rows.Select(r => r[2]));
    }

    [Fact]
    public void Explicit_names_are_kept_and_auto_names_skip_them()
    {
        var sorted = CulvertTableBuilder.Sorted(new[] { Record("a", 10), Record("b", 20, "C1"), Record("c", 30), Record("d", 40, "Cống bản") });

        Assert.Equal(new[] { "C2", "C1", "C3", "Cống bản" }, CulvertTableBuilder.Names(sorted));
    }

    [Fact]
    public void Formats_numbers_with_preset_decimals()
    {
        var preset = new ProjectPreset();
        preset.Culvert.ElevationDecimals = 3;
        preset.Culvert.LengthDecimals = 1;
        preset.Culvert.SlopeDecimals = 1;

        var row = CulvertTableBuilder.Build(new[] { Record("1", 10) }, preset).Rows[0];

        Assert.Equal("Cống tròn", row[3]);
        Assert.Equal("D1000", row[4]);
        Assert.Equal("12.0", row[5]);
        Assert.Equal("10.060", row[6]);
        Assert.Equal("10.000", row[7]);
        Assert.Equal("0.5", row[8]);       // (10.06 - 10.00) / 12 = 0.5 %
        Assert.Equal("0.800", row[9]);     // min(12.0 - 11.16, 11.9 - 11.10) = 0.80
        Assert.Equal("", row[11]);         // no PipeRules in the preset
    }

    [Fact]
    public void Missing_ground_leaves_cover_empty()
    {
        var r = Record("1", 10);
        r.GroundUpstream = null;
        r.GroundDownstream = null;

        var row = CulvertTableBuilder.Build(new[] { r }, new ProjectPreset()).Rows[0];

        Assert.Null(r.CoverMin);
        Assert.Equal("", row[9]);
    }

    [Fact]
    public void Warnings_come_from_pipe_checker_when_rules_exist()
    {
        var preset = new ProjectPreset { PipeRules = new PipeRules(minSlope: 0.01, minCover: 0.85) };

        var row = CulvertTableBuilder.Build(new[] { Record("1", 10) }, preset).Rows[0];

        Assert.Contains("độ dốc 0.50% nhỏ hơn tối thiểu 1.00%", row[11]);
        Assert.Contains("chiều sâu chôn đầu cống 0.84 m nhỏ hơn 0.85 m", row[11]);
        Assert.Contains("chiều sâu chôn cuối cống 0.80 m nhỏ hơn 0.85 m", row[11]);
        Assert.DoesNotContain("Cống C1:", row[11]);
    }

    [Fact]
    public void Missing_ground_is_not_a_cover_warning()
    {
        var preset = new ProjectPreset { PipeRules = new PipeRules(minSlope: 0.001, minCover: 5) };
        var r = Record("1", 10);
        r.GroundUpstream = null;
        r.GroundDownstream = null;

        Assert.Equal("", CulvertTableBuilder.Warning(r, "C1", preset.PipeRules));
    }

    [Fact]
    public void Good_culvert_has_no_warning()
    {
        var rules = new PipeRules(minSlope: 0.003, minCover: 0.5);

        Assert.Equal("", CulvertTableBuilder.Warning(Record("1", 10), "C1", rules));
    }

    [Fact]
    public void Slope_is_positive_when_downstream_is_lower()
    {
        var r = Record("1", 10);
        Assert.Equal(0.5, r.SlopePercent, 9);

        r.InvertDownstream = 10.12;
        Assert.Equal(-0.5, r.SlopePercent, 9);

        r.Length = 0;
        Assert.True(double.IsNaN(r.SlopePercent));
    }

    [Fact]
    public void Side_text_from_offset()
    {
        var r = Record("1", 10);
        r.Offset = -2.5;
        Assert.Equal("Trái 2.50", r.SideText);
        r.Offset = 3.456;
        Assert.Equal("Phải 3.46", r.SideText);
        r.Offset = 0.001;
        Assert.Equal("Tim", r.SideText);
    }

    [Fact]
    public void Default_note_mentions_skew()
    {
        var r = Record("1", 10);
        r.SkewDeg = 15.04;
        Assert.Equal("Chéo 15°", CulvertTableBuilder.DefaultNote(r));
        r.SkewDeg = 0.3;
        Assert.Equal("", CulvertTableBuilder.DefaultNote(r));
    }

    [Fact]
    public void Note_is_printed()
    {
        var r = Record("1", 10);
        r.Note = "Cống hiện hữu";

        Assert.Equal("Cống hiện hữu", CulvertTableBuilder.Build(new[] { r }, new ProjectPreset()).Rows[0][10]);
    }

    [Fact]
    public void Output_is_culture_invariant()
    {
        TestCulture.Run("vi-VN", () =>
        {
            var row = CulvertTableBuilder.Build(new[] { Record("1", 1234.5) }, new ProjectPreset()).Rows[0];

            Assert.Equal("Km1+234.50", row[2]);
            Assert.Equal("12.00", row[5]);
            Assert.Equal("10.06", row[6]);
            Assert.Equal("0.50", row[8]);
            Assert.Equal("0.80", row[9]);
        });
    }
}
