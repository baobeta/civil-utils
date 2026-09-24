using System.Collections.Generic;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class CurveRuleCheckerTests
{
    private static readonly CurveRules Rules = new CurveRules
    {
        MinRadius = new List<SpeedRadiusRule> { new SpeedRadiusRule { DesignSpeed = 60, MinRadius = 125, NormalRadius = 180 } },
        MinSpiral = new List<RadiusRangeRule>
        {
            new RadiusRangeRule { DesignSpeed = 60, RadiusFrom = 125, RadiusTo = 250, Value = 50 },
            new RadiusRangeRule { DesignSpeed = 60, RadiusFrom = 250, RadiusTo = 1000, Value = 40 },
        },
        Widening = new List<RadiusRangeRule>
        {
            new RadiusRangeRule { RadiusFrom = 150, RadiusTo = 250, Value = 0.6 },
            new RadiusRangeRule { RadiusFrom = 100, RadiusTo = 150, Value = 0.9 },
        },
    };

    private static CurveGroup Curve(double r, double l) => new CurveGroup(1, r, 1, 1.0, l, l, 0, l, l + 100);

    [Fact]
    public void Passing_curve_has_no_issues_and_gets_widening()
    {
        var result = CurveRuleChecker.Check(Curve(200, 50), 60, Rules);

        Assert.Empty(result.Issues);
        Assert.Equal(0.6, result.Widening);
    }

    [Fact]
    public void Radius_below_minimum_is_reported()
    {
        var result = CurveRuleChecker.Check(Curve(110, 50), 60, Rules);

        Assert.Contains(result.Issues, i => i.Code == CurveIssueCode.RadiusTooSmall);
    }

    [Fact]
    public void Radius_between_limit_and_normal_is_a_warning_only()
    {
        var result = CurveRuleChecker.Check(Curve(150, 50), 60, Rules);

        var issue = Assert.Single(result.Issues);
        Assert.Equal(CurveIssueCode.RadiusBelowNormal, issue.Code);
    }

    [Fact]
    public void Spiral_shorter_than_minimum_is_reported()
    {
        var result = CurveRuleChecker.Check(Curve(200, 30), 60, Rules);

        Assert.Contains(result.Issues, i => i.Code == CurveIssueCode.SpiralTooShort);
    }

    [Fact]
    public void Range_upper_bound_is_inclusive()   // decision D2
    {
        Assert.Equal(0.9, CurveRuleChecker.Check(Curve(150, 50), 60, Rules).Widening);
    }

    [Fact]
    public void Unknown_design_speed_is_reported_not_guessed()
    {
        var result = CurveRuleChecker.Check(Curve(200, 50), 80, Rules);

        Assert.Contains(result.Issues, i => i.Code == CurveIssueCode.NoRuleForSpeed);
    }

    [Fact]
    public void Radius_outside_widening_table_gives_no_widening()
    {
        Assert.Null(CurveRuleChecker.Check(Curve(600, 50), 60, Rules).Widening);
    }
}
