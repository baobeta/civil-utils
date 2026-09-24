using System;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class PiExtractorTests
{
    private static PlanPoint P(double x, double y) => new PlanPoint(x, y);

    [Fact]
    public void Intersect_of_two_perpendicular_lines()
    {
        var pi = PiExtractor.Intersect(P(0, 0), P(10, 0), P(50, -20), P(50, 40));
        Assert.Equal(50, pi.X, 9);
        Assert.Equal(0, pi.Y, 9);
    }

    [Fact]
    public void Parallel_lines_throw_with_a_Vietnamese_message()
    {
        var ex = Assert.Throws<ArgumentException>(() => PiExtractor.Intersect(P(0, 0), P(10, 0), P(0, 5), P(20, 5)));
        Assert.Contains("song song", ex.Message);
    }

    [Fact]
    public void FromTangents_returns_start_intersections_and_end()
    {
        // Three tangents with gaps between them, as when curves sit between the lines.
        var pis = PiExtractor.FromTangents(new[]
        {
            (P(0, 0), P(80, 0)),
            (P(100, 20), P(100, 80)),
            (P(120, 100), P(200, 100)),
        });

        Assert.Equal(4, pis.Count);
        Assert.Equal(0, pis[0].X, 9);
        Assert.Equal(0, pis[0].Y, 9);
        Assert.Equal(100, pis[1].X, 9);
        Assert.Equal(0, pis[1].Y, 9);
        Assert.Equal(100, pis[2].X, 9);
        Assert.Equal(100, pis[2].Y, 9);
        Assert.Equal(200, pis[3].X, 9);
        Assert.Equal(100, pis[3].Y, 9);
    }

    [Fact]
    public void FromTangents_with_one_line_gives_its_ends()
    {
        var pis = PiExtractor.FromTangents(new[] { (P(1, 2), P(3, 4)) });
        Assert.Equal(2, pis.Count);
        Assert.Equal(3, pis[1].X, 9);
    }

    [Fact]
    public void Measure_gives_tangent_lengths_and_external_distance()
    {
        // 90° simple arc R = 10 around PI (10, 0): NĐ (0,0), NC (10,10), centre (0,10).
        var mid = P(10 * Math.Cos(-Math.PI / 4), 10 + 10 * Math.Sin(-Math.PI / 4));
        var m = PiExtractor.Measure(P(0, 0), P(1, 0), P(10, 10), P(10, 9), mid);
        Assert.Equal(10, m.Pi.X, 9);
        Assert.Equal(0, m.Pi.Y, 9);
        Assert.Equal(10, m.T1, 9);
        Assert.Equal(10, m.T2, 9);
        Assert.Equal(10 * Math.Sqrt(2) - 10, m.P, 9);
    }
}
