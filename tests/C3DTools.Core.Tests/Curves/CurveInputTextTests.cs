using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class CurveInputTextTests
{
    [Fact]
    public void Round_trips_every_extra_input()
    {
        var input = new CurveInput
        {
            DesignSpeed = 40, Superelevated = true, SuperRate = 5.5, RunoffOnSpiral = false,
            RunoffIn = 50, RunoffOut = 60.25, OffsetIn = 25, OffsetOut = 30.125,
        };

        var text = CurveInputText.Format(input);
        var back = new CurveInput();

        Assert.Equal("v1;40;1;5.5;0;50;60.25;25;30.125", text);
        Assert.True(CurveInputText.TryApply(text, back));
        Assert.Equal(CurveInputText.Format(input), CurveInputText.Format(back));
    }

    [Fact]
    public void Route_speed_is_an_empty_field()
    {
        var back = new CurveInput { DesignSpeed = 80 };

        Assert.True(CurveInputText.TryApply(CurveInputText.Format(new CurveInput()), back));
        Assert.Null(back.DesignSpeed);
        Assert.True(back.RunoffOnSpiral);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v2;;0;0;1;0;0;0;0")]
    [InlineData("v1;;2;0;1;0;0;0;0")]
    [InlineData("v1;x;0;0;1;0;0;0;0")]
    [InlineData("v1;;0;0;1;0;0;0")]
    public void Other_text_is_ignored(string text)
    {
        var input = new CurveInput { SuperRate = 3 };

        Assert.False(CurveInputText.TryApply(text, input));
        Assert.Equal(3, input.SuperRate);
    }
}
