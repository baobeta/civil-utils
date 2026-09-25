using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class AngleFormatterTests
{
    [Theory]
    [InlineData(31.531111, 0, "31°31'52\"")]
    [InlineData(29.99999, 0, "30°00'00\"")]
    [InlineData(0.5, 0, "0°30'00\"")]
    [InlineData(5.0021, 1, "5°00'07.6\"")]
    [InlineData(-12.25, 0, "-12°15'00\"")]
    public void Formats_dms(double degrees, int secondDecimals, string expected)
    {
        Assert.Equal(expected, AngleFormatter.Dms(degrees, secondDecimals));
    }

    [Fact]
    public void Ignores_vietnamese_culture()
    {
        TestCulture.Run("vi-VN", () => Assert.Equal("5°00'07.6\"", AngleFormatter.Dms(5.0021, 1)));
    }
}
