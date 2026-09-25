using System;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class CurveElementsTests
{
    private static double Rad(double deg) => deg * Math.PI / 180;

    [Fact]
    public void Circular_curve_matches_textbook_formulas()
    {
        var e = CurveElementsCalculator.Compute(100, Rad(90), 0, 0);

        Assert.Equal(100, e.T1, 4);
        Assert.Equal(100, e.T2, 4);
        Assert.Equal(41.4214, e.P, 4);
        Assert.Equal(157.0796, e.K, 4);
        Assert.Equal(e.K, e.K0, 9);
    }

    [Fact]
    public void Symmetric_spiral_curve()
    {
        var e = CurveElementsCalculator.Compute(200, Rad(60), 50, 50);

        Assert.Equal(0.5205, e.Shift1, 4);
        Assert.Equal(24.9870, e.TangentOffset1, 4);
        Assert.Equal(140.7576, e.T1, 4);
        Assert.Equal(140.7576, e.T2, 4);
        Assert.Equal(31.5412, e.P, 4);
        Assert.Equal(259.4395, e.K, 4);
        Assert.Equal(159.4395, e.K0, 4);
    }

    [Fact]
    public void Asymmetric_spiral_curve_has_different_tangents()
    {
        var e = CurveElementsCalculator.Compute(300, Rad(45), 60, 40);

        Assert.Equal(154.0685, e.T1, 4);
        Assert.Equal(144.7458, e.T2, 4);
        Assert.Equal(25.1086, e.P, 4);
        Assert.Equal(285.6194, e.K, 4);
    }

    [Theory]
    [InlineData(0, 30, 0, 0)]
    [InlineData(100, 0, 0, 0)]
    [InlineData(100, 180, 0, 0)]
    [InlineData(100, 30, -1, 0)]
    public void Rejects_invalid_input(double r, double deltaDeg, double l1, double l2)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CurveElementsCalculator.Compute(r, Rad(deltaDeg), l1, l2));
    }

    [Fact]
    public void Rejects_spirals_longer_than_the_deflection_allows()
    {
        // α = 10° needs (L1+L2)/(2R) ≤ 0.1745; 100/(2·100) = 0.5.
        var ex = Assert.Throws<ArgumentException>(() => CurveElementsCalculator.Compute(100, Rad(10), 50, 50));
        Assert.Contains("chuyển tiếp", ex.Message);
    }
}
