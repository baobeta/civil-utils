using System;
using System.Linq;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class CurveGeometryBuilderTests
{
    private static PlanPoint P(double x, double y) => new PlanPoint(x, y);

    private static double Distance(PlanPoint a, PlanPoint b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

    private static CurveGeometry Build(PlanPoint[] pis, CurveInput input)
    {
        var c = RouteDesigner.Design(pis, 0, new[] { input }, 60, null).Curves[0];
        return CurveGeometryBuilder.Build(pis[1], pis[0], pis[2], input.Radius, input.SpiralIn, input.SpiralOut, c.Turn, c.Elements);
    }

    [Fact]
    public void Circular_curve_on_the_square_route()
    {
        var g = Build(new[] { P(0, 0), P(100, 0), P(100, 100) }, new CurveInput { Radius = 50 });

        Assert.Equal(50, g.ArcCentre.X, 9);
        Assert.Equal(50, g.ArcCentre.Y, 9);
        Assert.Equal(50, Distance(g.ArcCentre, g.ArcStart), 9);
        Assert.Equal(50, Distance(g.ArcCentre, g.ArcEnd), 9);
        Assert.Equal(50, g.ArcStart.X, 9);      // ArcStart == NĐ when L = 0
        Assert.Equal(0, g.ArcStart.Y, 9);
        Assert.Equal(100, g.ArcEnd.X, 9);
        Assert.Equal(50, g.ArcEnd.Y, 9);
        Assert.Empty(g.SpiralIn);
        Assert.Empty(g.SpiralOut);
        Assert.Equal(50, Distance(g.ArcCentre, g.Mid), 9);
    }

    [Fact]
    public void Arc_angles_run_counter_clockwise_like_the_lisp_arc()
    {
        var left = Build(new[] { P(0, 0), P(100, 0), P(100, 100) }, new CurveInput { Radius = 50 });
        Assert.Equal(-Math.PI / 2, left.ArcDrawStartAngle, 9);   // TĐ → TC
        Assert.Equal(0, left.ArcDrawEndAngle, 9);
        Assert.Equal(50, left.Start.X, 9);   // NĐ == TĐ when L = 0
        Assert.Equal(50, left.End.Y, 9);

        var right = Build(new[] { P(0, 0), P(100, 0), P(100, -100) }, new CurveInput { Radius = 50 });
        Assert.Equal(0, right.ArcDrawStartAngle, 9);             // TC → TĐ
        Assert.Equal(Math.PI / 2, right.ArcDrawEndAngle, 9);
    }

    [Fact]
    public void Right_turn_puts_the_centre_on_the_right()
    {
        var g = Build(new[] { P(0, 0), P(100, 0), P(100, -100) }, new CurveInput { Radius = 50 });

        Assert.Equal(50, g.ArcCentre.X, 9);
        Assert.Equal(-50, g.ArcCentre.Y, 9);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void Spirals_meet_the_arc_for_symmetric_scs(int turn)
    {
        var a = 60 * Math.PI / 180;
        var pis = new[] { P(0, 0), P(300, 0), P(300 + 300 * Math.Cos(a), turn * -300 * Math.Sin(a)) };
        var g = Build(pis, new CurveInput { Radius = 200, SpiralIn = 50, SpiralOut = 50 });

        Assert.Equal(21, g.SpiralIn.Count);
        Assert.Equal(21, g.SpiralOut.Count);
        Assert.True(Math.Abs(Distance(g.ArcCentre, g.SpiralIn.Last()) - 200) < 0.001);
        Assert.True(Math.Abs(Distance(g.ArcCentre, g.SpiralOut.First()) - 200) < 0.001);
        Assert.Equal(g.ArcStart, g.SpiralIn.Last());
        Assert.Equal(g.ArcEnd, g.SpiralOut.First());

        // NĐ on tangent 1 at T1 before the PI, NC on tangent 2 at T2 after it.
        Assert.Equal(300 - 140.76, g.SpiralIn[0].X, 2);
        Assert.Equal(0, g.SpiralIn[0].Y, 9);
        Assert.Equal(140.76, Distance(pis[1], g.SpiralOut.Last()), 2);
        Assert.Equal(200, Distance(g.ArcCentre, g.Mid), 9);
    }

    [Fact]
    public void Asymmetric_spirals_meet_the_arc()
    {
        var a = Math.PI / 4;
        var pis = new[] { P(0, 0), P(500, 0), P(500 + 500 * Math.Cos(a), 500 * Math.Sin(a)) };
        var g = Build(pis, new CurveInput { Radius = 300, SpiralIn = 60, SpiralOut = 40 });

        Assert.True(Math.Abs(Distance(g.ArcCentre, g.ArcStart) - 300) < 0.001);
        Assert.True(Math.Abs(Distance(g.ArcCentre, g.ArcEnd) - 300) < 0.001);
    }

    [Fact]
    public void Coincident_points_throw_instead_of_returning_nan()
    {
        var e = CurveElementsCalculator.Compute(50, Math.PI / 2, 0, 0);

        Assert.Throws<ArgumentException>(() =>
            CurveGeometryBuilder.Build(P(100, 0), P(100, 0), P(100, 100), 50, 0, 0, -1, e));
    }

    [Fact]
    public void Clothoid_series_matches_ytc_clo()
    {
        // At l = L the local offset is close to L²/(6RL) = L/(6R).
        var p = CurveGeometryBuilder.Clothoid(50, 200, 50);

        Assert.Equal(50 - Math.Pow(50, 5) / (40 * Math.Pow(200 * 50, 2)) + Math.Pow(50, 9) / (3456 * Math.Pow(200 * 50, 4)), p.X, 9);
        Assert.Equal(Math.Pow(50, 3) / (6 * 200 * 50) - Math.Pow(50, 7) / (336 * Math.Pow(200 * 50, 3))
            + Math.Pow(50, 11) / (42240 * Math.Pow(200 * 50, 5)), p.Y, 9);
    }
}
