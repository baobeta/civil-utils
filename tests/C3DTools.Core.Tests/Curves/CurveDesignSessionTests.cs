using System;
using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Curves;
using C3DTools.Core.Presets;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class CurveDesignSessionTests
{
    private static PlanPoint P(double x, double y) => new PlanPoint(x, y);
    private static CurveInput R(double r, double l1 = 0, double l2 = 0) =>
        new CurveInput { Radius = r, SpiralIn = l1, SpiralOut = l2 };

    private static CurveRules Rules() => new CurveRules
    {
        MinRadius = new List<SpeedRadiusRule> { new SpeedRadiusRule { DesignSpeed = 60, MinRadius = 125, NormalRadius = 250 } },
        MinSpiral = new List<RadiusRangeRule>
        {
            new RadiusRangeRule { DesignSpeed = 60, RadiusFrom = 125, RadiusTo = 250, Value = 50 },
            new RadiusRangeRule { DesignSpeed = 60, RadiusFrom = 250, RadiusTo = 1000, Value = 40 },
        },
        Widening = new List<RadiusRangeRule> { new RadiusRangeRule { RadiusFrom = 200, RadiusTo = 300, Value = 0.6 } },
    };

    private static CurveDesignSession Session(CurveRules rules = null, double? speed = 60) =>
        new CurveDesignSession(new ProjectPreset { CurveRules = rules, DesignSpeed = speed });

    // Three 90° turns, legs of 1000 m.
    private static readonly PlanPoint[] Zigzag = { P(0, 0), P(1000, 0), P(1000, 1000), P(2000, 1000), P(2000, 2000) };
    private static readonly PlanPoint[] Square = { P(0, 0), P(100, 0), P(100, 100) };

    private static List<string> Watch(System.ComponentModel.INotifyPropertyChanged source)
    {
        var names = new List<string>();
        source.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }

    [Fact]
    public void Load_without_inputs_fills_defaults_first_from_tcvn_then_copies_previous()
    {
        var s = Session(Rules());
        s.Load(Zigzag, null);

        Assert.Equal(3, s.Rows.Count);
        Assert.Equal(new[] { "Đ1", "Đ2", "Đ3" }, s.Rows.Select(r => r.Name));
        Assert.All(s.Inputs, i => Assert.Equal(250, i.Radius));
        Assert.All(s.Inputs, i => Assert.Equal(0, i.SpiralIn));
        Assert.NotSame(s.Inputs[0], s.Inputs[1]);
        Assert.Equal("250", s.Rows[2].RadiusText);
        Assert.Equal("90°00'00\"", s.Rows[0].AText);
    }

    [Fact]
    public void Load_without_rules_defaults_to_radius_100()
    {
        var s = Session();
        s.Load(Zigzag, null);

        Assert.All(s.Inputs, i => Assert.Equal(100, i.Radius));
        Assert.Equal("Chưa có bảng TCVN – không kiểm tra R, L", s.RminText);
        Assert.Equal(new double[] { 20, 30, 40, 60, 80, 100, 120 }, s.AvailableSpeeds);
    }

    [Fact]
    public void Load_with_existing_inputs_keeps_them_and_defaults_only_nulls()
    {
        var s = Session(Rules());
        s.Load(Zigzag, new[] { R(300, 60, 40), null, R(500) });

        Assert.Equal(300, s.Inputs[0].Radius);
        Assert.Equal(60, s.Inputs[0].SpiralIn);
        Assert.Equal(40, s.Inputs[0].SpiralOut);
        Assert.Equal(300, s.Inputs[1].Radius);   // null slot copies the row above
        Assert.Equal(500, s.Inputs[2].Radius);
    }

    [Fact]
    public void Collinear_pi_has_no_row_but_keeps_an_input_slot()
    {
        var s = Session(Rules());
        s.Load(new[] { P(0, 0), P(500, 0), P(1000, 0), P(1000, 1000) }, null);

        var row = Assert.Single(s.Rows);
        Assert.Equal(2, row.PiIndex);
        Assert.Equal("Đ1", row.Name);
        Assert.Equal(2, s.Inputs.Count);
    }

    [Fact]
    public void Editing_radius_raises_changes_for_row_session_and_later_rows()
    {
        var s = Session(Rules());
        s.Load(Zigzag, null);
        var row1 = Watch(s.Rows[1]);
        var row2 = Watch(s.Rows[2]);
        var session = Watch(s);
        var before = s.Rows[2].StationsText;

        s.Rows[1].RadiusText = "400";

        Assert.Equal(400, s.Inputs[1].Radius);
        foreach (var name in new[] { "T1Text", "KText", "StationsText", "IssueText" }) Assert.Contains(name, row1);
        Assert.Contains("CanApply", session);
        Assert.Contains("StationsText", row2);
        Assert.NotEqual(before, s.Rows[2].StationsText);
    }

    [Fact]
    public void Editing_first_row_shifts_second_row_stations()
    {
        var s = Session(Rules());
        s.Load(Zigzag, null);
        var before = s.Rows[1].StationsText;

        s.Rows[0].RadiusText = "300";

        Assert.NotEqual(before, s.Rows[1].StationsText);
    }

    [Fact]
    public void Comma_decimal_is_accepted()
    {
        var s = Session(Rules());
        s.Load(Zigzag, null);

        s.Rows[0].SpiralInText = "45,5";

        Assert.Equal(45.5, s.Inputs[0].SpiralIn);
        Assert.False(s.Rows[0].HasInputError);
    }

    [Fact]
    public void Typed_text_is_shown_back_while_it_means_the_stored_value()
    {
        var s = Session(Rules());
        s.Load(Zigzag, null);
        var row = s.Rows[0];

        row.RadiusText = "12,";
        Assert.Equal("12,", row.RadiusText);   // typing continues with "12,5"
        row.RadiusText = "12,5";
        Assert.Equal("12,5", row.RadiusText);
        Assert.Equal(12.5, s.Inputs[0].Radius);

        s.CopyDown(0);
        Assert.Equal("12.5", s.Rows[1].RadiusText);

        s.Inputs[0].Radius = 300;   // changed elsewhere (suggest, reload): the formatted value wins
        Assert.Equal("300", row.RadiusText);
    }

    [Fact]
    public void Invalid_text_keeps_value_and_flags_error_until_fixed()
    {
        var s = Session(Rules());
        s.Load(Zigzag, null);
        var row = s.Rows[0];

        row.RadiusText = "abc";

        Assert.Equal(250, s.Inputs[0].Radius);
        Assert.Equal("abc", row.RadiusText);   // the grid keeps what the user typed
        Assert.True(row.HasInputError);
        Assert.False(s.CanApply);
        Assert.Equal(CurveRowSeverity.Error, row.Severity);

        row.RadiusText = "260";

        Assert.False(row.HasInputError);
        Assert.Equal(260, s.Inputs[0].Radius);
        Assert.Equal("260", row.RadiusText);
    }

    [Fact]
    public void CopyDown_clears_input_errors_on_overwritten_rows()
    {
        var s = Session(Rules());
        s.Load(Zigzag, new[] { R(300, 40, 40), R(300, 40, 40), R(300, 40, 40) });
        s.Rows[1].RadiusText = "abc";
        Assert.False(s.CanApply);

        s.CopyDown(0);

        Assert.Equal(CurveRowSeverity.None, s.Rows[1].Severity);
        Assert.False(s.Rows[1].HasInputError);
        Assert.Equal("300", s.Rows[1].RadiusText);
        Assert.True(s.CanApply);
    }

    [Fact]
    public void SuggestAll_clears_input_errors()
    {
        var s = Session(Rules());
        s.Load(Zigzag, null);
        s.Rows[2].SpiralInText = "x";

        s.SuggestAll();

        Assert.False(s.Rows[2].HasInputError);
        Assert.True(s.CanApply);
    }

    [Fact]
    public void CopyDown_copies_all_values_to_later_rows()
    {
        var s = Session(Rules());
        s.Load(Zigzag, new[] { R(100), R(300, 60, 50), R(100) });
        s.Inputs[1].Wb = 0.6;
        s.Inputs[1].Wl = 0.2;

        s.CopyDown(1);

        Assert.Equal(100, s.Inputs[0].Radius);
        var last = s.Inputs[2];
        Assert.Equal(300, last.Radius);
        Assert.Equal(60, last.SpiralIn);
        Assert.Equal(50, last.SpiralOut);
        Assert.Equal(0.6, last.Wb);
        Assert.Equal(0.2, last.Wl);
        Assert.NotSame(s.Inputs[1], last);
    }

    [Fact]
    public void SuggestAll_applies_normal_radius_min_spiral_and_widening()
    {
        var s = Session(Rules());
        s.Load(Zigzag, new[] { R(100), R(600), R(150) });

        s.SuggestAll();

        Assert.All(s.Inputs, i =>
        {
            Assert.Equal(250, i.Radius);
            Assert.Equal(50, i.SpiralIn);
            Assert.Equal(50, i.SpiralOut);
            Assert.Equal(0.6, i.Wb);
        });
    }

    [Fact]
    public void SuggestAll_without_rules_keeps_values()
    {
        var s = Session();
        s.Load(Zigzag, new[] { R(120, 30, 20), R(600), R(150) });

        s.SuggestAll();

        Assert.Equal(120, s.Inputs[0].Radius);
        Assert.Equal(30, s.Inputs[0].SpiralIn);
        Assert.Equal(20, s.Inputs[0].SpiralOut);
    }

    [Fact]
    public void Changing_speed_recalculates_issues_but_keeps_typed_radius()
    {
        var s = Session(Rules());
        s.Load(Zigzag, new[] { R(200, 50, 50), R(200, 50, 50), R(200, 50, 50) });
        Assert.Contains("thông thường", s.Rows[0].IssueText);
        Assert.Equal(CurveRowSeverity.Warning, s.Rows[0].Severity);

        s.DesignSpeed = 40;

        Assert.Equal(200, s.Inputs[0].Radius);
        Assert.Contains("40 km/h", s.Rows[0].IssueText);
        Assert.DoesNotContain("thông thường", s.Rows[0].IssueText);
    }

    [Fact]
    public void Stations_text_lists_spiral_points_and_omits_zero_spirals()
    {
        var a = Math.PI / 4;
        var pis = new[] { P(0, 0), P(500, 0), P(500 + 500 * Math.Cos(a), 500 * Math.Sin(a)) };
        var s = Session();
        s.Load(pis, new[] { R(300, 60, 40) });

        Assert.Equal("NĐ 0+345.93 · TĐ 0+405.93 · P 0+498.74 · TC 0+591.55 · NC 0+631.55", s.Rows[0].StationsText);

        s.Rows[0].SpiralInText = "0";
        Assert.StartsWith("TĐ ", s.Rows[0].StationsText);
        Assert.DoesNotContain("NĐ", s.Rows[0].StationsText);
        Assert.Contains(" · NC ", s.Rows[0].StationsText);
    }

    [Fact]
    public void Square_route_summary_and_elements()
    {
        var s = Session();
        s.Load(Square, new[] { R(50) });

        Assert.Equal("1 đường cong, chiều dài tuyến = 178.54 m", s.SummaryText);
        Assert.Equal(178.5398, s.EndStation, 4);
        Assert.True(s.CanApply);
        var row = s.Rows[0];
        Assert.Equal("50", row.T1Text);
        Assert.Equal("78.54", row.KText);
        Assert.Equal(-1, row.Turn);
        Assert.Equal("", row.IssueText);
        Assert.Equal(CurveRowSeverity.None, row.Severity);
    }

    [Fact]
    public void Start_station_shifts_summary_and_stations()
    {
        var s = Session();
        s.Load(Square, new[] { R(50) });
        var watched = Watch(s);

        s.StartStation = 1000;

        Assert.Equal(1178.5398, s.EndStation, 4);
        Assert.Contains("EndStation", watched);
        Assert.StartsWith("TĐ 1+050.00", s.Rows[0].StationsText);
        Assert.Equal("1 đường cong, chiều dài tuyến = 178.54 m", s.SummaryText);
    }

    [Fact]
    public void Overlap_blocks_apply_and_shows_unknown_elements_as_question_mark()
    {
        var s = Session();
        s.Load(Square, new[] { R(100, 200, 200) });

        Assert.False(s.CanApply);
        Assert.Equal("?", s.Rows[0].T1Text);
        Assert.Equal(CurveRowSeverity.Error, s.Rows[0].Severity);
    }

    [Fact]
    public void Preset_speed_missing_from_rules_is_still_offered()
    {
        var s = Session(Rules(), speed: 50);

        Assert.Equal(new double[] { 50, 60 }, s.AvailableSpeeds);
        Assert.Equal(50, s.DesignSpeed);
    }

    [Fact]
    public void Option_flags_raise_property_changed()
    {
        var s = Session();
        var names = Watch(s);

        s.ReadOnlyGeometry = true;
        s.TextHeight = 3;
        s.DrawCurves = false;
        s.CreateAlignment = true;
        s.DrawBoxes = false;
        s.DrawStakes = false;
        s.WriteCsv = false;

        Assert.Equal(new[] { "ReadOnlyGeometry", "TextHeight", "DrawCurves", "CreateAlignment", "DrawBoxes", "DrawStakes", "WriteCsv" }, names);
    }

    [Fact]
    public void Rmin_text_and_speeds_come_from_rules()
    {
        var s = Session(Rules());

        Assert.Equal("Rmin giới hạn 125 m · thông thường 250 m", s.RminText);
        Assert.Equal(new double[] { 60 }, s.AvailableSpeeds);
    }

    [Fact]
    public void Defaults_and_read_only_geometry_is_plain_state()
    {
        var s = new CurveDesignSession(new ProjectPreset());
        s.Load(Square, new[] { R(50) });

        Assert.Equal(60, s.DesignSpeed);
        Assert.Equal(2.5, s.TextHeight);
        Assert.True(s.DrawCurves && s.DrawBoxes && s.DrawStakes && s.WriteCsv);
        Assert.False(s.CreateAlignment);
        Assert.False(s.ReadOnlyGeometry);

        s.ReadOnlyGeometry = true;
        s.Rows[0].RadiusText = "60";

        Assert.True(s.ReadOnlyGeometry);
        Assert.Equal(60, s.Inputs[0].Radius);
    }
}
