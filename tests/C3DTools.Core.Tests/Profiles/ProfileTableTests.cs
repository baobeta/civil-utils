using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Presets;
using C3DTools.Core.Profiles;
using C3DTools.Core.Stations;
using Xunit;

namespace C3DTools.Core.Tests.Profiles;

public class ProfileTableBuilderTests
{
    private static readonly List<StakeStation> Stations = new List<StakeStation>
    {
        new StakeStation(0, "Km0", StakeOrigin.Interval),
        new StakeStation(20, "C1", StakeOrigin.Interval),
        new StakeStation(45.5, "TĐ1", StakeOrigin.Curve),
        new StakeStation(100, "H1", StakeOrigin.Interval),
    };

    // +3 % to PVI 60 (L 40: TĐ 40, TC 80), then −2 % to 100.
    private static List<ProfileSegment> Design() => new List<ProfileSegment>
    {
        ProfileSegment.Tangent(0, 10, 40, 11.2),
        ProfileSegment.Parabola(60, 11.8, 0.03, -0.02, 40),
        ProfileSegment.Tangent(80, 11.4, 100, 11),
    };

    private static List<TableRowSpec> AllRows() => ProfileTableBuilder.KnownRows().ToList();

    private static ProfileTableModel Build(List<TableRowSpec> rows = null) =>
        ProfileTableBuilder.Build(Stations,
            new double?[] { 10.5, 10.004, null, 12.126 },
            new double?[] { 10, 10.6, 11.365, 11 },
            Design(), rows ?? AllRows());

    private static ProfileTableRow Row(ProfileTableModel m, string key) => m.Rows.Single(r => r.Key == key);

    [Fact]
    public void Rows_follow_the_spec_order()
    {
        var m = Build();

        Assert.Equal(ProfileTableBuilder.KnownRows().Select(r => r.Key), m.Rows.Select(r => r.Key));
        Assert.Equal("Tên cọc", m.Rows[0].Label);
        Assert.Equal(new[] { "Km0", "C1", "TĐ1", "H1" }, Row(m, "StakeName").Cells);
    }

    [Fact]
    public void Partial_distances_are_spans_between_stations()
    {
        var spans = Row(Build(), "PartialDistance").Spans;

        Assert.Equal(new[] { "20.00", "25.50", "54.50" }, spans.Select(s => s.Line1));
        Assert.Equal((20.0, 45.5), (spans[1].From, spans[1].To));
    }

    [Fact]
    public void Cumulative_distance_and_station()
    {
        var m = Build();

        Assert.Equal(new[] { "0.00", "20.00", "45.50", "100.00" }, Row(m, "CumulativeDistance").Cells);
        Assert.Equal(new[] { "0+000.00", "0+020.00", "0+045.50", "0+100.00" }, Row(m, "Station").Cells);
    }

    [Fact]
    public void Cut_fill_is_design_minus_ground_positive_for_fill()
    {
        var m = Build();

        // 10 − 10.5 = −0.50 (đào); 10.6 − 10.004 = +0.596 → +0.60 (đắp); no ground → empty; 11 − 12.126 = −1.126 → −1.13.
        Assert.Equal(new[] { "-0.50", "+0.60", "", "-1.13" }, Row(m, "CutFill").Cells);
        Assert.Equal(new[] { "10.50", "10.00", "", "12.13" }, Row(m, "GroundElevation").Cells);
    }

    [Fact]
    public void Grade_row_is_merged_pvi_to_pvi()
    {
        var spans = Row(Build(), "Grade").Spans;

        Assert.Equal(2, spans.Count);
        Assert.Equal((0.0, 60.0, "i=+3.00%", "L=60.00"), (spans[0].From, spans[0].To, spans[0].Line1, spans[0].Line2));
        Assert.Equal((60.0, 100.0, "i=-2.00%", "L=40.00"), (spans[1].From, spans[1].To, spans[1].Line1, spans[1].Line2));
    }

    [Fact]
    public void Vertical_curve_row_spans_td_to_tc()
    {
        var span = Assert.Single(Row(Build(), "VerticalCurve").Spans);

        Assert.Equal((40.0, 80.0, "R=800 K=40"), (span.From, span.To, span.Line1));
    }

    [Fact]
    public void Decimals_come_from_each_row()
    {
        var rows = new List<TableRowSpec>
        {
            new TableRowSpec("GroundElevation", "TN", 3),
            new TableRowSpec("DesignElevation", "TK", 1),
            new TableRowSpec("Grade", "i", 1),
        };

        var m = Build(rows);

        Assert.Equal(new[] { "10.500", "10.004", "", "12.126" }, m.Rows[0].Cells);
        Assert.Equal(new[] { "10.0", "10.6", "11.4", "11.0" }, m.Rows[1].Cells);
        Assert.Equal("i=+3.0%", m.Rows[2].Spans[0].Line1);
        Assert.Equal("L=60.0", m.Rows[2].Spans[0].Line2);
    }

    [Fact]
    public void Spans_are_clipped_to_a_station_range_narrower_than_the_profile()
    {
        var stations = new List<StakeStation> { new StakeStation(20, "C1", StakeOrigin.Interval), new StakeStation(70, "C2", StakeOrigin.Interval) };

        var m = ProfileTableBuilder.Build(stations, null, null, Design(), AllRows());

        var grade = Row(m, "Grade").Spans;
        Assert.Equal(new[] { (20.0, 60.0, "L=60.00"), (60.0, 70.0, "L=40.00") }, grade.Select(s => (s.From, s.To, s.Line2)));   // L stays the full grade length
        var curve = Assert.Single(Row(m, "VerticalCurve").Spans);
        Assert.Equal((40.0, 70.0), (curve.From, curve.To));
        Assert.Equal(new[] { "0.00", "50.00" }, Row(m, "CumulativeDistance").Cells);
    }

    [Fact]
    public void Legacy_keys_and_unknown_keys()
    {
        var rows = new List<TableRowSpec>
        {
            new TableRowSpec("Distance", "KC lẻ", 2),
            new TableRowSpec("ElevationDifference", "Chênh", 2),
            new TableRowSpec("Superelevation", "Siêu cao", 2),
        };

        var m = Build(rows);

        Assert.Equal(new[] { "PartialDistance", "CutFill" }, m.Rows.Select(r => r.Key));
        Assert.Contains("Superelevation", Assert.Single(m.Messages));
    }

    [Fact]
    public void Export_has_one_row_per_station()
    {
        var t = Build().Export;

        Assert.Equal(new[] { "Tên cọc", "Lý trình", "KC lẻ", "KC cộng dồn", "CĐ TN", "CĐ TK", "Chênh cao", "Dốc dọc" }, t.Headers);
        Assert.Equal(4, t.Rows.Count);
        Assert.Equal(new[] { "Km0", "0+000.00", "0.00", "0.00", "10.50", "10.00", "-0.50", "+3.00" }, t.Rows[0]);
        Assert.Equal(new[] { "TĐ1", "0+045.50", "25.50", "45.50", "", "11.37", "", "+3.00" }, t.Rows[2]);
        Assert.Equal("-2.00", t.Rows[3][7]);
    }

    [Fact]
    public void Ignores_windows_culture()
    {
        TestCulture.Run("vi-VN", () =>
        {
            var m = Build();
            Assert.Equal("25.50", Row(m, "PartialDistance").Spans[1].Line1);
            Assert.Equal("i=+3.00%", Row(m, "Grade").Spans[0].Line1);
            Assert.Equal("0+045.50", m.Export.Rows[2][1]);
        });
    }

    [Fact]
    public void Grade_lines_of_a_single_tangent_and_of_a_corner()
    {
        Assert.Equal(0.01, Assert.Single(ProfileTableBuilder.GradeLines(new[] { ProfileSegment.Tangent(0, 0, 100, 1) })).Grade, 9);

        var lines = ProfileTableBuilder.GradeLines(new[] { ProfileSegment.Tangent(0, 0, 50, 1), ProfileSegment.Tangent(50, 1, 100, 0) });

        Assert.Equal(new[] { (0.0, 50.0), (50.0, 100.0) }, lines.Select(l => (l.Start, l.End)));
        Assert.Equal(-0.02, lines[1].Grade, 9);
        Assert.Empty(ProfileTableBuilder.GradeLines(null));
    }
}

public class ProfileTableLayoutTests
{
    private static ProfileTableModel Model() =>
        ProfileTableBuilder.Build(
            new List<StakeStation> { new StakeStation(0, "A", StakeOrigin.Interval), new StakeStation(50, "B", StakeOrigin.Interval) },
            null, new double?[] { 10, 11 },
            new[] { ProfileSegment.Tangent(0, 10, 50, 11) },
            new[] { new TableRowSpec("StakeName", "Tên cọc", 0), new TableRowSpec("Grade", "Độ dốc", 2) });

    [Fact]
    public void Rows_go_down_from_the_top_with_labels_on_the_left()
    {
        var layout = ProfileTableLayout.Build(Model(), s => 1000 + s, top: 500, left: 1000, right: 1050, rowHeight: 8, textHeight: 2, labelWidth: 30, rotateStationText: true);

        Assert.Equal((500.0, 484.0, 970.0, 1050.0), (layout.Top, layout.Bottom, layout.Left, layout.Right));
        var horizontal = layout.Lines.Where(l => l.Y1 == l.Y2).Select(l => l.Y1).OrderByDescending(y => y);
        Assert.Equal(new[] { 500.0, 492.0, 484.0 }, horizontal);
        var label = layout.Texts.First(t => t.Text == "Tên cọc");
        Assert.Equal((985.0, 496.0), (label.X, label.Y));
    }

    [Fact]
    public void Station_text_is_rotated_beside_the_stake_line()
    {
        var layout = ProfileTableLayout.Build(Model(), s => 1000 + s, 500, 1000, 1050, 8, 2, 30, rotateStationText: true);

        var b = layout.Texts.Single(t => t.Text == "B");
        Assert.Equal(1050 - 1.5, b.X, 9);
        Assert.Equal(System.Math.PI / 2, b.Rotation, 9);
        Assert.Contains(layout.Lines, l => l.X1 == 1050 && l.X2 == 1050 && l.Y1 == 500 && l.Y2 == 492);
    }

    [Fact]
    public void Unrotated_station_text_is_centred()
    {
        var layout = ProfileTableLayout.Build(Model(), s => 1000 + s, 500, 1000, 1050, 8, 2, 30, rotateStationText: false);

        // Centred on station 0 would stick out left of the table: shifted right by half its width + 0.1·h.
        var a = layout.Texts.Single(t => t.Text == "A");
        Assert.Equal((1000.9, 496.0, 0.0), (System.Math.Round(a.X, 9), a.Y, a.Rotation));
        var b = layout.Texts.Single(t => t.Text == "B");
        Assert.Equal(1050 - 0.9, b.X, 9);
    }

    [Fact]
    public void Span_text_on_two_lines()
    {
        var layout = ProfileTableLayout.Build(Model(), s => 1000 + s, 500, 1000, 1050, 9, 2, 30, true);

        var i = layout.Texts.Single(t => t.Text == "i=+2.00%");
        var l = layout.Texts.Single(t => t.Text == "L=50.00");
        // Row 2 is 500 − 9 … 500 − 18, mid 486.5; lines at mid ± 0.65·h.
        Assert.Equal(1025, i.X, 9);
        Assert.Equal(487.8, i.Y, 9);
        Assert.Equal(1025, l.X, 9);
        Assert.Equal(485.2, l.Y, 9);
    }

    private static ProfileTableModel Full(double spacing = 20) =>
        ProfileTableBuilder.Build(
            Enumerable.Range(0, 6).Select(i => new StakeStation(i * spacing, i == 0 ? "Km0" : "C" + i, StakeOrigin.Interval)).ToList(),
            new double?[] { 10.5, 10.9, 11.2, 11.3, 11.1, 12.126 },
            new double?[] { 10, 10.6, 11.2, 11.5, 11.4, 11 },
            new List<ProfileSegment>
            {
                ProfileSegment.Tangent(0, 10, 2 * spacing, 10 + 0.03 * 2 * spacing),
                ProfileSegment.Parabola(3 * spacing, 10 + 0.03 * 3 * spacing, 0.03, -0.02, 2 * spacing),
                ProfileSegment.Tangent(4 * spacing, 10 + 0.03 * 3 * spacing - 0.02 * spacing, 5 * spacing, 10 + 0.03 * 3 * spacing - 0.04 * spacing),
            },
            ProfileTableBuilder.KnownRows());

    /// <summary>(left, bottom, right, top) of a text from the 0.7·h per character estimate.</summary>
    private static (double l, double b, double r, double t) Box(ProfileTableText t)
    {
        var w = ProfileTableLayout.TextWidth(t.Text, t.Height);
        var rotated = System.Math.Abs(t.Rotation - System.Math.PI / 2) < 1e-9;
        double hw = rotated ? t.Height / 2 : w / 2, hh = rotated ? w / 2 : t.Height / 2;
        return (t.X - hw, t.Y - hh, t.X + hw, t.Y + hh);
    }

    // Rotated: 20 m stakes. Horizontal station text ("0+000.00" ≈ 14 wide at h 2.5) needs wider stakes: 30 m.
    [Theory]
    [InlineData(true, 20)]
    [InlineData(false, 30)]
    public void Every_text_fits_its_row_and_none_overlap_with_the_preset_sizes(bool rotate, double spacing)
    {
        var model = Full(spacing);
        const double h = 2.5;
        var labelWidth = model.Rows.Max(r => ProfileTableLayout.TextWidth(r.Label, h)) + 2 * h;

        var layout = ProfileTableLayout.Build(model, s => 1000 + s, top: 500, left: 1000, right: 1000 + 5 * spacing, rowHeight: 8, textHeight: h,
            labelWidth: labelWidth, rotateStationText: rotate);

        Assert.All(layout.RowHeights, rh => Assert.True(rh >= 2.6 * h - 1e-9));
        var bands = new List<(double bottom, double top)>();
        var y = 500.0;
        foreach (var rh in layout.RowHeights)
        {
            bands.Add((y - rh, y));
            y -= rh;
        }

        var boxes = layout.Texts.Select(Box).ToList();
        foreach (var b in boxes)
        {
            Assert.Contains(bands, band => b.b >= band.bottom - 1e-9 && b.t <= band.top + 1e-9);
            Assert.True(b.l >= layout.Left - 1e-9 && b.r <= layout.Right + 1e-9);
        }

        for (var i = 0; i < boxes.Count; i++)
        for (var j = i + 1; j < boxes.Count; j++)
        {
            var overlap = boxes[i].l < boxes[j].r - 1e-9 && boxes[j].l < boxes[i].r - 1e-9
                          && boxes[i].b < boxes[j].t - 1e-9 && boxes[j].b < boxes[i].t - 1e-9;
            Assert.False(overlap, $"'{layout.Texts[i].Text}' overlaps '{layout.Texts[j].Text}'");
        }

        Assert.Equal(0, layout.SkippedTexts);
    }

    [Fact]
    public void Rotated_station_rows_grow_to_the_longest_text()
    {
        var layout = ProfileTableLayout.Build(Full(), s => 1000 + s, 500, 1000, 1100, 8, 2.5, 60, rotateStationText: true);
        var station = Full().Rows.Select((r, i) => (r, i)).Single(x => x.r.Key == "Station").i;

        Assert.Equal(0.7 * 2.5 * "0+100.00".Length + 2.5, layout.RowHeights[station], 9);
        Assert.Equal(layout.Top - layout.RowHeights.Sum(), layout.Bottom, 9);
    }

    [Fact]
    public void Narrow_span_text_is_rotated_or_skipped_and_edges_are_drawn_once()
    {
        var model = ProfileTableBuilder.Build(
            new List<StakeStation> { new StakeStation(0, "A", StakeOrigin.Interval), new StakeStation(4, "B", StakeOrigin.Interval), new StakeStation(5, "C", StakeOrigin.Interval) },
            null, null, null, new[] { new TableRowSpec("PartialDistance", "KC", 2) });

        // Span 0–4 (width 4): "4.00" (7 wide) rotated fits a 10-high row; span 4–5 (width 1): too narrow even rotated.
        var layout = ProfileTableLayout.Build(model, s => s, 100, 0, 5, 10, 2.5, 10, true);

        var four = layout.Texts.Single(t => t.Text == "4.00");
        Assert.Equal(System.Math.PI / 2, four.Rotation, 9);
        Assert.DoesNotContain(layout.Texts, t => t.Text == "1.00");
        Assert.Equal(1, layout.SkippedTexts);
        Assert.Single(layout.Lines, l => l.X1 == 4 && l.X2 == 4);
    }
}

public class ProfileTableSessionTests
{
    private static ProfileTableSession Loaded()
    {
        var s = new ProfileTableSession(new ProjectPreset());
        s.SetSource("PV1", 0, 100);
        s.SetProfiles(new[] { "EG" }, new[] { "TK", "TK2" });
        return s;
    }

    [Fact]
    public void Rows_from_the_preset_then_the_other_known_rows_unchecked()
    {
        var s = new ProfileTableSession(new ProjectPreset());

        Assert.Equal(new[] { "StakeName", "PartialDistance", "CumulativeDistance", "Station", "GroundElevation", "DesignElevation", "CutFill", "Grade", "VerticalCurve" },
            s.Rows.Select(r => r.Key));
        Assert.True(s.Rows.Take(8).All(r => r.Include));
        Assert.False(s.Rows[8].Include);
        Assert.Equal("Khoảng cách lẻ", s.Rows[1].Label);
        Assert.Empty(s.PresetMessages);
        Assert.Equal("2.5", s.TextHeightText);
        Assert.True(s.RotateStationText);
    }

    [Fact]
    public void Rotate_station_text_defaults_to_true_and_round_trips()
    {
        Assert.True(PresetSerializer.Load("{ \"SchemaVersion\": 1 }").ProfileTable.RotateStationText);
        var preset = new ProjectPreset();
        preset.ProfileTable.RotateStationText = false;

        Assert.False(PresetSerializer.Load(PresetSerializer.Save(preset)).ProfileTable.RotateStationText);
    }

    [Fact]
    public void Unknown_preset_rows_are_reported()
    {
        var preset = new ProjectPreset();
        preset.ProfileTable.Rows.Add(new TableRowSpec("Superelevation", "Siêu cao", 2));

        var s = new ProfileTableSession(preset);

        Assert.Contains("Superelevation", Assert.Single(s.PresetMessages));
        Assert.DoesNotContain(s.Rows, r => r.Key == "Superelevation");
    }

    [Fact]
    public void Defaults_and_profiles()
    {
        var s = Loaded();

        Assert.Equal("EG", s.SurfaceProfile);
        Assert.Equal("TK", s.DesignProfile);
        Assert.True(s.CanApply);
        Assert.Equal("Bấm Xem trước để cập nhật bảng", s.SummaryText);

        s.SurfaceProfileIndex = 0;
        s.DesignProfileIndex = 0;
        Assert.False(s.CanApply);
        Assert.Equal("Chọn trắc dọc tự nhiên hoặc thiết kế", s.SummaryText);
    }

    [Fact]
    public void Move_rows_up_and_down()
    {
        var s = Loaded();

        s.MoveRow(1, -1);
        Assert.Equal("PartialDistance", s.Rows[0].Key);
        Assert.Equal(0, s.SelectedRowIndex);

        s.MoveRow(0, -1);   // already at the top
        Assert.Equal("PartialDistance", s.Rows[0].Key);

        s.MoveRow(8, 1);    // already at the bottom
        Assert.Equal("VerticalCurve", s.Rows[8].Key);
    }

    [Fact]
    public void Invalid_decimals_and_text_height_block_apply()
    {
        var s = Loaded();

        s.Rows[4].DecimalsText = "7";
        Assert.False(s.CanApply);
        Assert.Equal("Số lẻ phải là số nguyên từ 0 đến 6", s.SummaryText);
        s.Rows[4].DecimalsText = "3";

        s.TextHeightText = "2,0";
        Assert.True(s.CanApply);
        Assert.Equal(2, s.TextHeight);
        s.TextHeightText = "0";
        Assert.False(s.CanApply);
    }

    [Fact]
    public void Station_modes()
    {
        var s = Loaded();
        var curve = new[] { new StakeStation(45.5, "TĐ1", StakeOrigin.Curve) };
        var samples = new[] { new StakeStation(10, "SL1", StakeOrigin.Extra), new StakeStation(150, "SL9", StakeOrigin.Extra) };

        s.IntervalText = "50";
        Assert.Equal(new[] { 0.0, 45.5, 50, 100 }, s.Stations(curve, samples).Select(x => x.Station));

        s.IsInterval = true;
        Assert.Equal(new[] { 0.0, 50, 100 }, s.Stations(curve, samples).Select(x => x.Station));

        s.IsSampleLines = true;
        s.IntervalText = "x";   // not used for sample lines
        Assert.True(s.IsIntervalValid);
        Assert.Equal(new[] { "SL1" }, s.Stations(curve, samples).Select(x => x.Name));
    }

    [Fact]
    public void Build_uses_the_checked_rows_in_order()
    {
        var s = Loaded();
        foreach (var r in s.Rows) r.Include = r.Key == "Station" || r.Key == "StakeName";
        s.MoveRow(3, -1);
        s.MoveRow(2, -1);
        s.MoveRow(1, -1);

        var m = s.Build(new[] { new StakeStation(0, "A", StakeOrigin.Interval) }, null, null, null);

        Assert.Equal(new[] { "Station", "StakeName" }, m.Rows.Select(r => r.Key));
        Assert.Equal("1 cọc, 2 dòng", s.SummaryText);
        Assert.False(s.IsStale);
    }
}
