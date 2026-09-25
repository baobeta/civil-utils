using System;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class CurveBoxPlacementTests
{
    private static PlanPoint P(double x, double y) => new PlanPoint(x, y);

    private static double Distance(PlanPoint a, PlanPoint b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

    // Square route, left turn at (100,0); curve midpoint of an R50 arc.
    private static readonly PlanPoint Pi = P(100, 0);
    private static readonly PlanPoint Mid = P(50 + 50 * Math.Cos(-Math.PI / 4), 50 + 50 * Math.Sin(-Math.PI / 4));

    private static BoxPlacement Square() => CurveBoxPlacement.Place(Pi, P(0, 0), P(100, 100), Mid, 30, 15, 2.5);

    [Fact]
    public void Box_sits_outside_the_pi()
    {
        var b = Square();

        Assert.True(b.Centre.X > 100);
        Assert.True(b.Centre.Y < 0);
        Assert.Equal(4 * 2.5 + 15 / 2.0, Distance(Pi, b.Centre), 9);
    }

    [Fact]
    public void Rotation_puts_the_box_vertical_axis_on_the_bisector_and_reads_upright()
    {
        // out points to −45°; −45° − 90° = −135° is upside down, so it flips to +45° (as ytc:bang does).
        Assert.Equal(Math.PI / 4, Square().Rotation, 9);
    }

    [Fact]
    public void Leader_runs_from_curve_midpoint_to_the_near_edge_of_the_box()
    {
        var b = Square();

        Assert.Equal(Mid.X, b.LeaderStart.X, 9);
        Assert.Equal(Mid.Y, b.LeaderStart.Y, 9);
        Assert.Equal(4 * 2.5, Distance(Pi, b.LeaderEnd), 9);
        Assert.Equal(Distance(Pi, b.Centre), Distance(Pi, b.LeaderEnd) + Distance(b.LeaderEnd, b.Centre), 9);
    }

    [Fact]
    public void Bisector_pointing_down_gives_a_readable_rotation()
    {
        var b = CurveBoxPlacement.Place(P(0, 0), P(-100, 100), P(100, 100), P(0, 20), 30, 15, 2.5);

        Assert.True(b.Centre.Y < 0);
        Assert.InRange(b.Rotation, -Math.PI / 2 + 1e-9, Math.PI / 2);
        Assert.Equal(0, b.Rotation, 9);
    }

    [Fact]
    public void Straight_through_pi_throws_instead_of_returning_nan()
    {
        Assert.Throws<System.ArgumentException>(() =>
            CurveBoxPlacement.Place(P(50, 0), P(0, 0), P(100, 0), P(50, 0), 30, 15, 2.5));
    }

    [Fact]
    public void Corners_frame_the_box()
    {
        var b = Square();

        Assert.Equal(4, b.Corners.Count);
        foreach (var c in b.Corners)
            Assert.Equal(Math.Sqrt(15 * 15 + 7.5 * 7.5), Distance(b.Centre, c), 9);
        Assert.Equal(30, Distance(b.Corners[0], b.Corners[1]), 9);
        Assert.Equal(15, Distance(b.Corners[1], b.Corners[2]), 9);
    }

    [Fact]
    public void Frame_size_follows_ytc_bang()
    {
        Assert.Equal(1.7 * 2.5, CurveBoxPlacement.LineSpacing(2.5), 9);
        Assert.Equal(30 + 5, CurveBoxPlacement.FrameWidth(30, 2.5), 9);
        Assert.Equal(5 * 1.7 * 2.5 + 0.6 * 2.5, CurveBoxPlacement.FrameHeight(5, 2.5), 9);
    }
}
