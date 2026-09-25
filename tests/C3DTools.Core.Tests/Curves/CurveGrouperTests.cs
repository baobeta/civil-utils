using System;
using System.Collections.Generic;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class CurveGrouperTests
{
    private static AlignmentSegment Line(double start, double length) =>
        new AlignmentSegment(SegmentKind.Line, start, length, 0, 0);
    private static AlignmentSegment Arc(double start, double length, double r, int turn = 1) =>
        new AlignmentSegment(SegmentKind.Arc, start, length, r, turn);
    private static AlignmentSegment Spiral(double start, double length, double r, int turn = 1) =>
        new AlignmentSegment(SegmentKind.Spiral, start, length, r, turn);

    [Fact]
    public void Groups_spiral_arc_spiral_into_one_curve()
    {
        var segments = new List<AlignmentSegment>
        {
            Line(0, 100),
            Spiral(100, 50, 200),
            Arc(150, 159.43951, 200),
            Spiral(309.43951, 50, 200),
            Line(359.43951, 80),
        };

        var result = CurveGrouper.Group(segments);

        Assert.Empty(result.Warnings);
        var g = Assert.Single(result.Groups);
        Assert.Equal(1, g.Index);
        Assert.Equal(200, g.Radius, 6);
        Assert.Equal(50, g.SpiralIn, 6);
        Assert.Equal(50, g.SpiralOut, 6);
        Assert.Equal(60, g.DeltaDegrees, 4);
        Assert.Equal(100, g.StartStation, 6);        // NĐ
        Assert.Equal(150, g.ArcStartStation, 6);     // TĐ
        Assert.Equal(309.43951, g.ArcEndStation, 5); // TC
        Assert.Equal(359.43951, g.EndStation, 5);    // NC
    }

    [Fact]
    public void Simple_arc_is_a_curve_without_spirals()
    {
        var result = CurveGrouper.Group(new[] { Line(0, 50), Arc(50, 157.0796, 100, -1), Line(207.0796, 50) });

        var g = Assert.Single(result.Groups);
        Assert.Equal(0, g.SpiralIn);
        Assert.Equal(0, g.SpiralOut);
        Assert.Equal(90, g.DeltaDegrees, 3);
        Assert.Equal(-1, g.Turn);
    }

    [Fact]
    public void Numbers_curves_in_station_order()
    {
        var result = CurveGrouper.Group(new[]
        {
            Line(0, 10), Arc(10, 20, 100), Line(30, 10), Arc(40, 20, 150), Line(60, 10),
        });

        Assert.Equal(new[] { 1, 2 }, result.Groups.ConvertAll(g => g.Index));
    }

    [Fact]
    public void Compound_curve_is_skipped_with_warning()
    {
        var result = CurveGrouper.Group(new[] { Line(0, 10), Arc(10, 20, 100), Arc(30, 20, 150), Line(50, 10) });

        Assert.Empty(result.Groups);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("Km0+010.00", warning);
    }

    [Fact]
    public void Spiral_that_does_not_match_arc_radius_is_skipped()
    {
        var result = CurveGrouper.Group(new[] { Spiral(0, 50, 250), Arc(50, 100, 200), Line(150, 10) });

        Assert.Empty(result.Groups);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Spiral_only_run_is_skipped()
    {
        var result = CurveGrouper.Group(new[] { Line(0, 10), Spiral(10, 40, 200), Spiral(50, 40, 200), Line(90, 10) });

        Assert.Empty(result.Groups);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => CurveGrouper.Group(null));
    }
}
