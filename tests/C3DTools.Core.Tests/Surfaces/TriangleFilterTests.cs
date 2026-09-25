using System.Collections.Generic;
using C3DTools.Core.Curves;
using C3DTools.Core.Surfaces;
using Xunit;

namespace C3DTools.Core.Tests.Surfaces;

public class TriangleFilterTests
{
    private static readonly List<PlanPoint> Square = new List<PlanPoint>
    {
        new PlanPoint(0, 0), new PlanPoint(100, 0), new PlanPoint(100, 100), new PlanPoint(0, 100),
    };

    // An L shape: the notch (60..100, 60..100) is outside.
    private static readonly List<PlanPoint> Ell = new List<PlanPoint>
    {
        new PlanPoint(0, 0), new PlanPoint(100, 0), new PlanPoint(100, 60), new PlanPoint(60, 60),
        new PlanPoint(60, 100), new PlanPoint(0, 100), new PlanPoint(0, 0),
    };

    [Theory]
    [InlineData(50, 50, true)]
    [InlineData(150, 50, false)]
    [InlineData(-0.001, 50, false)]
    [InlineData(0, 50, true)]      // on an edge
    [InlineData(100, 100, true)]   // on a vertex
    public void Point_in_a_square(double x, double y, bool inside)
    {
        Assert.Equal(inside, TriangleFilter.IsInside(new PlanPoint(x, y), Square));
    }

    [Theory]
    [InlineData(30, 30, true)]
    [InlineData(80, 80, false)]
    [InlineData(80, 30, true)]
    [InlineData(30, 80, true)]
    [InlineData(60, 80, true)]     // on the inner edge
    [InlineData(100, 60, true)]    // ray through a vertex
    [InlineData(-10, 60, false)]   // ray through two vertices at y = 60
    public void Point_in_a_concave_closed_polygon(double x, double y, bool inside)
    {
        Assert.Equal(inside, TriangleFilter.IsInside(new PlanPoint(x, y), Ell));
    }

    [Fact]
    public void Clockwise_polygon_works_too()
    {
        var cw = new List<PlanPoint>(Square);
        cw.Reverse();

        Assert.True(TriangleFilter.IsInside(new PlanPoint(10, 90), cw));
        Assert.False(TriangleFilter.IsInside(new PlanPoint(110, 90), cw));
    }

    [Fact]
    public void Flags_a_triangle_with_a_long_edge()
    {
        var flag = TriangleFilter.Check(new PlanPoint(0, 0), new PlanPoint(60, 0), new PlanPoint(0, 10), 50, null);

        Assert.Equal(TriangleFlag.LongEdge, flag);
        Assert.Equal(TriangleFlag.None, TriangleFilter.Check(new PlanPoint(0, 0), new PlanPoint(40, 0), new PlanPoint(0, 10), 50, null));
    }

    [Fact]
    public void Flags_a_triangle_with_a_vertex_outside_the_boundary()
    {
        var flag = TriangleFilter.Check(new PlanPoint(50, 50), new PlanPoint(90, 50), new PlanPoint(80, 80), 0, Ell);

        Assert.Equal(TriangleFlag.Outside, flag);
        Assert.Equal(TriangleFlag.LongEdge | TriangleFlag.Outside,
            TriangleFilter.Check(new PlanPoint(50, 50), new PlanPoint(150, 50), new PlanPoint(80, 50.1), 50, Ell));
    }

    [Fact]
    public void Zero_max_edge_and_no_boundary_flag_nothing()
    {
        Assert.Equal(TriangleFlag.None, TriangleFilter.Check(new PlanPoint(0, 0), new PlanPoint(1000, 0), new PlanPoint(0, 1000), 0, null));
    }

    [Fact]
    public void Edges_to_delete_are_distinct_long_or_outside_edges()
    {
        var edges = new List<(PlanPoint a, PlanPoint b)>
        {
            (new PlanPoint(0, 0), new PlanPoint(60, 0)),     // long
            (new PlanPoint(60, 0), new PlanPoint(0, 0)),     // same edge from the neighbour triangle
            (new PlanPoint(0, 0), new PlanPoint(10, 10)),    // keep
            (new PlanPoint(10, 10), new PlanPoint(80, 80)),  // one end outside the L
            (new PlanPoint(10, 10), new PlanPoint(20, 10)),  // keep
        };

        var result = TriangleFilter.EdgesToDelete(edges, 50, Ell);

        Assert.Equal(new[] { 0, 3 }, result);
    }
}
