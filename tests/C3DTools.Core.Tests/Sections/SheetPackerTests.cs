using System;
using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Presets;
using C3DTools.Core.Sections;
using Xunit;

namespace C3DTools.Core.Tests.Sections;

public class SheetPackerTests
{
    // A3 landscape 420 × 297, margins L25 R10 T10 B10, gap 10, 3 × 2, 1 mm = 1 unit.
    // Inner 385 × 277 from (25, 10); cells (385 − 20) / 3 = 121.667 wide, (277 − 10) / 2 = 133.5 high.
    private static SheetLayoutOptions A3() => new SheetLayoutOptions();

    private static List<(double, double)> Views(int count, double w = 100, double h = 100) =>
        Enumerable.Range(0, count).Select(_ => (w, h)).ToList();

    [Fact]
    public void Seven_views_on_three_by_two_need_two_sheets_and_the_seventh_starts_sheet_two()
    {
        var plan = SheetPacker.Pack(Views(7), A3(), 1);

        Assert.Equal(2, plan.SheetCount);
        var seventh = plan.Placements[6];
        Assert.Equal(1, seventh.Sheet);
        Assert.Equal(0, seventh.Row);
        Assert.Equal(0, seventh.Column);
        // Sheet 2 starts at 420 + 2·10 = 440; its top-left cell spans x 465…586.667, y 153.5…287.
        Assert.Equal(465 + (365.0 / 3 - 100) / 2, seventh.X, 9);
        Assert.Equal(153.5 + (133.5 - 100) / 2, seventh.Y, 9);
        Assert.Equal(6, plan.Sheets[1].FirstItem);
        Assert.Equal(6, plan.Sheets[1].LastItem);
        Assert.Equal(440, plan.Sheets[1].X, 9);
    }

    [Fact]
    public void Placements_are_row_major_from_the_top_left()
    {
        var plan = SheetPacker.Pack(Views(6), A3(), 1);

        Assert.Equal(1, plan.SheetCount);
        Assert.Equal(new[] { (0, 0), (0, 1), (0, 2), (1, 0), (1, 1), (1, 2) }, plan.Placements.Select(p => (p.Row, p.Column)));
        Assert.True(plan.Placements[0].Y > plan.Placements[3].Y);
        Assert.True(plan.Placements[0].X < plan.Placements[1].X);
        Assert.Equal(0, plan.Oversize);
    }

    [Fact]
    public void Scale_converts_paper_mm_to_drawing_units()
    {
        // 1:200 in metres: 1 mm = 0.2 m, the sheet is 84 × 59.4.
        var plan = SheetPacker.Pack(Views(1, 10, 10), A3(), 0.2, 1000, 2000);

        var sheet = plan.Sheets[0];
        Assert.Equal(1000, sheet.X, 9);
        Assert.Equal(2000, sheet.Y, 9);
        Assert.Equal(84, sheet.Width, 9);
        Assert.Equal(59.4, sheet.Height, 9);
        Assert.Equal(1005, sheet.InnerLeft, 9);
        Assert.Equal(2057.4, sheet.InnerTop, 9);
    }

    [Fact]
    public void An_item_larger_than_its_cell_is_flagged()
    {
        var plan = SheetPacker.Pack(new List<(double, double)> { (100, 100), (130, 50) }, A3(), 1);

        Assert.True(plan.Placements[0].Fits);
        Assert.False(plan.Placements[1].Fits);
        Assert.Equal(1, plan.Oversize);
    }

    [Fact]
    public void No_items_no_sheets()
    {
        Assert.Equal(0, SheetPacker.Pack(Views(0), A3(), 1).SheetCount);
    }

    [Fact]
    public void Invalid_layouts_are_rejected_in_vietnamese()
    {
        var layout = A3();
        layout.Columns = 0;
        Assert.NotNull(SheetPacker.Validate(layout));
        Assert.Throws<ArgumentException>(() => SheetPacker.Pack(Views(1), layout, 1));
        layout = A3();
        layout.MarginLeft = 300;
        layout.MarginRight = 200;
        Assert.Contains("rộng hơn", SheetPacker.Validate(layout));
        Assert.Null(SheetPacker.Validate(A3()));
    }

    [Theory]
    [InlineData("vi-VN")]
    [InlineData("en-US")]
    public void Title_names_the_sheet_and_its_station_range(string culture)
    {
        TestCulture.Run(culture, () =>
        {
            Assert.Equal("TRẮC NGANG – Tờ 1/2 – Km0+000.00 … Km0+120.50", SheetPacker.Title(0, 2, 0, 120.5));
            Assert.Equal("TRẮC NGANG – Tờ 2/2 – Km1+000.00", SheetPacker.Title(1, 2, 1000, 1000));
            Assert.Equal("TRẮC NGANG – Tờ 2/2", SheetPacker.Title(1, 2, 1000, double.NaN));
        });
    }
}
