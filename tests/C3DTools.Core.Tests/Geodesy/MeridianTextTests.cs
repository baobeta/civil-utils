using C3DTools.Core.Geodesy;
using Xunit;

namespace C3DTools.Core.Tests.Geodesy;

public class MeridianTextTests
{
    [Theory]
    [InlineData("105°45'", 105.75)]
    [InlineData("105° 45′", 105.75)]
    [InlineData("105 45", 105.75)]
    [InlineData("105.75", 105.75)]
    [InlineData("105,75", 105.75)]
    [InlineData(" 107°00' ", 107.0)]
    [InlineData("104d30", 104.5)]
    [InlineData("105°45'36\"", 105.76)]
    [InlineData("105", 105.0)]
    public void Parses_degree_minute_and_decimal_forms(string text, double expected)
    {
        Assert.True(MeridianText.TryParse(text, out var deg));
        Assert.Equal(expected, deg, 9);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("105 60")]
    [InlineData("105°-5'")]
    [InlineData("200")]
    [InlineData("105 45 10 5")]
    public void Rejects_invalid_text(string text)
    {
        Assert.False(MeridianText.TryParse(text, out _));
    }

    [Theory]
    [InlineData(105.75, "105°45'")]
    [InlineData(105.0, "105°00'")]
    [InlineData(104.5, "104°30'")]
    [InlineData(105.76, "105°45'36\"")]
    public void Formats_as_degrees_and_minutes(double deg, string expected)
    {
        Assert.Equal(expected, MeridianText.Format(deg));
    }

    [Fact]
    public void Parsing_ignores_the_windows_culture()
    {
        TestCulture.Run("vi-VN", () =>
        {
            Assert.True(MeridianText.TryParse("105.75", out var deg));
            Assert.Equal(105.75, deg, 9);
            Assert.Equal("105°45'", MeridianText.Format(105.75));
        });
    }
}
