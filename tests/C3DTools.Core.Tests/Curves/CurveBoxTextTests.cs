using System;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class CurveBoxTextTests
{
    private static PlanPoint P(double x, double y) => new PlanPoint(x, y);

    private static DesignedCurve Symmetric60(double radius = 200, double spiral = 50)
    {
        var a = 60 * Math.PI / 180;
        var pis = new[] { P(0, 0), P(300, 0), P(300 + 300 * Math.Cos(a), 300 * Math.Sin(a)) };
        var input = new CurveInput { Radius = radius, SpiralIn = spiral, SpiralOut = spiral, Wb = 0.6, Wl = 0 };
        return RouteDesigner.Design(pis, 0, new[] { input }, 60, null).Curves[0];
    }

    private static readonly string[] Expected =
    {
        "A=60°00'00\"  P=31.54",
        "R=200  K=259.44",
        "T1=140.76  T2=140.76",
        "L1=50  L2=50",
        "Wb=0.6  Wl=0",
    };

    [Fact]
    public void Builds_the_lisp_box_layout()
    {
        Assert.Equal(Expected, CurveBoxText.Build(Symmetric60(), new CurveBoxOptions()));
    }

    [Fact]
    public void Ignores_vietnamese_culture()
    {
        TestCulture.Run("vi-VN", () => Assert.Equal(Expected, CurveBoxText.Build(Symmetric60(), new CurveBoxOptions())));
    }

    [Fact]
    public void Error_row_shows_question_marks_for_computed_elements()
    {
        var c = Symmetric60(spiral: 300);   // L too long for α
        Assert.Null(c.Elements);

        var lines = CurveBoxText.Build(c, new CurveBoxOptions());

        Assert.Equal(new[]
        {
            "A=60°00'00\"  P=?",
            "R=200  K=?",
            "T1=?  T2=?",
            "L1=300  L2=300",
            "Wb=0.6  Wl=0",
        }, lines);
    }
}
