using System;
using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Curves;
using C3DTools.Core.Presets;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

/// <summary>The CTYTC detail panel ("Hiệu chỉnh yếu tố cong và thông số siêu cao") on CurveRow and the session.</summary>
public class CurveDetailTests
{
    private static PlanPoint P(double x, double y) => new PlanPoint(x, y);
    private static readonly PlanPoint[] Zigzag = { P(0, 0), P(1000, 0), P(1000, 1000), P(2000, 1000), P(2000, 2000) };

    private static CurveRules Rules() => new CurveRules
    {
        MinRadius = new List<SpeedRadiusRule>
        {
            new SpeedRadiusRule { DesignSpeed = 60, MinRadius = 125, NormalRadius = 250 },
            new SpeedRadiusRule { DesignSpeed = 40, MinRadius = 60, NormalRadius = 125 },
        },
        MinSpiral = new List<RadiusRangeRule> { new RadiusRangeRule { DesignSpeed = 60, RadiusFrom = 100, RadiusTo = 300, Value = 50 } },
        Widening = new List<RadiusRangeRule> { new RadiusRangeRule { RadiusFrom = 100, RadiusTo = 300, Value = 0.8 } },
        Superelevation = new List<RadiusRangeRule> { new RadiusRangeRule { DesignSpeed = 60, RadiusFrom = 100, RadiusTo = 300, Value = 5 } },
    };

    private static CurveDesignSession Loaded(CurveRules rules = null)
    {
        var s = new CurveDesignSession(new ProjectPreset { CurveRules = rules ?? Rules(), DesignSpeed = 60 });
        s.Load(Zigzag, Enumerable.Range(0, 3).Select(_ => new CurveInput { Radius = 200 }).ToList());
        return s;
    }

    [Fact]
    public void Suggest_row_uses_min_or_normal_radius_and_splits_widening_when_asked()
    {
        var s = Loaded();
        s.SuggestNormalRadius = false;
        s.SplitWidening = true;

        s.SuggestRow(1);

        var input = s.Inputs[1];
        Assert.Equal(125, input.Radius);
        Assert.Equal(50, input.SpiralIn);
        Assert.Equal(0.4, input.Wb, 9);
        Assert.Equal(0.4, input.Wl, 9);
        Assert.Equal(200, s.Inputs[0].Radius);   // other rows untouched
    }

    [Fact]
    public void Per_PI_speed_drives_the_suggestion()
    {
        var s = Loaded();
        s.Rows[0].SpeedText = "40";

        s.SuggestRow(0);

        Assert.Equal(40, s.Inputs[0].DesignSpeed);
        Assert.Equal(125, s.Inputs[0].Radius);   // Rmin thông thường at 40 km/h
        s.Rows[0].SpeedText = "";
        Assert.Null(s.Inputs[0].DesignSpeed);
        s.Rows[0].SpeedText = "-5";
        Assert.True(s.Rows[0].HasInputError);
    }

    [Fact]
    public void Suggest_superelevation_takes_rate_and_runoff_from_the_tables()
    {
        var s = Loaded();

        Assert.Null(s.SuggestSuperelevation(0));

        var input = s.Inputs[0];
        Assert.True(input.Superelevated);
        Assert.Equal(5, input.SuperRate);
        Assert.False(input.RunoffOnSpiral);   // no spiral on this curve
        Assert.Equal(50, input.RunoffIn);
        Assert.Equal(25, input.OffsetIn);
        Assert.Equal("5%", s.Rows[0].SuperelevationText);
        Assert.True(s.HasSuperelevation);
        Assert.Equal(8, s.SuperelevationPoints.Count);
    }

    [Fact]
    public void Suggest_superelevation_without_a_table_value_says_so()
    {
        var s = Loaded();
        s.Rows[0].RadiusText = "900";

        Assert.Equal("Preset chưa có độ dốc siêu cao cho R = 900 m, V = 60 km/h.", s.SuggestSuperelevation(0));
        Assert.False(s.Inputs[0].Superelevated);
    }

    [Fact]
    public void Superelevation_fields_edit_the_input_and_validate()
    {
        var s = Loaded();
        var row = s.Rows[2];

        row.Superelevated = true;
        row.SuperRateText = "6";
        row.RunoffOnSpiral = false;
        row.RunoffInText = "60";
        row.OffsetInText = "30";
        row.RunoffOutText = "60";
        row.OffsetOutText = "30";

        Assert.Equal(6, s.Inputs[2].SuperRate);
        Assert.Equal(60, s.Inputs[2].RunoffIn);
        Assert.False(row.HasInputError);
        row.SuperRateText = "25";
        Assert.True(row.HasInputError);
        Assert.False(s.CanApply);
    }

    [Fact]
    public void Spiral_parameter_sets_the_length()
    {
        var s = Loaded();
        var row = s.Rows[0];

        row.A1Text = "100";   // L = 100² / 200

        Assert.Equal(50, s.Inputs[0].SpiralIn, 9);
        Assert.Equal("100", row.A1Text);
        Assert.Equal("0", row.A2Text);
    }

    [Fact]
    public void Rmax_and_Lmax_are_shown_for_the_row()
    {
        var s = Loaded();

        // 90° turns with 1000 m legs and R = 200 neighbours: T of Đ2 ≤ 800 → Rmax = 800.
        Assert.Equal("Rmax = 800.00", s.Rows[1].RmaxText);
        Assert.StartsWith("Lmax = ", s.Rows[1].LmaxText);
        Assert.Contains("đoạn trước 1000.00 m", s.Rows[0].HeaderText);
    }

    [Fact]
    public void Deflection_can_be_changed_only_on_a_polyline_design()
    {
        var s = Loaded();
        s.Rows[0].DeflectionText = "60";
        Assert.True(s.Rows[0].HasInputError);   // CanEditDeflection is off
        Assert.False(s.PisEdited);

        s.CanEditDeflection = true;
        s.Rows[0].DeflectionText = "60";

        Assert.False(s.Rows[0].HasInputError);
        Assert.True(s.PisEdited);
        Assert.Equal(60, s.Design.Curves[0].DeltaRadians * 180 / Math.PI, 6);
        Assert.Equal(90, s.Design.Curves[1].DeltaRadians * 180 / Math.PI, 6);
        Assert.Throws<InvalidOperationException>(() => { s.ReadOnlyGeometry = true; s.SetDeflection(0, 50); });
    }

    [Fact]
    public void Transition_warnings_reach_the_row()
    {
        var s = Loaded();
        var row = s.Rows[0];

        row.WbText = "0.5";

        Assert.Contains("chưa có chiều dài nối", row.IssueText);
        Assert.Equal(CurveRowSeverity.Warning, row.Severity);
        Assert.True(s.CanApply);
    }

    [Fact]
    public void Road_section_comes_from_the_preset()
    {
        var s = new CurveDesignSession(new ProjectPreset { RoadSection = new RoadSectionOptions { CrossSlope = 3, PavementHalfWidth = 4.5, EdgeStep = 2 } });

        Assert.Equal(3, s.CrossSlope);
        Assert.Equal(4.5, s.PavementHalfWidth);
        Assert.Equal(2, s.EdgeStep);
        Assert.False(s.SplitWidening);
        Assert.True(s.SuggestNormalRadius);
    }
}
