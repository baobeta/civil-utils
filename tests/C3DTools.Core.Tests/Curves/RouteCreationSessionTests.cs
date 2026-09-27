using System.Linq;
using C3DTools.Core.Curves;
using C3DTools.Core.Presets;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class RouteCreationSessionTests
{
    private static RouteCreationSession Loaded()
    {
        var s = new RouteCreationSession(new ProjectPreset { DesignSpeed = 60 });
        s.SetDrawing(new[] { "Tuyen1", "YTC - 1" }, new[] { "Basic", "TCVN_Tuyen" }, "TCVN_Tuyen", new[] { "All Labels", "TCVN" }, "TCVN",
            new[] { "EG", "" }, new[] { "Mat cat A" });
        return s;
    }

    [Fact]
    public void Defaults_pick_a_free_name_and_the_preferred_styles()
    {
        var s = Loaded();

        Assert.Equal("Tuyen2", s.Name);   // Tuyen1 exists
        Assert.Equal(1, s.StyleIndex);
        Assert.Equal(1, s.LabelSetIndex);
        Assert.Equal(new[] { RouteCreationSession.NoSurface, "EG" }, s.SurfaceNames);
        Assert.Null(s.Surface);
        Assert.Equal(1000, s.Scale);
        Assert.Equal(2.5, s.TextHeight, 9);
        Assert.Equal(0, s.StartStation);
        Assert.Equal("TUYEN", s.LayerName);
        Assert.True(s.OpenCurveDesign);
        Assert.False(s.CanApply);
        Assert.Equal("Bấm Theo polyline… hoặc Chỉ điểm… để có tim tuyến", s.SummaryText);
    }

    [Fact]
    public void Picked_points_or_a_polyline_give_the_source()
    {
        var s = Loaded();

        s.SetPickedPoints(new[] { new PlanPoint(0, 0), new PlanPoint(0, 0), new PlanPoint(100, 0), new PlanPoint(100, 50) });

        Assert.Equal(3, s.PickedPoints.Count);
        Assert.False(s.IsPolyline);
        Assert.True(s.CanApply);
        Assert.Equal("Tạo alignment Tuyen2 từ 3 điểm đã chỉ", s.SummaryText);

        s.SetPolylineSource("Polyline (5 đỉnh)");
        Assert.True(s.IsPolyline);
        Assert.Empty(s.PickedPoints);
        Assert.Equal("Polyline (5 đỉnh)", s.SourceText);
    }

    [Theory]
    [InlineData("Name", "tuyen1", "Đã có alignment tên tuyen1")]
    [InlineData("Name", " ", "Nhập tên tuyến")]
    [InlineData("ScaleText", "0", "Tỉ lệ phải là số lớn hơn 0")]
    [InlineData("StartStationText", "x", "Lý trình đầu không hợp lệ")]
    [InlineData("LayerName", "a*b", "Tên layer không hợp lệ")]
    public void Invalid_fields_block_apply(string field, string value, string summary)
    {
        var s = Loaded();
        s.SetPolylineSource("Polyline (2 đỉnh)");

        typeof(RouteCreationSession).GetProperty(field).SetValue(s, value);

        Assert.False(s.CanApply);
        Assert.Equal(summary, s.SummaryText);
    }

    [Fact]
    public void Scale_sets_the_text_height_and_start_accepts_km_text()
    {
        var s = Loaded();
        s.ScaleText = "500";
        s.StartStationText = "Km1+250";

        Assert.Equal(1.25, s.TextHeight, 9);
        Assert.Equal("Chữ cọc, khung cong: 1.25 (2.5 mm ở 1/500)", s.TextHeightText);
        Assert.Equal(1250, s.StartStation);
    }

    [Fact]
    public void Assemblies_from_a_section_file_are_offered_and_imported()
    {
        var s = Loaded();

        s.SetFileAssemblies(@"C:\mau\matcat.dwg", new[] { "Mat cat A", "Hai mai 7m", "Mot mai" });

        Assert.Equal(new[] { RouteCreationSession.NoAssembly, "Mat cat A", "Hai mai 7m", "Mot mai" }, s.AssemblyNames);
        Assert.Equal("Mat cat A", s.Assembly);   // first of the file; already in the drawing
        Assert.False(s.AssemblyFromFile);
        Assert.Equal(new[] { "Hai mai 7m", "Mot mai" }, s.AssembliesToImport);

        s.LoadAllAssemblies = false;
        s.AssemblyIndex = 2;
        Assert.True(s.AssemblyFromFile);
        Assert.Equal(new[] { "Hai mai 7m" }, s.AssembliesToImport);

        s.AssemblyIndex = 0;
        Assert.Empty(s.AssembliesToImport);
    }

    [Fact]
    public void Speeds_come_from_the_preset_rules()
    {
        var preset = new ProjectPreset { DesignSpeed = 50, CurveRules = new CurveRules() };
        preset.CurveRules.MinRadius.Add(new SpeedRadiusRule { DesignSpeed = 60 });

        var s = new RouteCreationSession(preset);

        Assert.Equal(new[] { 50.0, 60 }, s.AvailableSpeeds);
        Assert.Equal(50, s.DesignSpeed);
    }
}
