using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using C3DTools.Core.Presets;
using C3DTools.Core.Sections;
using Xunit;

namespace C3DTools.Core.Tests.Sections;

public class SectionTableSessionTests
{
    private static SectionProfile Flat() => new SectionProfile(new List<(double, double)> { (-10, 0), (10, 0) });
    private static SectionProfile Ditch() => new SectionProfile(new List<(double, double)> { (-10, 0), (-2.5, 0), (-2, -1), (2, -1), (2.5, 0), (10, 0) });

    [Fact]
    public void Preset_rows_come_first_then_the_other_known_rows_off()
    {
        var preset = new ProjectPreset();
        preset.SectionTable.Rows = new List<TableRowSpec> { new TableRowSpec("FillArea", "Đắp", 1), new TableRowSpec("Nope", "?", 2) };
        var session = new SectionTableSession(preset);

        Assert.Equal(SectionTableBuilder.FillArea, session.Rows[0].Key);
        Assert.True(session.Rows[0].Include);
        Assert.All(session.Rows.Skip(1), r => Assert.False(r.Include));
        Assert.Equal(SectionTableBuilder.KnownRows().Count, session.Rows.Count);
        Assert.Single(session.PresetMessages);
    }

    [Fact]
    public void Needs_views_and_a_ground_section_to_apply()
    {
        var session = new SectionTableSession(new ProjectPreset());
        Assert.False(session.CanApply);
        Assert.Equal("Chưa chọn trắc ngang", session.SummaryText);

        session.SetSource("3 trắc ngang", 3);
        Assert.False(session.CanApply);
        session.SetSections(new[] { "EG" }, new[] { "EG", "Corridor - (1) Top" });
        Assert.Equal("EG", session.GroundSection);
        Assert.Equal("Corridor - (1) Top", session.DesignSection);
        Assert.True(session.CanApply);

        session.TextHeightText = "2,5";
        Assert.True(session.IsTextHeightValid);
        session.TextHeightText = "x";
        Assert.False(session.CanApply);
    }

    [Fact]
    public void Design_defaults_to_none_when_only_the_ground_is_listed_and_remembered_names_win()
    {
        var session = new SectionTableSession(new ProjectPreset());
        session.SetSource("1", 1);
        session.SetSections(new[] { "EG", "TN2" }, new[] { "EG" });
        Assert.Null(session.DesignSection);

        var other = new SectionTableSession(new ProjectPreset());
        other.SetSections(new[] { "EG", "TN2" }, new[] { "EG", "FG" }, "TN2", "FG");
        Assert.Equal("TN2", other.GroundSection);
        Assert.Equal("FG", other.DesignSection);
    }

    [Fact]
    public void Heights_scale_to_the_plot_when_the_preset_says_so()
    {
        var preset = new ProjectPreset();
        preset.SheetLayout.Scale = 200;
        var session = new SectionTableSession(preset);

        // 2.5 mm on paper at 1:200 = 0.5 m; row 8 mm = 1.6 m.
        Assert.Equal(0.5, session.DrawingTextHeight, 9);
        Assert.Equal(1.6, session.DrawingRowHeight, 9);
        Assert.Equal("Chiều cao chữ (mm giấy, 1:200)", session.TextHeightLabel);

        preset.SectionTable.ScaleTextToPlot = false;
        var plain = new SectionTableSession(preset);
        Assert.Equal(2.5, plain.DrawingTextHeight, 9);
        Assert.Equal("Chiều cao chữ", plain.TextHeightLabel);
    }

    [Fact]
    public void Profile_table_keeps_drawing_unit_heights_by_default()
    {
        var preset = new ProjectPreset();
        Assert.Equal(2.5, new C3DTools.Core.Profiles.ProfileTableSession(preset).DrawingTextHeight, 9);
        preset.ProfileTable.ScaleTextToPlot = true;
        Assert.Equal(0.5, new C3DTools.Core.Profiles.ProfileTableSession(preset).DrawingTextHeight, 9);
    }

    [Fact]
    public void Build_orders_by_station_and_sums_areas_in_the_summary()
    {
        var session = new SectionTableSession(new ProjectPreset());
        session.SetSource("2", 2);
        session.SetSections(new[] { "EG" }, new[] { "FG" });
        var changed = new List<string>();
        ((INotifyPropertyChanged)session).PropertyChanged += (s, e) => changed.Add(e.PropertyName);

        session.Build(new[] { new SectionInput("C2", 20, Flat(), Ditch()), new SectionInput("C1", 0, Flat(), Ditch()) });

        Assert.Equal(new[] { "C1", "C2" }, session.Models.Select(m => m.Name));
        Assert.Equal("2 trắc ngang, tổng diện tích đào 9.00, đắp 0.00", session.SummaryText);
        Assert.Contains(nameof(SectionTableSession.SummaryText), changed);
        Assert.Equal(2, session.Export.Rows.Count);
        Assert.False(session.IsStale);

        session.Rows[0].Include = false;
        Assert.True(session.IsStale);
    }
}

public class SheetArrangeSessionTests
{
    private static List<(double, double)> Sizes(int n) => Enumerable.Range(0, n).Select(_ => (20.0, 20.0)).ToList();

    [Fact]
    public void Fields_start_from_the_preset_and_the_summary_counts_sheets()
    {
        var session = new SheetArrangeSession(new ProjectPreset());
        Assert.Equal("420", session.WidthText);
        Assert.Equal("200", session.ScaleText);
        Assert.False(session.CanApply);

        session.SetViews("7 trắc ngang", Sizes(7), Enumerable.Range(0, 7).Select(i => i * 20.0));

        Assert.True(session.CanApply);
        Assert.Equal("7 trắc ngang → 2 tờ", session.SummaryText);
        var plan = session.Plan();
        Assert.Equal("TRẮC NGANG – Tờ 2/2 – Km0+120.00", session.Title(plan, 1));
        Assert.Equal("TRẮC NGANG – Tờ 1/2 – Km0+000.00 … Km0+100.00", session.Title(plan, 0));
    }

    [Fact]
    public void Comma_decimals_are_accepted_and_bad_fields_block_apply()
    {
        var session = new SheetArrangeSession(new ProjectPreset());
        session.SetViews("1", new List<(double, double)> { (10, 10) }, new[] { 0.0 });
        var raised = new List<string>();
        session.PropertyChanged += (s, e) => raised.Add(e.PropertyName);

        session.MarginLeftText = "12,5";
        Assert.True(session.IsMarginLeftValid);
        Assert.Contains(nameof(SheetArrangeSession.IsMarginLeftValid), raised);
        Assert.Equal(12.5, session.Layout().MarginLeft);

        session.ColumnsText = "0";
        Assert.False(session.IsColumnsValid);
        Assert.False(session.CanApply);
        session.ColumnsText = "4";
        session.RowsText = "1";
        Assert.True(session.CanApply);
        Assert.Equal("1 trắc ngang → 1 tờ", session.SummaryText);
    }

    [Fact]
    public void Oversize_views_are_reported()
    {
        var session = new SheetArrangeSession(new ProjectPreset());
        // 1:200 cell width = (385 − 20) / 3 · 0.2 = 24.33 m.
        session.SetViews("1", new List<(double, double)> { (30, 10) }, new[] { 0.0 });

        Assert.Contains("lớn hơn ô", session.SummaryText);
        Assert.True(session.CanApply);
    }

    [Fact]
    public void Margins_wider_than_the_paper_explain_why()
    {
        var session = new SheetArrangeSession(new ProjectPreset());
        session.SetViews("1", Sizes(1), new[] { 0.0 });
        session.MarginLeftText = "400";

        Assert.False(session.CanApply);
        Assert.Contains("rộng hơn khổ giấy", session.SummaryText);
    }
}
