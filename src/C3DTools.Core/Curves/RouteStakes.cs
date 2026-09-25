using System;
using System.Collections.Generic;
using C3DTools.Core.Stations;

namespace C3DTools.Core.Curves;

public enum StakeKind { Start, Nd, Td, P, Tc, Nc, End }

/// <summary>One stake (cọc) of c:YTC: where it is, which way the route runs there, and which side the tick goes.</summary>
public sealed class Stake
{
    public StakeKind Kind { get; set; }

    /// <summary>Đ number; 0 for the start and end stakes.</summary>
    public int CurveNumber { get; set; }

    public double Station { get; set; }
    public PlanPoint Point { get; set; }

    /// <summary>Unit direction of travel at the stake.</summary>
    public PlanPoint Direction { get; set; }

    /// <summary>+1: tick on the left of Direction, -1: on the right (ytc:coc's side).</summary>
    public int Side { get; set; }

    /// <summary>"NĐ1", "TĐ1", "P1", "TC1", "NC1"; empty for the start and end stakes.</summary>
    public string Name => Kind switch
    {
        StakeKind.Nd => "NĐ" + CurveNumber,
        StakeKind.Td => "TĐ" + CurveNumber,
        StakeKind.P => "P" + CurveNumber,
        StakeKind.Tc => "TC" + CurveNumber,
        StakeKind.Nc => "NC" + CurveNumber,
        _ => "",
    };
}

/// <summary>Drawing of one stake: tick line and two texts (ytc:coc).</summary>
public sealed class StakeDrawing
{
    public PlanPoint TickStart { get; set; }
    public PlanPoint TickEnd { get; set; }
    public PlanPoint StationTextPoint { get; set; }
    public PlanPoint NameTextPoint { get; set; }
    public double Rotation { get; set; }
    public string StationText { get; set; }
    public string NameText { get; set; }
}

public static class RouteStakes
{
    /// <summary>Every stake of the route, from the PI polygon and the designed curves (curves with errors are skipped).</summary>
    public static List<Stake> Build(IReadOnlyList<PlanPoint> pis, double startStation, RouteDesign design)
    {
        if (pis == null) throw new ArgumentNullException(nameof(pis));
        if (design == null) throw new ArgumentNullException(nameof(design));
        if (pis.Count < 2) throw new ArgumentException("Tuyến cần ít nhất 2 đỉnh.", nameof(pis));

        var stakes = new List<Stake>
        {
            new Stake { Kind = StakeKind.Start, Station = startStation, Point = pis[0], Direction = Unit(pis[0], pis[1]), Side = 1 },
        };

        foreach (var c in design.Curves)
        {
            if (c.Elements == null) continue;
            PlanPoint before = pis[c.PiIndex - 1], pi = pis[c.PiIndex], after = pis[c.PiIndex + 1];
            var input = c.Input;
            var g = CurveGeometryBuilder.Build(pi, before, after, input.Radius, input.SpiralIn, input.SpiralOut, c.Turn, c.Elements);
            var v1 = Unit(before, pi);
            var v2 = Unit(pi, after);
            double sgn = -c.Turn;
            var beta1 = input.SpiralIn / (2 * input.Radius);
            var beta2 = input.SpiralOut / (2 * input.Radius);

            // c:YTC: curve stakes point away from the centre (side -sgn), the P stake towards it (side sgn).
            if (input.SpiralIn > 0) stakes.Add(Make(StakeKind.Nd, c, c.StationStart, g.Start, v1, c.Turn));
            stakes.Add(Make(StakeKind.Td, c, c.StationArcStart, g.ArcStart, Rotate(v1, sgn * beta1), c.Turn));
            stakes.Add(Make(StakeKind.Tc, c, c.StationArcEnd, g.ArcEnd, Rotate(v2, -sgn * beta2), c.Turn));
            if (input.SpiralOut > 0) stakes.Add(Make(StakeKind.Nc, c, c.StationEnd, g.End, v2, c.Turn));
            stakes.Add(Make(StakeKind.P, c, c.StationArcMid, g.Mid, Unit(v1.X + v2.X, v1.Y + v2.Y), -c.Turn));
        }

        var n = pis.Count;
        stakes.Add(new Stake { Kind = StakeKind.End, Station = design.EndStation, Point = pis[n - 1], Direction = Unit(pis[n - 2], pis[n - 1]), Side = 1 });
        return stakes;
    }

    /// <summary>
    /// Stakes of an as-built design (YTCA, existing alignment): kind, number, station and side only.
    /// The host puts each one on the alignment by station.
    /// </summary>
    public static List<Stake> FromStations(RouteDesign design, double startStation, double endStation)
    {
        if (design == null) throw new ArgumentNullException(nameof(design));

        var stakes = new List<Stake> { new Stake { Kind = StakeKind.Start, Station = startStation, Side = 1 } };
        foreach (var c in design.Curves)
        {
            if (c.Elements == null) continue;
            var input = c.Input;
            if (input.SpiralIn > 0) stakes.Add(Make(StakeKind.Nd, c, c.StationStart, default, default, c.Turn));
            stakes.Add(Make(StakeKind.Td, c, c.StationArcStart, default, default, c.Turn));
            stakes.Add(Make(StakeKind.Tc, c, c.StationArcEnd, default, default, c.Turn));
            if (input.SpiralOut > 0) stakes.Add(Make(StakeKind.Nc, c, c.StationEnd, default, default, c.Turn));
            stakes.Add(Make(StakeKind.P, c, c.StationArcMid, default, default, -c.Turn));
        }

        stakes.Add(new Stake { Kind = StakeKind.End, Station = endStation, Side = 1 });
        return stakes;
    }

    /// <summary>ytc:coc: tick from −h to 10h along out, texts at 0.55 of the tick, ±0.9h along the route, rotated readable.</summary>
    public static StakeDrawing Layout(Stake stake, double textHeight)
    {
        if (stake == null) throw new ArgumentNullException(nameof(stake));
        var h = textHeight;
        var dir = stake.Direction;
        double ox = -dir.Y * stake.Side, oy = dir.X * stake.Side;   // lnorm(dir)·side
        var tick = 10 * h;
        var p = stake.Point;
        var mid = new PlanPoint(p.X + ox * 0.55 * tick, p.Y + oy * 0.55 * tick);
        return new StakeDrawing
        {
            TickStart = new PlanPoint(p.X - ox * h, p.Y - oy * h),
            TickEnd = new PlanPoint(p.X + ox * tick, p.Y + oy * tick),
            StationTextPoint = new PlanPoint(mid.X + dir.X * 0.9 * h, mid.Y + dir.Y * 0.9 * h),
            NameTextPoint = new PlanPoint(mid.X - dir.X * 0.9 * h, mid.Y - dir.Y * 0.9 * h),
            Rotation = TextAngle.Readable(Math.Atan2(oy, ox)),
            StationText = StationFormatter.Format(stake.Station, 2, withKmPrefix: false),
            NameText = stake.Name,
        };
    }

    private static Stake Make(StakeKind kind, DesignedCurve c, double station, PlanPoint point, PlanPoint direction, int side) =>
        new Stake { Kind = kind, CurveNumber = c.Number, Station = station, Point = point, Direction = direction, Side = side };

    private static PlanPoint Rotate(PlanPoint v, double angle)
    {
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        return new PlanPoint(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    private static PlanPoint Unit(PlanPoint a, PlanPoint b) => Unit(b.X - a.X, b.Y - a.Y);

    private static PlanPoint Unit(double x, double y)
    {
        var l = Math.Sqrt(x * x + y * y);
        if (l < 1e-12) throw new ArgumentException("Không xác định được hướng: hai điểm trùng nhau.");
        return new PlanPoint(x / l, y / l);
    }
}
