using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using C3DTools.Core.Stations;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Curves;

/// <summary>Critical points of a superelevation runoff; the names match Civil 3D's SuperelevationCriticalStationType.</summary>
public enum SuperelevationPointKind { BeginNormalCrown, LevelCrown, ReverseCrown, BeginFullSuper, EndFullSuper, EndNormalCrown }

/// <summary>One critical station: cross slopes in %, negative = falling away from the centreline.</summary>
public sealed class SuperelevationPoint
{
    public SuperelevationPoint(int curveNumber, SuperelevationPointKind kind, double station, double leftSlope, double rightSlope)
    {
        CurveNumber = curveNumber;
        Kind = kind;
        Station = station;
        LeftSlope = leftSlope;
        RightSlope = rightSlope;
    }

    public int CurveNumber { get; }
    public SuperelevationPointKind Kind { get; }
    public double Station { get; }
    public double LeftSlope { get; }
    public double RightSlope { get; }
    public bool IsEntry => Kind <= SuperelevationPointKind.BeginFullSuper;
}

/// <summary>Begin and end station of a runoff (the same range carries the widening transition).</summary>
public readonly struct StationRange
{
    public StationRange(double begin, double end)
    {
        Begin = begin;
        End = end;
    }

    public double Begin { get; }
    public double End { get; }
    public double Length => End - Begin;
}

/// <summary>
/// Superelevation and widening transitions of a curve (TCVN 4054 §5.5–5.6): the outside lane turns about the
/// centreline from −in to +in, then the whole section to isc; widening grows linearly over the same length.
/// </summary>
public static class SuperelevationPlanner
{
    /// <summary>Entry runoff: the spiral with "Bố trí theo chuyển tiếp", otherwise RunoffIn starting OffsetIn before TĐ.</summary>
    public static StationRange EntryRange(DesignedCurve c)
    {
        var i = c.Input;
        if (i.RunoffOnSpiral && i.SpiralIn > 0) return new StationRange(c.StationStart, c.StationArcStart);
        var begin = c.StationArcStart - i.OffsetIn;
        return new StationRange(begin, begin + i.RunoffIn);
    }

    /// <summary>Exit runoff: the spiral with "Bố trí theo chuyển tiếp", otherwise RunoffOut ending OffsetOut after TC.</summary>
    public static StationRange ExitRange(DesignedCurve c)
    {
        var i = c.Input;
        if (i.RunoffOnSpiral && i.SpiralOut > 0) return new StationRange(c.StationArcEnd, c.StationEnd);
        var end = c.StationArcEnd + i.OffsetOut;
        return new StationRange(end - i.RunoffOut, end);
    }

    /// <summary>The six critical stations of each runoff; empty when the curve is not superelevated or has no elements.</summary>
    public static List<SuperelevationPoint> Plan(DesignedCurve c, double crossSlope)
    {
        var points = new List<SuperelevationPoint>();
        if (c == null || c.Elements == null || !c.Input.Superelevated) return points;
        var normal = Math.Abs(crossSlope);
        var full = Math.Max(Math.Abs(c.Input.SuperRate), normal);
        var level = normal + full > 0 ? normal / (normal + full) : 0;
        var reverse = 2 * level;

        // Right turn (Turn = 1): the centre is on the right, the left side is the outside.
        (double left, double right) Slopes(double outside, double inside) => c.Turn == 1 ? (outside, inside) : (inside, outside);
        void Add(SuperelevationPointKind kind, double station, double outside, double inside)
        {
            var (l, r) = Slopes(outside, inside);
            points.Add(new SuperelevationPoint(c.Number, kind, station, l, r));
        }

        var entry = EntryRange(c);
        Add(SuperelevationPointKind.BeginNormalCrown, entry.Begin, -normal, -normal);
        Add(SuperelevationPointKind.LevelCrown, entry.Begin + entry.Length * level, 0, -normal);
        Add(SuperelevationPointKind.ReverseCrown, entry.Begin + entry.Length * reverse, normal, -normal);
        Add(SuperelevationPointKind.BeginFullSuper, entry.End, full, -full);

        var exit = ExitRange(c);
        Add(SuperelevationPointKind.EndFullSuper, exit.Begin, full, -full);
        Add(SuperelevationPointKind.ReverseCrown, exit.End - exit.Length * reverse, normal, -normal);
        Add(SuperelevationPointKind.LevelCrown, exit.End - exit.Length * level, 0, -normal);
        Add(SuperelevationPointKind.EndNormalCrown, exit.End, -normal, -normal);
        return points;
    }

    /// <summary>0 before the entry runoff and after the exit runoff, 1 between them, linear on the runoffs.</summary>
    public static double WideningFactor(DesignedCurve c, double station)
    {
        var entry = EntryRange(c);
        var exit = ExitRange(c);
        if (station <= entry.Begin || station >= exit.End) return 0;
        if (station < entry.End) return entry.Length > 0 ? (station - entry.Begin) / entry.Length : 1;
        if (station > exit.Begin) return exit.Length > 0 ? (exit.End - station) / exit.Length : 1;
        return 1;
    }

    /// <summary>
    /// Adds warnings to the curves' Issues: a runoff missing, reaching past the route ends, overlapping the curve's other
    /// runoff or the next curve's. Only curves with superelevation or widening are checked.
    /// </summary>
    public static void Check(IReadOnlyList<DesignedCurve> curves, double startStation, double endStation)
    {
        var active = curves.Where(c => c.Elements != null && (c.Input.Superelevated || c.Input.Wb > 0 || c.Input.Wl > 0)).ToList();
        for (var k = 0; k < active.Count; k++)
        {
            var c = active[k];
            var name = "Đ" + c.Number.ToString(CultureInfo.InvariantCulture);
            var entry = EntryRange(c);
            var exit = ExitRange(c);
            void Warn(CurveIssueCode code, string message) => c.Issues.Add(new CurveIssue(code, message));
            if (entry.Length <= 0 || exit.Length <= 0)
                Warn(CurveIssueCode.TransitionOverlap, $"{name}: chưa có chiều dài nối (siêu cao / mở rộng).");
            if (entry.End > exit.Begin + 1e-6)
                Warn(CurveIssueCode.TransitionOverlap, $"{name}: đoạn nối đầu và nối cuối chồng nhau {NumberFormat.Fixed(entry.End - exit.Begin, 2)} m.");
            if (entry.Begin < startStation - 1e-6 || exit.End > endStation + 1e-6)
                Warn(CurveIssueCode.TransitionOutsideRoute, $"{name}: đoạn nối vượt ra ngoài đầu/cuối tuyến.");
            if (k + 1 < active.Count)
            {
                var nextEntry = EntryRange(active[k + 1]);
                if (exit.End > nextEntry.Begin + 1e-6)
                    Warn(CurveIssueCode.TransitionOverlap,
                        $"{name}: đoạn nối cuối chồng lên đoạn nối đầu của Đ{active[k + 1].Number} {NumberFormat.Fixed(exit.End - nextEntry.Begin, 2)} m.");
            }
        }
    }

    /// <summary>Bảng siêu cao: one row per critical station.</summary>
    public static TableData Table(IEnumerable<SuperelevationPoint> points, int stationDecimals)
    {
        var table = new TableData("Đỉnh", "Điểm", "Lý trình", "Dốc ngang trái (%)", "Dốc ngang phải (%)");
        foreach (var p in points.OrderBy(p => p.Station))
            table.AddRow("Đ" + p.CurveNumber.ToString(CultureInfo.InvariantCulture), KindText(p.Kind),
                StationFormatter.Format(p.Station, Math.Max(0, Math.Min(6, stationDecimals))),
                NumberFormat.Fixed(p.LeftSlope, 2), NumberFormat.Fixed(p.RightSlope, 2));
        return table;
    }

    public static string KindText(SuperelevationPointKind kind) => kind switch
    {
        SuperelevationPointKind.BeginNormalCrown => "Đầu đoạn nối",
        SuperelevationPointKind.LevelCrown => "Mui bằng",
        SuperelevationPointKind.ReverseCrown => "Mui ngược",
        SuperelevationPointKind.BeginFullSuper => "Bắt đầu siêu cao",
        SuperelevationPointKind.EndFullSuper => "Hết siêu cao",
        _ => "Cuối đoạn nối",
    };
}
