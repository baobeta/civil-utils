using System;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class CurveLimitsTests
{
    private static PlanPoint P(double x, double y) => new PlanPoint(x, y);

    [Fact]
    public void Max_radius_of_a_circular_curve_is_the_tangent_over_tan_half_delta()
    {
        var r = CurveLimits.MaxRadius(Math.PI / 2, 0, 0, 100, 80);   // T = R·tan(45°) = R

        Assert.Equal(80, r.Value, 2);
    }

    [Fact]
    public void Max_radius_with_spirals_keeps_both_tangents_inside()
    {
        var delta = 0.6;
        var r = CurveLimits.MaxRadius(delta, 40, 40, 150, 150).Value;
        var e = CurveElementsCalculator.Compute(r, delta, 40, 40);
        var over = CurveElementsCalculator.Compute(r + 0.02, delta, 40, 40);

        Assert.True(e.T1 <= 150);
        Assert.True(over.T1 > 150);
    }

    [Fact]
    public void No_radius_fits_when_the_spirals_alone_are_too_long()
    {
        Assert.Null(CurveLimits.MaxRadius(0.2, 200, 200, 50, 50));
        Assert.Null(CurveLimits.MaxRadius(0.2, 0, 0, 0, 50));
    }

    [Fact]
    public void Max_spiral_keeps_tangents_inside()
    {
        var l = CurveLimits.MaxSpiral(200, 0.5, 90, 90).Value;
        var e = CurveElementsCalculator.Compute(200, 0.5, l, l);

        Assert.True(e.T1 <= 90);
        Assert.True(CurveElementsCalculator.Compute(200, 0.5, l + 0.02, l + 0.02).T1 > 90);
        Assert.Null(CurveLimits.MaxSpiral(1000, 0.5, 90, 90));   // T at L = 0 is already 255 m
    }

    [Fact]
    public void Available_tangents_subtract_the_neighbours()
    {
        var pis = new[] { P(0, 0), P(200, 0), P(200, 200), P(400, 200) };
        var inputs = new[] { new CurveInput { Radius = 50 }, new CurveInput { Radius = 80 } };
        var design = RouteDesigner.Design(pis, 0, inputs, 60, null);

        CurveLimits.Available(pis, design, design.Curves[1], out var before, out var after);

        Assert.Equal(200 - 50, before, 6);   // T2 of Đ1 = R = 50
        Assert.Equal(200, after, 6);
    }

    [Fact]
    public void Spiral_parameter_round_trips()
    {
        Assert.Equal(100, CurveLimits.SpiralParameter(250, 40), 9);
        Assert.Equal(40, CurveLimits.SpiralLength(250, 100), 9);
        Assert.Equal(0, CurveLimits.SpiralParameter(250, 0));
    }
}
