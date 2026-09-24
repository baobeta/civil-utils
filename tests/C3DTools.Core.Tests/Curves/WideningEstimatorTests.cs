using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class WideningEstimatorTests
{
    [Fact]
    public void Nominal_is_the_smallest_absolute_offset()
    {
        Assert.Equal(3.5, WideningEstimator.Nominal(new[] { 3.5, 3.5, 4.1, 3.5 }), 9);
        Assert.Equal(3.5, WideningEstimator.Nominal(new[] { -3.6, -3.5, -4.1 }), 9);
    }

    [Fact]
    public void Widening_is_offset_at_mid_minus_nominal()
    {
        Assert.Equal(0.6, WideningEstimator.Widening(4.1, 3.5), 9);
        Assert.Equal(0.6, WideningEstimator.Widening(-4.1, 3.5), 9);
    }

    [Theory]
    [InlineData(3.504)]
    [InlineData(3.4)]
    public void Widening_below_5_mm_is_zero(double offsetAtMid)
    {
        Assert.Equal(0, WideningEstimator.Widening(offsetAtMid, 3.5));
    }
}
