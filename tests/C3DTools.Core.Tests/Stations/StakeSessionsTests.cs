using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Curves;
using C3DTools.Core.Presets;
using C3DTools.Core.Stations;
using Xunit;

namespace C3DTools.Core.Tests.Stations;

public class StakeGenerationSessionTests
{
    private static StakeGenerationSession Loaded(params string[] groups)
    {
        var s = new StakeGenerationSession(new ProjectPreset());
        s.SetSource("Alignment T1", 0, 250, groups);
        return s;
    }

    [Fact]
    public void Defaults_and_source()
    {
        var s = new StakeGenerationSession(new ProjectPreset());
        Assert.False(s.CanApply);
        Assert.Equal("Chưa chọn tuyến", s.SummaryText);

        s.SetSource("Alignment T1", 0, 250, new[] { "T1-COC" });

        Assert.Equal("Km0+000.00", s.FromText);
        Assert.Equal("Km0+250.00", s.ToText);
        Assert.Equal(20, s.StraightSpacing);
        Assert.Equal(10, s.CurveSpacing);
        Assert.Equal(60, s.HalfWidth);
        Assert.Equal(new[] { StakeGenerationSession.NewGroup, "T1-COC" }, s.GroupNames);
        Assert.Equal(1, s.GroupIndex);   // an existing group is chosen first
        Assert.True(s.CanApply);
        Assert.Equal("Bấm Xem trước để xem danh sách cọc", s.SummaryText);
    }

    [Theory]
    [InlineData("0+100", "0+050", false)]
    [InlineData("0+100", "0+260", false)]
    [InlineData("25,5", "Km0+200", true)]
    [InlineData("abc", "0+200", false)]
    public void Range_validation(string from, string to, bool valid)
    {
        var s = Loaded("G");
        s.FromText = from;
        s.ToText = to;

        Assert.Equal(valid, s.IsRangeValid);
        Assert.Equal(valid, s.CanApply);
    }

    [Fact]
    public void Default_range_is_valid_when_the_end_station_rounds_up()
    {
        var s = new StakeGenerationSession(new ProjectPreset());

        s.SetSource("Alignment T1", 0.004, 4561.226, new[] { "G" });   // shown as Km0+000.00 … Km4+561.23

        Assert.Equal("Km4+561.23", s.ToText);
        Assert.True(s.IsRangeValid);
        Assert.Equal(4561.226, s.To);
        Assert.Equal(0.004, s.From);
        var plan = s.Plan(null, null, null);
        Assert.Equal(4561.226, plan[plan.Count - 1].Station);
        Assert.All(plan, p => Assert.InRange(p.Station, 0.004, 4561.226));

        s.InsertMode = true;
        s.InsertStationsText = "Km4+561.23";
        Assert.True(s.IsInsertValid);
        Assert.Equal(4561.226, s.InsertStations[0]);
    }

    [Fact]
    public void Detail_start_follows_the_spacing_until_the_user_types_one()
    {
        var s = Loaded("G");
        Assert.Equal("20", s.DetailStartText);

        s.StraightSpacingText = "100";
        Assert.Equal("50", s.DetailStartText);
        Assert.Equal(new[] { 0.0, 50, 100, 150, 200, 250 }, s.Plan(null, null, null).Select(p => p.Station));

        s.DetailStartText = "Km0+010";
        s.StraightSpacingText = "40";
        Assert.Equal("Km0+010", s.DetailStartText);   // typed: no longer follows the spacing
        Assert.Equal(new[] { 0.0, 10, 50, 90, 100 }, s.Plan(null, null, null).Take(5).Select(p => p.Station));

        s.DetailStartText = "x";
        Assert.False(s.CanApply);
        Assert.Equal("Lý trình bắt đầu cọc C không hợp lệ", s.SummaryText);

        s.DetailStartText = "";   // cleared: back to the rule
        Assert.Equal("40", s.DetailStartText);
        Assert.True(s.CanApply);
    }

    [Fact]
    public void Curve_stakes_are_off_by_default_and_need_a_spacing_when_on()
    {
        var s = Loaded("G");
        var zone = new[] { new StationZone(100, 160) };
        Assert.False(s.DensifyCurves);
        Assert.Contains("20", StakeGenerationSession.SpacingChoices);
        Assert.Contains("100", StakeGenerationSession.SpacingChoices);

        s.CurveSpacingText = "abc";   // ignored while the option is off
        Assert.True(s.CanApply);
        Assert.DoesNotContain(110.0, s.Plan(null, zone, null).Select(p => p.Station));
        Assert.Contains(120.0, s.Plan(null, zone, null).Select(p => p.Station));   // the C stake inside the curve

        s.DensifyCurves = true;
        Assert.False(s.CanApply);
        s.CurveSpacingText = "10";
        Assert.True(s.CanApply);
        Assert.Contains(110.0, s.Plan(null, zone, null).Select(p => p.Station));
    }

    [Fact]
    public void H_options_change_the_generated_names()
    {
        var s = Loaded("G");
        s.ToText = "140";
        Assert.Equal(new[] { "Km0", "C1", "C2", "C3", "C4", "H1", "C5", "C6" }, s.Plan(null, null, null).Select(p => p.Name));

        s.SkipHundredPositions = false;
        Assert.True(s.IsStale);
        Assert.Equal(new[] { "Km0", "C1", "C2", "C3", "C4", "H1", "C6", "C7" }, s.Plan(null, null, null).Select(p => p.Name));

        s.NoHundreds = true;
        Assert.Equal(new[] { "Km0", "C1", "C2", "C3", "C4", "C5", "C6", "C7" }, s.Plan(null, null, null).Select(p => p.Name));
        Assert.True(s.LabelOptions.StationOnlyAtKm);
    }

    [Fact]
    public void New_group_needs_a_name()
    {
        var s = Loaded();
        Assert.True(s.IsNewGroup);
        Assert.False(s.CanApply);

        s.NewGroupName = "T1-COC";

        Assert.True(s.CanApply);
    }

    [Fact]
    public void Generate_plan_names_all_stakes_and_preview_marks_new_and_renamed()
    {
        var s = Loaded("G");
        s.FromText = "100";
        s.ToText = "200";
        var existing = new List<RouteStake>
        {
            new RouteStake(0, StakeRole.Km, "Km0"), new RouteStake(50, StakeRole.Detail, "X"), new RouteStake(150, StakeRole.Detail, "old"),
        };

        var plan = s.Plan(existing, null, null);
        s.SetPreview(plan, existing);

        Assert.Equal(new[] { "Km0", "C1", "H1", "C2", "C3", "C4", "C5", "H2" }, plan.Select(p => p.Name));
        Assert.Equal(new[] { 0.0, 50, 100, 120, 140, 160, 180, 200 }, plan.Select(p => p.Station));
        Assert.Equal(6, s.NewCount);
        Assert.Equal(1, s.RemovedCount);   // 150 was inside the range and is not a planned station
        Assert.Equal("đổi tên (X)", s.PreviewRows[1].Status);
        Assert.Equal("8 cọc: 6 mới, 1 bỏ", s.SummaryText);
        Assert.False(s.IsStale);
    }

    [Fact]
    public void Insert_mode_needs_an_existing_group_and_valid_stations()
    {
        var s = Loaded("G");
        s.InsertMode = true;
        Assert.False(s.CanApply);

        s.InsertStationsText = "Km0+025.5; 300";
        Assert.False(s.IsInsertValid);   // 300 is past the end

        s.InsertStationsText = "";
        s.AddInsertStation(25.5);
        s.AddInsertStation(30);
        Assert.Equal("Km0+025.50; Km0+030.00", s.InsertStationsText);
        Assert.True(s.CanApply);

        var plan = s.Plan(new[] { new RouteStake(0, StakeRole.Km, "Km0"), new RouteStake(20, StakeRole.Detail, "C1") }, null, null);
        Assert.Equal(new[] { "Km0", "C1", "Km0+025.50", "Km0+030" }, plan.Select(p => p.Name));

        s.SubStakeStyle = true;
        Assert.True(s.IsStale);
    }

    [Theory]
    [InlineData("Km1+020.5", 1020.5)]
    [InlineData("0+100", 100)]
    [InlineData("12,5", 12.5)]
    public void ParseStation(string text, double expected) => Assert.Equal(expected, StakeGenerationSession.ParseStation(text), 6);
}

public class StakeRenameSessionTests
{
    private static RouteStake D(double s, string name) => new RouteStake(s, StakeRole.Detail, name);

    private static StakeRenameSession Loaded()
    {
        var s = new StakeRenameSession(new ProjectPreset());
        s.SetStakes("T1-COC", new List<RouteStake>
        {
            new RouteStake(0, StakeRole.Km, "Km0"), D(20, "A"), D(40, "B"),
            new RouteStake(55, StakeRole.CurveKey, "TĐ7", StakeKind.Td, 1), D(60, "C"), new RouteStake(100, StakeRole.Hundred, "H1"),
        });
        return s;
    }

    [Fact]
    public void Loading_previews_the_default_names_at_once()
    {
        var s = Loaded();

        Assert.Equal(new[] { "Km0", "C1", "C2", "TĐ1", "C3", "H1" }, s.NewNames);
        Assert.Equal(4, s.ChangedCount);
        Assert.True(s.CanApply);
        Assert.Equal("4 / 6 cọc đổi tên", s.SummaryText);
        Assert.Equal("A (Km0+020.00)", s.StakeChoices[1]);
    }

    [Fact]
    public void Options_change_the_preview()
    {
        var s = Loaded();

        s.RenameCurveKeys = false;
        s.DetailPrefix = "D";
        s.FirstDetailNumberText = "5";
        s.KeepPrefixesText = "B";

        Assert.Equal(new[] { "Km0", "D5", "B", "TĐ7", "D6", "H1" }, s.NewNames);
    }

    [Fact]
    public void Range_limits_the_rename()
    {
        var s = Loaded();
        s.FromIndex = 2;
        s.ToIndex = 4;

        Assert.Equal(new[] { "Km0", "A", "C1", "TĐ1", "C2", "H1" }, s.NewNames);

        s.ToIndex = 1;
        Assert.False(s.CanApply);
        Assert.Equal("Cọc đầu phải đứng trước cọc cuối", s.SummaryText);
    }

    [Fact]
    public void Counting_H_positions_in_the_rename_dialog()
    {
        var s = Loaded();
        s.SkipHundredPositions = false;
        s.SetStakes("G", new List<RouteStake> { new RouteStake(80, StakeRole.Detail, "a"), new RouteStake(100, StakeRole.Hundred, "b"), new RouteStake(120, StakeRole.Detail, "c") });

        Assert.Equal(new[] { "C1", "H1", "C3" }, s.NewNames);
    }

    [Fact]
    public void Bad_numbers_block_apply()
    {
        var s = Loaded();
        s.FirstPiNumberText = "0";

        Assert.False(s.CanApply);
        Assert.Equal("Số thứ tự phải là số nguyên dương", s.SummaryText);
    }

    [Fact]
    public void No_hundreds_and_no_change()
    {
        var s = Loaded();
        s.NoHundreds = true;
        Assert.Equal("C4", s.NewNames[5]);

        var unchanged = new StakeRenameSession(new ProjectPreset());
        unchanged.SetStakes("G", new[] { new RouteStake(0, StakeRole.Km, "Km0"), D(20, "C1") });
        Assert.True(unchanged.WriteLabels);
        Assert.True(unchanged.CanApply);   // nothing to rename, but the names can still be drawn
        Assert.Equal("Không có tên nào thay đổi; Áp dụng sẽ ghi tên cọc lên bình đồ", unchanged.SummaryText);
        unchanged.WriteLabels = false;
        Assert.False(unchanged.CanApply);
        Assert.Equal("Không có tên nào thay đổi", unchanged.SummaryText);
    }
}
