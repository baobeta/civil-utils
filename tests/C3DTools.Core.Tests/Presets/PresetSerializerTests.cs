using System.Collections.Generic;
using System.IO;
using System.Linq;
using C3DTools.Core.Curves;
using C3DTools.Core.Drainage;
using C3DTools.Core.Presets;
using Xunit;

namespace C3DTools.Core.Tests.Presets;

public class PresetSerializerTests
{
    [Fact]
    public void Round_trips_a_preset()
    {
        var preset = new ProjectPreset { Name = "Dự án QL1A", Version = "1.2", StationDecimals = 3 };
        preset.Styles["Alignment"] = "Tim tuyến";
        preset.PointFile.Delimiter = ';';
        preset.PipeRules = new PipeRules(0.003, 0.7);

        var back = PresetSerializer.Load(PresetSerializer.Save(preset));

        Assert.Equal("Dự án QL1A", back.Name);
        Assert.Equal("1.2", back.Version);
        Assert.Equal(3, back.StationDecimals);
        Assert.Equal("Tim tuyến", back.Styles["Alignment"]);
        Assert.Equal(';', back.PointFile.Delimiter);
        Assert.Equal(0.003, back.PipeRules.MinSlope);
        Assert.Equal(0.7, back.PipeRules.MinCover);
    }

    [Fact]
    public void Round_trips_curve_rules()
    {
        var preset = new ProjectPreset { Name = "Dự án QL1A", DesignSpeed = 60 };
        preset.CurveRules = new CurveRules
        {
            Source = "TCVN 4054:2005",
            MinRadius = new List<SpeedRadiusRule>
            {
                new SpeedRadiusRule { DesignSpeed = 60, MinRadius = 125, NormalRadius = 250 },
            },
            MinSpiral = new List<RadiusRangeRule>
            {
                new RadiusRangeRule { DesignSpeed = 60, RadiusFrom = 125, RadiusTo = 250, Value = 50 },
            },
            Widening = new List<RadiusRangeRule>
            {
                new RadiusRangeRule { RadiusFrom = 100, RadiusTo = 150, Value = 0.9 },
            },
        };
        preset.CurveBox.TextHeight = 3.0;

        var back = PresetSerializer.Load(PresetSerializer.Save(preset));

        Assert.Equal(60, back.DesignSpeed);
        Assert.Equal("TCVN 4054:2005", back.CurveRules.Source);
        Assert.Single(back.CurveRules.MinRadius);
        Assert.Equal(125, back.CurveRules.MinRadius[0].MinRadius);
        Assert.Equal(250, back.CurveRules.MinRadius[0].NormalRadius);
        Assert.Single(back.CurveRules.MinSpiral);
        Assert.Equal(50, back.CurveRules.MinSpiral[0].Value);
        Assert.Single(back.CurveRules.Widening);
        Assert.Equal(0.9, back.CurveRules.Widening[0].Value);
        Assert.Equal(3.0, back.CurveBox.TextHeight);
    }

    [Fact]
    public void Round_trips_the_0_3_sections()
    {
        var preset = new ProjectPreset();
        preset.FontConversion.TargetFont = "Times New Roman";
        preset.LayerMap.Add(new LayerMapRule { Pattern = "*COC*", Layer = "TK_COC", Color = 1, Linetype = "DASHED" });
        preset.StakeTable.XDecimals = 4;
        preset.StakeTable.IncludeZ = false;
        preset.VerticalRules.Source = "TCVN 4054:2005";
        preset.VerticalRules.MinRadiusCrest.Add(new SpeedValueRule { DesignSpeed = 60, Value = 2500 });
        preset.VerticalRules.MaxGrade.Add(new SpeedValueRule { DesignSpeed = 60, Value = 7 });
        preset.ProfileTable.Rows.RemoveAt(0);
        preset.ProfileTable.RowHeight = 10;
        preset.SectionTable.Rows.Add(new TableRowSpec("Note", "Ghi chú", 0));
        preset.SheetLayout.Columns = 4;
        preset.Vn2000.Provinces.Add(new Vn2000Province { Province = "Hà Nội", MeridianDeg = 105, MeridianMin = 0 });
        preset.Surface.MaxEdgeLength = 80;
        preset.Culvert.ElevationDecimals = 3;

        var back = PresetSerializer.Load(PresetSerializer.Save(preset));

        Assert.Equal(1, back.SchemaVersion);
        Assert.Equal("Times New Roman", back.FontConversion.TargetFont);
        var rule = Assert.Single(back.LayerMap);
        Assert.Equal(("*COC*", "TK_COC", (short)1, "DASHED"), (rule.Pattern, rule.Layer, rule.Color, rule.Linetype));
        Assert.Equal(4, back.StakeTable.XDecimals);
        Assert.False(back.StakeTable.IncludeZ);
        Assert.Equal("TCVN 4054:2005", back.VerticalRules.Source);
        Assert.Equal(2500, Assert.Single(back.VerticalRules.MinRadiusCrest).Value);
        Assert.Equal(7, Assert.Single(back.VerticalRules.MaxGrade).Value);
        Assert.Empty(back.VerticalRules.MinRadiusSag);
        Assert.Equal(7, back.ProfileTable.Rows.Count);   // replaced, not appended to the defaults
        Assert.Equal("Distance", back.ProfileTable.Rows[0].Key);
        Assert.Equal(10, back.ProfileTable.RowHeight);
        Assert.Equal(6, back.SectionTable.Rows.Count);
        Assert.Equal("Ghi chú", back.SectionTable.Rows[5].Label);
        Assert.Equal(4, back.SheetLayout.Columns);
        var province = Assert.Single(back.Vn2000.Provinces);
        Assert.Equal(("Hà Nội", 105, 0), (province.Province, province.MeridianDeg, province.MeridianMin));
        Assert.Equal(80, back.Surface.MaxEdgeLength);
        Assert.Equal(3, back.Culvert.ElevationDecimals);
    }

    [Fact]
    public void A_preset_without_the_0_3_sections_gets_their_defaults()
    {
        var preset = PresetSerializer.Load("{ \"SchemaVersion\": 1, \"Name\": \"A\" }");

        Assert.Equal("Arial", preset.FontConversion.TargetFont);
        Assert.Empty(preset.LayerMap);
        Assert.Equal((3, 3, 2, true), (preset.StakeTable.XDecimals, preset.StakeTable.YDecimals, preset.StakeTable.ZDecimals, preset.StakeTable.IncludeZ));
        Assert.Empty(preset.VerticalRules.MinRadiusCrest);
        Assert.Empty(preset.VerticalRules.MinRadiusSag);
        Assert.Empty(preset.VerticalRules.MinLength);
        Assert.Empty(preset.VerticalRules.MaxGrade);
        Assert.Equal(
            new[] { "Tên cọc", "Khoảng cách lẻ", "Khoảng cách cộng dồn", "Lý trình", "Cao độ tự nhiên", "Cao độ thiết kế", "Chênh cao", "Độ dốc dọc" },
            preset.ProfileTable.Rows.Select(r => r.Label));
        Assert.Equal((8.0, 2.5), (preset.ProfileTable.RowHeight, preset.ProfileTable.TextHeight));
        Assert.Equal(
            new[] { "Cao độ tự nhiên", "Cao độ thiết kế", "Khoảng cách lẻ", "Diện tích đào", "Diện tích đắp" },
            preset.SectionTable.Rows.Select(r => r.Label));
        Assert.Equal((420.0, 297.0), (preset.SheetLayout.Width, preset.SheetLayout.Height));
        Assert.Empty(preset.Vn2000.Provinces);
        Assert.Equal(50, preset.Surface.MaxEdgeLength);
        Assert.Equal(2, preset.Culvert.ElevationDecimals);
    }

    [Fact]
    public void Default_table_rows_are_not_duplicated_by_a_round_trip()
    {
        var back = PresetSerializer.Load(PresetSerializer.Save(new ProjectPreset()));

        Assert.Equal(8, back.ProfileTable.Rows.Count);
        Assert.Equal(5, back.SectionTable.Rows.Count);
    }

    [Fact]
    public void Loads_the_bundled_preset_with_default_sections()
    {
        var preset = PresetSerializer.Load(File.ReadAllText(Path.Combine("Resources", "tcvn4054.preset.json")));

        Assert.Equal(8, preset.ProfileTable.Rows.Count);
        Assert.Equal("Arial", preset.FontConversion.TargetFont);
    }

    [Fact]
    public void Style_keys_are_case_insensitive_after_load()
    {
        var json = "{ \"SchemaVersion\": 1, \"Name\": \"A\", \"Styles\": { \"Alignment\": \"Tim tuyến\" } }";

        Assert.Equal("Tim tuyến", PresetSerializer.Load(json).Styles["alignment"]);
    }

    [Fact]
    public void Pipe_rules_stay_null_when_not_configured()
    {
        Assert.Null(PresetSerializer.Load("{ \"SchemaVersion\": 1, \"Name\": \"A\" }").PipeRules);
    }

    [Fact]
    public void Loads_the_tcvn4054_preset_resource()
    {
        var json = File.ReadAllText(Path.Combine("Resources", "tcvn4054.preset.json"));

        var preset = PresetSerializer.Load(json);

        Assert.Equal(7, preset.CurveRules.MinRadius.Count);
        var v60 = preset.CurveRules.MinRadius.Single(r => r.DesignSpeed == 60);
        Assert.Equal(125, v60.MinRadius);
        Assert.Equal(250, v60.NormalRadius);
    }

    [Fact]
    public void Rejects_missing_schema_version()
    {
        var ex = Assert.Throws<PresetException>(() => PresetSerializer.Load("{ \"Name\": \"A\" }"));
        Assert.Contains("SchemaVersion", ex.Message);
    }

    [Fact]
    public void Rejects_newer_schema_version()
    {
        var ex = Assert.Throws<PresetException>(() => PresetSerializer.Load("{ \"SchemaVersion\": 99 }"));
        Assert.Contains("mới hơn", ex.Message);
    }

    [Fact]
    public void Wraps_invalid_json()
    {
        Assert.Throws<PresetException>(() => PresetSerializer.Load("{ not json"));
    }

    [Fact]
    public void ReplaceSection_changes_one_key_and_keeps_unknown_ones()
    {
        var json = "{ \"SchemaVersion\": 1, \"Name\": \"Cty A\", \"CompanyExtra\": { \"Keep\": true }, \"LayerMap\": [] }";

        var result = PresetSerializer.ReplaceSection(json, "LayerMap",
            new List<LayerMapRule> { new LayerMapRule { Pattern = "*COC*", Layer = "TK_COC", Color = 1 } });

        var obj = Newtonsoft.Json.Linq.JObject.Parse(result);
        Assert.True((bool)obj["CompanyExtra"]["Keep"]);
        Assert.Equal("Cty A", (string)obj["Name"]);
        var preset = PresetSerializer.Load(result);
        Assert.Equal("TK_COC", Assert.Single(preset.LayerMap).Layer);
    }

    [Fact]
    public void ReplaceSection_rejects_an_invalid_preset()
    {
        Assert.Throws<PresetException>(() => PresetSerializer.ReplaceSection("{ \"Name\": \"x\" }", "LayerMap", new List<LayerMapRule>()));
        Assert.Throws<PresetException>(() => PresetSerializer.ReplaceSection("not json", "LayerMap", new List<LayerMapRule>()));
    }
}
