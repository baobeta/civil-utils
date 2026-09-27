using System;
using System.Linq;
using C3DTools.Core.Curves;
using C3DTools.Core.Profiles;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class RouteGeometryTests
{
    private static PlanPoint P(double x, double y) => new PlanPoint(x, y);
    private static readonly PlanPoint[] Right = { P(0, 0), P(300, 0), P(300, -300) };
    private static readonly PlanPoint[] Left = { P(0, 0), P(300, 0), P(300, 300) };

    private static double Dist(PlanPoint a, PlanPoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(40, 40)]
    [InlineData(20, 20)]
    public void Matches_the_drawn_curve_geometry(double l1, double l2)
    {
        var input = new CurveInput { Radius = 100, SpiralIn = l1, SpiralOut = l2 };
        var design = RouteDesigner.Design(Right, 10, new[] { input }, 60, null);
        var c = design.Curves[0];
        var g = CurveGeometryBuilder.Build(Right[1], Right[0], Right[2], 100, l1, l2, c.Turn, c.Elements);
        var route = new RouteGeometry(Right, 10, design);

        Assert.True(Dist(g.Start, route.PointAt(c.StationStart)) < 1e-6);
        Assert.True(Dist(g.ArcStart, route.PointAt(c.StationArcStart)) < 1e-6);
        Assert.True(Dist(g.Mid, route.PointAt(c.StationArcMid)) < 1e-6);
        Assert.True(Dist(g.ArcEnd, route.PointAt(c.StationArcEnd)) < 1e-6);
        Assert.True(Dist(g.End, route.PointAt(c.StationEnd)) < 1e-6);
        Assert.True(Dist(P(300, -300), route.PointAt(design.EndStation)) < 1e-6);
        Assert.True(Dist(P(0, 0), route.PointAt(10)) < 1e-9);
    }

    [Theory]
    [InlineData(40, 40)]
    [InlineData(30, 10)]
    public void Left_turn_matches_the_drawn_geometry_too(double l1, double l2)
    {
        var design = RouteDesigner.Design(Left, 0, new[] { new CurveInput { Radius = 100, SpiralIn = l1, SpiralOut = l2 } }, 60, null);
        var c = design.Curves[0];
        var g = CurveGeometryBuilder.Build(Left[1], Left[0], Left[2], 100, l1, l2, c.Turn, c.Elements);
        var route = new RouteGeometry(Left, 0, design);

        Assert.Equal(-1, c.Turn);
        foreach (var (name, point, station) in new[] { ("NĐ", g.Start, c.StationStart), ("TĐ", g.ArcStart, c.StationArcStart), ("TC", g.ArcEnd, c.StationArcEnd), ("NC", g.End, c.StationEnd) })
            Assert.True(Dist(point, route.PointAt(station)) < 1e-6, $"{name}: {Dist(point, route.PointAt(station))}");
        // g.Mid is the arc point nearest the PI; with L1 ≠ L2 that is not the arc's station midpoint, so only check the radius there.
        Assert.Equal(100, Dist(g.ArcCentre, route.PointAt(c.StationArcMid)), 6);
        var quarter = route.PointAt(c.StationStart + l1 / 4);   // on the entry spiral, bending left (+Y)
        Assert.True(quarter.Y > 0 && quarter.Y < 1);
        Assert.Equal(1, route.DirectionAt(design.EndStation - 1).Y, 9);
    }

    [Fact]
    public void Left_turn_edge_lines_put_Wb_on_the_left()
    {
        var input = new CurveInput { Radius = 100, SpiralIn = 40, SpiralOut = 40, Wb = 1.2, Wl = 0.4 };
        var design = RouteDesigner.Design(Left, 0, new[] { input }, 60, null);
        var c = design.Curves[0];
        var g = CurveGeometryBuilder.Build(Left[1], Left[0], Left[2], 100, 40, 40, c.Turn, c.Elements);

        var lines = EdgeLineBuilder.Build(new RouteGeometry(Left, 0, design), design, 3.5, 1);

        var inside = lines.Single(l => l.IsInside);
        Assert.True(inside.IsLeft);
        Assert.Equal(3.5, inside.Points[0].Y, 6);
        Assert.Contains(inside.Points, p => Math.Abs(Dist(p, g.ArcCentre) - (100 - 3.5 - 1.2)) < 1e-6);
        Assert.Contains(lines.Single(l => !l.IsInside).Points, p => Math.Abs(Dist(p, g.ArcCentre) - (100 + 3.5 + 0.4)) < 1e-6);
    }

    [Fact]
    public void Direction_turns_with_the_route_and_offsets_go_left_for_positive()
    {
        var design = RouteDesigner.Design(Right, 0, new[] { new CurveInput { Radius = 100, SpiralIn = 40, SpiralOut = 40 } }, 60, null);
        var route = new RouteGeometry(Right, 0, design);
        var c = design.Curves[0];

        Assert.Equal(1, route.DirectionAt(5).X, 9);
        Assert.Equal(-1, route.DirectionAt(design.EndStation - 5).Y, 9);
        var mid = route.DirectionAt(c.StationArcMid);
        Assert.Equal(Math.Sqrt(0.5), mid.X, 6);
        Assert.Equal(-Math.Sqrt(0.5), mid.Y, 6);
        Assert.Equal(3.5, route.OffsetPoint(5, 3.5).Y, 9);   // left of +X is +Y
    }

    [Fact]
    public void Edge_lines_widen_the_inside_by_Wb_and_the_outside_by_Wl()
    {
        var input = new CurveInput { Radius = 100, SpiralIn = 40, SpiralOut = 40, Wb = 1.2, Wl = 0.4 };
        var design = RouteDesigner.Design(Right, 0, new[] { input }, 60, null);
        var route = new RouteGeometry(Right, 0, design);
        var c = design.Curves[0];
        var g = CurveGeometryBuilder.Build(Right[1], Right[0], Right[2], 100, 40, 40, c.Turn, c.Elements);

        var lines = EdgeLineBuilder.Build(route, design, 3.5, 1);

        Assert.Equal(2, lines.Count);
        var inside = lines.Single(l => l.IsInside);
        Assert.False(inside.IsLeft);   // right turn: centre on the right
        Assert.Equal(-3.5, inside.Points[0].Y, 6);   // starts at the normal edge on the tangent
        var closest = inside.Points.Min(p => Math.Abs(Dist(p, g.ArcCentre) - (100 - 3.5 - 1.2)));
        Assert.True(closest < 1e-6);
        var outside = lines.Single(l => !l.IsInside);
        Assert.Contains(outside.Points, p => Math.Abs(Dist(p, g.ArcCentre) - (100 + 3.5 + 0.4)) < 1e-6);
        Assert.Empty(EdgeLineBuilder.Build(route, RouteDesigner.Design(Right, 0, new[] { new CurveInput { Radius = 100 } }, 60, null), 3.5, 1));
    }

    [Fact]
    public void SetDeflection_rotates_only_what_follows_the_PI()
    {
        var pis = new[] { P(0, 0), P(100, 0), P(100, 100), P(200, 100) };   // left 90°, then right 90°

        var edited = PiEditor.SetDeflection(pis, 1, Math.PI / 3);

        Assert.Equal(Math.PI / 3, PiEditor.Deflection(edited, 1), 9);
        Assert.Equal(Math.PI / 2, PiEditor.Deflection(edited, 2), 9);
        Assert.Equal(100, Dist(edited[1], edited[2]), 9);
        Assert.Equal(pis[1].X, edited[1].X);
        Assert.True(edited[2].Y > 0);   // still a left turn
        Assert.Throws<ArgumentOutOfRangeException>(() => PiEditor.SetDeflection(pis, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PiEditor.SetDeflection(pis, 1, Math.PI));
    }

    [Theory]
    [InlineData(true, 40, 10)]
    [InlineData(false, 40, 10)]
    [InlineData(true, 0, 30)]
    public void Direction_is_continuous_at_TD_and_TC(bool left, double l1, double l2)
    {
        var pis = left ? Left : Right;
        var design = RouteDesigner.Design(pis, 0, new[] { new CurveInput { Radius = 100, SpiralIn = l1, SpiralOut = l2 } }, 60, null);
        var route = new RouteGeometry(pis, 0, design);
        var c = design.Curves[0];

        foreach (var station in new[] { c.StationStart, c.StationArcStart, c.StationArcEnd, c.StationEnd })
        {
            var justBefore = route.DirectionAt(station - 1e-6);
            var justAfter = route.DirectionAt(station + 1e-6);
            Assert.True(Dist(justBefore, justAfter) < 1e-4, $"hướng gãy tại {station}: {Dist(justBefore, justAfter)}");
        }
    }

    [Fact]
    public void Station_shift_handles_a_removed_and_an_added_curve()
    {
        var pis = new[] { P(0, 0), P(300, 0), P(300, 300), P(600, 300) };
        var both = RouteDesigner.Design(pis, 0, new[] { new CurveInput { Radius = 50 }, new CurveInput { Radius = 50 } }, 60, null);
        var firstOnly = RouteDesigner.Design(pis, 0, new[] { new CurveInput { Radius = 50 }, new CurveInput { NoCurve = true } }, 60, null);

        // Đ2 removed: a point 100 m before the last PI keeps its place on the ground.
        var removed = new StationShift(both.Curves, firstOnly.Curves);
        var oldStation = both.Curves[1].StationEnd + (300 - both.Curves[1].Elements.T2) - 100;
        Assert.Equal(firstOnly.EndStation - 100, removed.Map(oldStation), 6);
        Assert.Equal(firstOnly.EndStation, removed.Map(both.EndStation), 6);

        // Đ2 added: the reverse mapping.
        var added = new StationShift(firstOnly.Curves, both.Curves);
        Assert.Equal(both.EndStation - 100, added.Map(firstOnly.EndStation - 100), 6);
        Assert.Equal(firstOnly.Curves[0].StationEnd + 10, added.Map(firstOnly.Curves[0].StationEnd + 10), 6);   // before Đ2 nothing moves
    }

    [Fact]
    public void Station_shift_moves_later_stations_by_the_chainage_change()
    {
        var pis = new[] { P(0, 0), P(300, 0), P(300, 300), P(600, 300) };
        var before = RouteDesigner.Design(pis, 0, new[] { new CurveInput { Radius = 50 }, new CurveInput { Radius = 50 } }, 60, null);
        var after = RouteDesigner.Design(pis, 0, new[] { new CurveInput { Radius = 80 }, new CurveInput { Radius = 50 } }, 60, null);
        var shift = new StationShift(before.Curves, after.Curves);

        // A point 100 m past the PI on the middle tangent keeps its place on the ground.
        var oldStation = before.Curves[0].StationEnd + (100 - before.Curves[0].Elements.T2);
        var newStation = after.Curves[0].StationEnd + (100 - after.Curves[0].Elements.T2);
        Assert.Equal(newStation, shift.Map(oldStation), 6);
        Assert.Equal(20, shift.Map(20));   // before the first curve nothing moves
        Assert.Equal(after.EndStation, shift.Map(before.EndStation), 6);
        Assert.Equal(after.EndStation - before.EndStation, shift.EndShift, 6);
        Assert.False(shift.IsIdentity);
        Assert.True(new StationShift(before.Curves, before.Curves).IsIdentity);
    }
}
