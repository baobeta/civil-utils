using System;
using C3DTools.Core.Stations;
using Xunit;

namespace C3DTools.Core.Tests.Stations;

public class StationFormatterTests
{
    [Theory]
    [InlineData(1234.56, 2, "Km1+234.56")]
    [InlineData(0, 2, "Km0+000.00")]
    [InlineData(5.5, 2, "Km0+005.50")]
    [InlineData(7, 0, "Km0+007")]
    [InlineData(999.999, 2, "Km1+000.00")]
    [InlineData(12345.6789, 3, "Km12+345.679")]
    [InlineData(-12.5, 2, "-Km0+012.50")]
    public void Format_produces_km_plus_metres(double station, int decimals, string expected)
    {
        Assert.Equal(expected, StationFormatter.Format(station, decimals));
    }

    [Fact]
    public void Format_ignores_vietnamese_culture()
    {
        TestCulture.Run("vi-VN", () => Assert.Equal("Km1+234.56", StationFormatter.Format(1234.56, 2)));
    }

    [Fact]
    public void Format_rejects_bad_decimals()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StationFormatter.Format(1, -1));
    }

    [Theory]
    [InlineData("Km1+234.56", 1234.56)]
    [InlineData("km0+005", 5)]
    [InlineData("1+234.56", 1234.56)]
    [InlineData(" Km12+000.00 ", 12000)]
    [InlineData("-Km0+012.50", -12.5)]
    public void TryParse_reads_station_text(string text, double expected)
    {
        Assert.True(StationFormatter.TryParse(text, out var station));
        Assert.Equal(expected, station, 6);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("Km1+1234")]
    [InlineData("Km1+23,5")]
    [InlineData("Km1")]
    public void TryParse_rejects_invalid_text(string text)
    {
        Assert.False(StationFormatter.TryParse(text, out _));
    }
}
