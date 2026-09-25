using System;
using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Presets;
using C3DTools.Core.Profiles;
using C3DTools.Core.Sections;
using Xunit;

namespace C3DTools.Core.Tests.Sections;

public class SectionTableLayoutTests
{
    private static SectionProfile Line(params double[] xz)
    {
        var points = new List<(double, double)>();
        for (var i = 0; i + 1 < xz.Length; i += 2) points.Add((xz[i], xz[i + 1]));
        return new SectionProfile(points);
    }

    private static SectionTableModel Model(IEnumerable<TableRowSpec> rows = null) =>
        SectionTableBuilder.Build("C1", 0, Line(-10, 0, 10, 0), Line(-10, 0, -2.5, 0, -2, -1, 2, -1, 2.5, 0, 10, 0),
            rows ?? SectionTableBuilder.KnownRows());

    // View from −10 to 10 at x = 100 + offset; table top at y = 0.
    private static SectionTableLayout Layout(SectionTableModel model, double h, bool rotate = true) =>
        SectionTableLayout.Build(model, o => 100 + o, 0, 90, 110, 8, h, 15, rotate);

    [Fact]
    public void Columns_too_close_for_their_text_are_skipped()
    {
        // h = 1: rotated text needs about 1.35 between ticks; −2.5 → −2 and 2 → 2.5 are only 0.5 apart.
        var layout = Layout(Model(), 1);

        Assert.Equal(new[] { -10, -2.5, 0, 2, 10.0 }, layout.KeptOffsets);
        Assert.Equal(new[] { -2, 2.5 }, layout.DroppedOffsets);
        Assert.Equal(2, layout.SkippedColumns);
    }

    [Fact]
    public void Kept_texts_never_overlap_in_a_row()
    {
        var layout = Layout(Model(), 1);
        var rotated = layout.Texts.Where(t => Math.Abs(t.Rotation - Math.PI / 2) < 1e-9).GroupBy(t => Math.Round(t.Y, 6));
        foreach (var row in rotated)
        {
            var xs = row.Select(t => t.X).OrderBy(x => x).ToList();
            for (var i = 1; i < xs.Count; i++) Assert.True(xs[i] - xs[i - 1] >= 1.0 + 0.1 - 1e-9, $"texts at {xs[i - 1]} and {xs[i]}");
        }
    }

    [Fact]
    public void Partial_distance_is_measured_between_kept_columns()
    {
        var layout = Layout(Model(new[] { new TableRowSpec(SectionTableBuilder.GroundElevation, "TN", 2), new TableRowSpec("Distance", "KC", 2) }), 1);

        // The KC row is the second 8-high row (mid y = −12); 2.50 and 2.00 do not fit across their spans and are rotated.
        var texts = layout.Texts.Where(t => System.Math.Abs(t.Y + 12) < 1e-9 && t.Text != "KC").OrderBy(t => t.X).ToList();
        Assert.Equal(new[] { "7.50", "2.50", "2.00", "8.00" }, texts.Select(t => t.Text));
        Assert.Equal(new[] { 0, System.Math.PI / 2, System.Math.PI / 2, 0 }, texts.Select(t => t.Rotation));
    }

    [Fact]
    public void Row_heights_follow_the_profile_table_rules()
    {
        var layout = Layout(Model(), 1);

        // Rotated elevation rows: longest text "-1.00" = 5 · 0.7 + 1 = 4.5 < 8 → 8; others at least 2.6 → 8.
        Assert.All(layout.RowHeights, rh => Assert.Equal(8, rh, 9));
        Assert.Equal(-48, layout.Bottom, 9);
        Assert.Equal(75, layout.Left, 9);
    }

    [Fact]
    public void Area_text_is_centred_across_the_table()
    {
        var layout = Layout(Model(new[] { new TableRowSpec(SectionTableBuilder.CutArea, "Đào", 2) }), 1);

        var area = layout.Texts.Single(t => t.Text == "4.50");
        Assert.Equal(100, area.X, 9);
        Assert.Equal(-4, area.Y, 9);
    }

    [Fact]
    public void Horizontal_text_is_thinned_by_its_width()
    {
        var layout = Layout(Model(new[] { new TableRowSpec(SectionTableBuilder.GroundElevation, "TN", 2) }), 1, rotate: false);

        // "0.00" is 2.8 wide (+0.1 each side): the ends and the centre go first, and every ditch vertex is within
        // 3 of one of them or of each other.
        Assert.Equal(new[] { -10, 0, 10.0 }, layout.KeptOffsets);
        Assert.DoesNotContain(layout.Texts, t => t.Rotation != 0);
    }

    [Fact]
    public void Centre_column_is_kept_over_a_close_ground_vertex_on_its_left()
    {
        // Ground-only vertex at −0.5 is within a text width of the centre; left to right it would win, by priority it loses.
        var model = SectionTableBuilder.Build("C1", 0, Line(-10, 0, -0.5, 0.2, 10, 0), null,
            new[] { new TableRowSpec(SectionTableBuilder.GroundElevation, "TN", 2) });
        var layout = Layout(model, 1);

        Assert.Contains(0.0, layout.KeptOffsets);
        Assert.Equal(new[] { -0.5 }, layout.DroppedOffsets);
    }

    [Fact]
    public void Design_toe_beats_a_nearby_ground_vertex()
    {
        // Ground vertex at −5.3, design toe at −5: the toe is kept although the ground vertex comes first.
        var model = SectionTableBuilder.Build("C1", 0, Line(-10, 0, -5.3, 0.2, 10, 0), Line(-5, 0.1, -4, 1, 4, 1, 5, 0.1),
            new[] { new TableRowSpec(SectionTableBuilder.GroundElevation, "TN", 2) });
        var layout = Layout(model, 1);

        Assert.Contains(-5.0, layout.KeptOffsets);
        Assert.Contains(-5.3, layout.DroppedOffsets);
    }

    [Fact]
    public void Area_rows_have_no_inner_edges()
    {
        var layout = Layout(Model(new[] { new TableRowSpec(SectionTableBuilder.CutArea, "Đào", 2) }), 1);

        // Top and bottom borders, and the verticals at the label column's left, the table's left and its right only.
        Assert.Equal(new[] { 75, 90, 110.0 }, layout.Lines.Where(l => l.X1 == l.X2).Select(l => l.X1).OrderBy(x => x));
    }

    [Fact]
    public void Offsets_outside_the_view_are_skipped()
    {
        var model = SectionTableBuilder.Build("C1", 0, Line(-20, 0, 20, 0), null, new[] { new TableRowSpec(SectionTableBuilder.GroundElevation, "TN", 2) });
        var layout = Layout(model, 1);

        Assert.Equal(new[] { 0.0 }, layout.KeptOffsets);
        Assert.Equal(new[] { -20, 20.0 }, layout.DroppedOffsets);
    }
}
