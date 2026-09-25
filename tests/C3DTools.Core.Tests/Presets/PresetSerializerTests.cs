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
}
