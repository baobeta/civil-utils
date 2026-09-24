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
