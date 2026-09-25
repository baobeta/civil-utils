using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class NumberInputTests
{
    [Theory]
    [InlineData("12.5", 12.5)]
    [InlineData("12,5", 12.5)]
    [InlineData("  250 ", 250)]
    [InlineData("-3,25", -3.25)]
    public void Accepts_dot_or_single_comma(string text, double expected)
    {
        TestCulture.Run("vi-VN", () =>
        {
            Assert.True(NumberInput.TryParse(text, out var v));
            Assert.Equal(expected, v, 10);
        });
        TestCulture.Run("en-US", () =>
        {
            Assert.True(NumberInput.TryParse(text, out var v));
            Assert.Equal(expected, v, 10);
        });
    }

    [Theory]
    [InlineData("1.234,5")]
    [InlineData("1,2,3")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rejects_bad_text(string text)
    {
        TestCulture.Run("vi-VN", () => Assert.False(NumberInput.TryParse(text, out _)));
    }
}
