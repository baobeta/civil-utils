using System;
using System.Collections.Generic;
using System.Linq;

namespace C3DTools.Core.Curves;

/// <summary>
/// Point and direction at any station of a designed route (tangents, clothoids, arcs), the same geometry
/// CurveGeometryBuilder draws. Curves with errors run straight through their PI.
/// </summary>
public sealed class RouteGeometry
{
    private readonly IReadOnlyList<PlanPoint> _pis;
    private readonly double _startStation;
    private readonly List<Piece> _pieces = new List<Piece>();

    public RouteGeometry(IReadOnlyList<PlanPoint> pis, double startStation, RouteDesign design)
    {
        _pis = pis ?? throw new ArgumentNullException(nameof(pis));
        if (design == null) throw new ArgumentNullException(nameof(design));
        if (pis.Count < 2) throw new ArgumentException("Tuyến cần ít nhất 2 đỉnh.", nameof(pis));
        _startStation = startStation;
        EndStation = design.EndStation;

        var point = pis[0];
        var station = startStation;
        foreach (var c in design.Curves.Where(c => c.Elements != null).OrderBy(c => c.PiIndex))
        {
            var input = c.Input;
            var g = CurveGeometryBuilder.Build(pis[c.PiIndex], pis[c.PiIndex - 1], pis[c.PiIndex + 1],
                input.Radius, input.SpiralIn, input.SpiralOut, c.Turn, c.Elements);
            _pieces.Add(Piece.Line(station, c.StationStart, point, g.Start));
            _pieces.Add(new Piece(c, g, pis[c.PiIndex - 1], pis[c.PiIndex], pis[c.PiIndex + 1]));
            point = g.End;
            station = c.StationEnd;
        }

        _pieces.Add(Piece.Line(station, EndStation, point, pis[pis.Count - 1]));
    }

    public double StartStation => _startStation;
    public double EndStation { get; }

    /// <summary>Point on the centreline; stations outside the route are clamped to its ends.</summary>
    public PlanPoint PointAt(double station) => Locate(station, out _);

    /// <summary>Unit direction of travel at the station.</summary>
    public PlanPoint DirectionAt(double station)
    {
        Locate(station, out var direction);
        return direction;
    }

    /// <summary>The point offset to the left (positive) or right (negative) of the centreline.</summary>
    public PlanPoint OffsetPoint(double station, double offset)
    {
        var p = Locate(station, out var d);
        return new PlanPoint(p.X - d.Y * offset, p.Y + d.X * offset);
    }

    private PlanPoint Locate(double station, out PlanPoint direction)
    {
        var s = Math.Max(_startStation, Math.Min(EndStation, station));
        var piece = _pieces.FirstOrDefault(p => s <= p.EndStation + 1e-9) ?? _pieces[_pieces.Count - 1];
        return piece.At(s, out direction);
    }

    private sealed class Piece
    {
        private readonly DesignedCurve _curve;
        private readonly CurveGeometry _g;
        private readonly PlanPoint _a, _b;   // a line's ends; the tangent directions of a curve
        private readonly double _startStation;

        private Piece(double startStation, double endStation, PlanPoint a, PlanPoint b)
        {
            _startStation = startStation;
            EndStation = endStation;
            _a = a;
            _b = b;
        }

        public Piece(DesignedCurve curve, CurveGeometry g, PlanPoint before, PlanPoint pi, PlanPoint after)
        {
            _curve = curve;
            _g = g;
            _startStation = curve.StationStart;
            EndStation = curve.StationEnd;
            _a = Unit(pi.X - before.X, pi.Y - before.Y);
            _b = Unit(after.X - pi.X, after.Y - pi.Y);
        }

        public double EndStation { get; }

        public static Piece Line(double startStation, double endStation, PlanPoint a, PlanPoint b) => new Piece(startStation, endStation, a, b);

        public PlanPoint At(double s, out PlanPoint direction)
        {
            if (_curve == null)
            {
                var length = EndStation - _startStation;
                direction = Distance(_a, _b) > 1e-12 ? Unit(_b.X - _a.X, _b.Y - _a.Y) : new PlanPoint(1, 0);
                var t = length > 1e-12 ? (s - _startStation) / length : 0;
                return new PlanPoint(_a.X + (_b.X - _a.X) * t, _a.Y + (_b.Y - _a.Y) * t);
            }

            var c = _curve;
            var input = c.Input;
            double sgn = -c.Turn;   // +1 = left turn (counter-clockwise)
            if (s < c.StationArcStart && input.SpiralIn > 0)
            {
                var l = s - c.StationStart;
                var xy = CurveGeometryBuilder.Clothoid(l, input.Radius, input.SpiralIn);
                var n = new PlanPoint(-_a.Y * sgn, _a.X * sgn);
                direction = Rotate(_a, sgn * l * l / (2 * input.Radius * input.SpiralIn));
                return new PlanPoint(_g.Start.X + _a.X * xy.X + n.X * xy.Y, _g.Start.Y + _a.Y * xy.X + n.Y * xy.Y);
            }

            if (s > c.StationArcEnd && input.SpiralOut > 0)
            {
                var l = c.StationEnd - s;
                var xy = CurveGeometryBuilder.Clothoid(l, input.Radius, input.SpiralOut);
                var n = new PlanPoint(-_b.Y * sgn, _b.X * sgn);
                direction = Rotate(_b, -sgn * l * l / (2 * input.Radius * input.SpiralOut));
                return new PlanPoint(_g.End.X - _b.X * xy.X + n.X * xy.Y, _g.End.Y - _b.Y * xy.X + n.Y * xy.Y);
            }

            var start = Math.Atan2(_g.ArcStart.Y - _g.ArcCentre.Y, _g.ArcStart.X - _g.ArcCentre.X);
            var angle = start + sgn * (s - c.StationArcStart) / input.Radius;
            var radial = new PlanPoint(Math.Cos(angle), Math.Sin(angle));
            direction = new PlanPoint(-radial.Y * sgn, radial.X * sgn);
            return new PlanPoint(_g.ArcCentre.X + radial.X * input.Radius, _g.ArcCentre.Y + radial.Y * input.Radius);
        }
    }

    internal static PlanPoint Rotate(PlanPoint v, double angle)
    {
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        return new PlanPoint(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    private static PlanPoint Unit(double x, double y)
    {
        var l = Math.Sqrt(x * x + y * y);
        return l < 1e-12 ? new PlanPoint(1, 0) : new PlanPoint(x / l, y / l);
    }

    private static double Distance(PlanPoint a, PlanPoint b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
}
