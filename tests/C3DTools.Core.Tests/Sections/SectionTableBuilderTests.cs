using System;
using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Presets;
using C3DTools.Core.Sections;
using Xunit;

namespace C3DTools.Core.Tests.Sections;

public class SectionTableBuilderTests
{
    private static SectionProfile Line(params double[] xz)
    {
        var points = new List<(double, double)>();
        for (var i = 0; i + 1 < xz.Length; i += 2) points.Add((xz[i], xz[i + 1]));
        return new SectionProfile(points);
    }

    private static SectionProfile FlatGround() => Line(-10, 0, 10, 0);

    // A ditch 1 m deep: 4 m wide at the bottom (−2…2), side slopes 1:0.5, so 5 m wide at the top (−2.5…2.5).
    private static SectionProfile Ditch() => Line(-10, 0, -2.5, 0, -2, -1, 2, -1, 2.5, 0, 10, 0);

    private static List<TableRowSpec> AllRows() => SectionTableBuilder.KnownRows().ToList();

    [Fact]
    public void Trapezoid_ditch_below_flat_ground_is_all_cut()
    {
        var areas = SectionTableBuilder.Areas(FlatGround(), Ditch());

        // (bottom 4 + top 4 + 2·0.5) / 2 · depth 1 = 4.5 m².
        Assert.Equal(4.5, areas.Cut, 9);
        Assert.Equal(0, areas.Fill, 9);
        Assert.Equal(1, areas.Loops);
    }

    [Fact]
    public void Embankment_above_flat_ground_is_all_fill()
    {
        // Top 8 m (−4…4) at +1, slopes 1:2 down to ±6: (8 + 12) / 2 · 1 = 10 m².
        var areas = SectionTableBuilder.Areas(FlatGround(), Line(-6, 0, -4, 1, 4, 1, 6, 0));

        Assert.Equal(0, areas.Cut, 9);
        Assert.Equal(10, areas.Fill, 9);
    }

    [Fact]
    public void Crossing_lines_give_both_cut_and_fill()
    {
        // Design −1 at 0 rising to +1 at 10 over flat ground: crosses at 5, each side a triangle 5 · 1 / 2.
        var areas = SectionTableBuilder.Areas(Line(0, 0, 10, 0), Line(0, -1, 10, 1));

        Assert.Equal(2.5, areas.Cut, 9);
        Assert.Equal(2.5, areas.Fill, 9);
        Assert.Equal(2, areas.Loops);
    }

    [Fact]
    public void Crossing_between_vertices_of_sloping_ground()
    {
        // Ground 0 → 2 over 0…4; design flat at 1 crosses it at 2: a 2 × 1 triangle on each side.
        var areas = SectionTableBuilder.Areas(Line(0, 0, 4, 2), Line(0, 1, 4, 1));

        Assert.Equal(1, areas.Fill, 9);   // 0…2, design above ground
        Assert.Equal(1, areas.Cut, 9);    // 2…4, design below ground
    }

    [Fact]
    public void Only_the_common_offsets_count_and_missing_design_gives_zero()
    {
        // Design only −1…1 at −1 over flat ground −10…10: 2 m².
        Assert.Equal(2, SectionTableBuilder.Areas(FlatGround(), Line(-1, -1, 1, -1)).Cut, 9);
        var none = SectionTableBuilder.Areas(FlatGround(), null);
        Assert.Equal(0, none.Cut);
        Assert.Equal(0, none.Fill);
    }

    [Fact]
    public void Vertical_step_in_the_design_is_kept()
    {
        // Curb: design −0.5 on −2…0, steps to −0.2 at 0, stays to 2: 2·0.5 + 2·0.2 = 1.4 m² cut.
        var areas = SectionTableBuilder.Areas(Line(-2, 0, 2, 0), Line(-2, -0.5, 0, -0.5, 0, -0.2, 2, -0.2));

        Assert.Equal(1.4, areas.Cut, 9);
        Assert.Equal(1, areas.Loops);
    }

    [Fact]
    public void Shoelace_of_a_unit_square()
    {
        Assert.Equal(1, Math.Abs(SectionTableBuilder.Shoelace(new List<(double, double)> { (0, 0), (1, 0), (1, 1), (0, 1) })), 12);
    }

    [Fact]
    public void Columns_are_the_union_of_offsets_with_interpolated_elevations()
    {
        var model = SectionTableBuilder.Build("C5", 100, Line(-10, 1, 10, 3), Line(-2.5, 2, 2.5, 2), AllRows());

        Assert.Equal(new[] { -10, -2.5, 2.5, 10.0 }, model.Offsets);
        var ground = model.Rows.Single(r => r.Key == SectionTableBuilder.GroundElevation);
        var design = model.Rows.Single(r => r.Key == SectionTableBuilder.DesignElevation);
        Assert.Equal(new[] { "1.00", "1.75", "2.25", "3.00" }, ground.Cells);
        Assert.Equal(new[] { "", "2.00", "2.00", "" }, design.Cells);
        Assert.Equal(new[] { "10.00", "2.50", "2.50", "10.00" }, model.Rows.Single(r => r.Key == SectionTableBuilder.Offset).Cells);
    }

    [Fact]
    public void Partial_distances_and_whole_width_area_cells()
    {
        var model = SectionTableBuilder.Build("C1", 0, FlatGround(), Ditch(), AllRows());

        var gaps = model.Rows.Single(r => r.Key == SectionTableBuilder.PartialDistance);
        Assert.Equal(SectionTableRowKind.Span, gaps.Kind);
        Assert.Equal(new[] { "7.50", "0.50", "4.00", "0.50", "7.50" }, gaps.Spans.Select(s => s.Line1));
        var cut = model.Rows.Single(r => r.Key == SectionTableBuilder.CutArea).Spans.Single();
        Assert.Equal("4.50", cut.Line1);
        Assert.Equal(-10, cut.From);
        Assert.Equal(10, cut.To);
        Assert.Equal("0.00", model.Rows.Single(r => r.Key == SectionTableBuilder.FillArea).Spans.Single().Line1);
        Assert.Equal(-1, model.CentreDifference.Value, 9);
    }

    [Fact]
    public void Preset_keys_are_read_case_insensitively_and_unknown_ones_are_reported()
    {
        var rows = new List<TableRowSpec>
        {
            new TableRowSpec("distance", "KC lẻ", 1),
            new TableRowSpec("Volume", "Khối lượng", 2),
            new TableRowSpec("cutarea", "Đào", 3),
        };
        var model = SectionTableBuilder.Build("C1", 0, FlatGround(), Ditch(), rows);

        Assert.Equal(new[] { SectionTableBuilder.PartialDistance, SectionTableBuilder.CutArea }, model.Rows.Select(r => r.Key));
        Assert.Equal("7.5", model.Rows[0].Spans[0].Line1);
        Assert.Equal("4.500", model.Rows[1].Spans[0].Line1);
        Assert.Single(model.Messages);
        Assert.Contains("Volume", model.Messages[0]);
    }

    [Fact]
    public void Default_preset_rows_are_all_known()
    {
        var model = SectionTableBuilder.Build("C1", 0, FlatGround(), Ditch(), new SectionTableOptions().Rows);

        Assert.Empty(model.Messages);
        Assert.Equal(5, model.Rows.Count);
    }

    [Fact]
    public void Export_has_one_row_per_section_in_station_order()
    {
        var a = SectionTableBuilder.Build("C2", 1020.5, FlatGround(), Ditch(), AllRows());
        var b = SectionTableBuilder.Build("C1", 1000, FlatGround(), null, AllRows());

        var table = SectionTableBuilder.Export(new[] { a, b });

        Assert.Equal(new[] { "Tên cọc", "Lý trình", "Diện tích đào", "Diện tích đắp", "Chênh cao tim" }, table.Headers);
        Assert.Equal(new[] { "C1", "1+000.00", "0.00", "0.00", "" }, table.Rows[0]);
        Assert.Equal(new[] { "C2", "1+020.50", "4.50", "0.00", "-1.00" }, table.Rows[1]);
    }

    [Theory]
    [InlineData("vi-VN")]
    [InlineData("de-DE")]
    public void Numbers_use_a_dot_whatever_the_culture(string culture)
    {
        TestCulture.Run(culture, () =>
        {
            var model = SectionTableBuilder.Build("C1", 1020.5, Line(-10, 1.256, 10, 1.256), Ditch(), AllRows());
            Assert.Equal("1.26", model.Rows.Single(r => r.Key == SectionTableBuilder.GroundElevation).Cells[0]);
            var table = SectionTableBuilder.Export(new[] { model });
            Assert.Equal("1+020.50", table.Rows[0][1]);
        });
    }

    [Fact]
    public void Profile_ignores_non_finite_points_and_sorts_by_offset()
    {
        var p = new SectionProfile(new List<(double, double)> { (5, 1), (double.NaN, 2), (-5, 3) });

        Assert.Equal(new[] { -5.0, 5 }, p.Points.Select(v => v.Offset));
        Assert.Equal(2, p.ElevationAt(0).Value, 12);
        Assert.Null(p.ElevationAt(6));
    }
}
