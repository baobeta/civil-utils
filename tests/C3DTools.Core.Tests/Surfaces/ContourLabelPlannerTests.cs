using System;
using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Curves;
using C3DTools.Core.Surfaces;
using Xunit;

namespace C3DTools.Core.Tests.Surfaces;

public class ContourLabelPlannerTests
{
    private static readonly List<PlanPoint> Line = new List<PlanPoint> { new PlanPoint(0, 0), new PlanPoint(100, 0) };

    /// <summary>Vertical contours x = 10, 20, … 90 at elevations 101 … 109.</summary>
    private static List<ContourLine> Vertical() =>
        Enumerable.Range(1, 9).Select(i => new ContourLine(100 + i, new List<PlanPoint> { new PlanPoint(10 * i, -50), new PlanPoint(10 * i, 50) })).ToList();

    private static ContourLabelOptions Options(double spacing = 0, bool minor = true) =>
        new ContourLabelOptions { MajorInterval = 5, Decimals = 2, Spacing = spacing, IncludeMinor = minor };

    [Fact]
    public void Labels_every_crossing_in_order_along_the_line()
    {
        var labels = ContourLabelPlanner.Plan(Line, Vertical(), Options());

        Assert.Equal(9, labels.Count);
        Assert.Equal(new[] { 10.0, 20, 30, 40, 50, 60, 70, 80, 90 }, labels.Select(l => l.Distance).ToArray());
        Assert.Equal("101", labels[0].Text);
        Assert.Equal(10, labels[0].Position.X, 9);
        Assert.Equal(0, labels[0].Position.Y, 9);
    }

    [Fact]
    public void Major_contours_are_multiples_of_the_major_interval()
    {
        var labels = ContourLabelPlanner.Plan(Line, Vertical(), Options());

        Assert.Equal(new[] { 105.0 }, labels.Where(l => l.IsMajor).Select(l => l.Elevation).ToArray());
        Assert.True(ContourLabelPlanner.IsMajor(110.0000001, 5));
        Assert.False(ContourLabelPlanner.IsMajor(112.5, 5));
    }

    [Fact]
    public void Only_major_labels_when_minor_is_off()
    {
        var labels = ContourLabelPlanner.Plan(Line, Vertical(), Options(minor: false));

        Assert.Single(labels);
        Assert.Equal("105", labels[0].Text);
    }

    [Fact]
    public void Spacing_skips_close_labels_but_keeps_majors_first()
    {
        // Spacing 25: major at 50 first, then minors 25 m away from every kept label: 10 (40 from 50), 80, then 30? no (20 from 10 and 50).
        var labels = ContourLabelPlanner.Plan(Line, Vertical(), Options(spacing: 25));

        Assert.Equal(new[] { 10.0, 50, 80 }, labels.Select(l => l.Distance).ToArray());
        Assert.True(labels[1].IsMajor);
    }

    [Fact]
    public void Text_keeps_up_to_the_preset_decimals()
    {
        var contours = new List<ContourLine> { new ContourLine(12.5, new List<PlanPoint> { new PlanPoint(30, -1), new PlanPoint(30, 1) }) };

        var labels = ContourLabelPlanner.Plan(Line, contours, Options());

        Assert.Equal("12.5", labels[0].Text);
        TestCulture.Run("vi-VN", () => Assert.Equal("12.5", ContourLabelPlanner.Plan(Line, contours, Options())[0].Text));
    }

    [Fact]
    public void Rotation_follows_the_contour_and_stays_readable()
    {
        // Contour drawn top to bottom (angle −90°) and one drawn right-to-left diagonally (135° → −45°).
        var contours = new List<ContourLine>
        {
            new ContourLine(101, new List<PlanPoint> { new PlanPoint(20, 10), new PlanPoint(20, -10) }),
            new ContourLine(102, new List<PlanPoint> { new PlanPoint(50, -10), new PlanPoint(30, 10) }),
        };

        var labels = ContourLabelPlanner.Plan(Line, contours, Options());

        Assert.Equal(Math.PI / 2, labels[0].Rotation, 9);
        Assert.Equal(-Math.PI / 4, labels[1].Rotation, 9);
        Assert.Equal(40, labels[1].Position.X, 9);
    }

    [Fact]
    public void Polyline_line_and_contour_crossing_twice()
    {
        var line = new List<PlanPoint> { new PlanPoint(0, 0), new PlanPoint(100, 0), new PlanPoint(100, 100) };
        var ring = new List<PlanPoint> { new PlanPoint(50, -10), new PlanPoint(110, -10), new PlanPoint(110, 50), new PlanPoint(50, 50), new PlanPoint(50, -10) };

        var labels = ContourLabelPlanner.Plan(line, new[] { new ContourLine(103, ring) }, Options());

        Assert.Equal(new[] { 50.0, 150 }, labels.Select(l => l.Distance).ToArray());
    }

    [Fact]
    public void A_crossing_at_a_contour_vertex_is_labelled_once()
    {
        var contour = new List<PlanPoint> { new PlanPoint(40, -10), new PlanPoint(40, 0), new PlanPoint(45, 10) };

        var labels = ContourLabelPlanner.Plan(Line, new[] { new ContourLine(101, contour) }, Options());

        Assert.Single(labels);
        Assert.Equal(40, labels[0].Distance, 9);
    }

    [Fact]
    public void Parallel_contour_gives_no_label()
    {
        var contour = new List<PlanPoint> { new PlanPoint(0, 5), new PlanPoint(100, 5) };

        Assert.Empty(ContourLabelPlanner.Plan(Line, new[] { new ContourLine(101, contour) }, Options()));
    }
}
