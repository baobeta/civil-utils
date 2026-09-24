using System;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class TextAngleTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3 * Math.PI / 4, -Math.PI / 4)]
    [InlineData(-3 * Math.PI / 4, Math.PI / 4)]
    [InlineData(Math.PI / 2, Math.PI / 2)]
    public void Readable_keeps_text_upright(double radians, double expected)
    {
        Assert.Equal(expected, TextAngle.Readable(radians), 9);
    }
}
