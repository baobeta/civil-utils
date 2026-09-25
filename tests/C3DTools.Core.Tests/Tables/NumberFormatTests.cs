using C3DTools.Core.Tables;
using Xunit;

namespace C3DTools.Core.Tests.Tables;

public class NumberFormatTests
{
    [Theory]
    [InlineData(90.0, 2, "90")]
    [InlineData(0.60, 2, "0.6")]
    [InlineData(12.345, 2, "12.35")]
    [InlineData(-12.345, 2, "-12.35")]
    [InlineData(140.7649, 2, "140.76")]
    [InlineData(-0.001, 2, "0")]
    [InlineData(0, 2, "0")]
    [InlineData(250, 0, "250")]
    public void Trimmed_rounds_then_drops_trailing_zeros(double value, int decimals, string expected)
    {
        Assert.Equal(expected, NumberFormat.Trimmed(value, decimals));
    }

    [Fact]
    public void Trimmed_ignores_vietnamese_culture()
    {
        TestCulture.Run("vi-VN", () => Assert.Equal("0.6", NumberFormat.Trimmed(0.6, 2)));
    }
}
