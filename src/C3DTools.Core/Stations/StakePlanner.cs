using System;
using System.Collections.Generic;
using System.Linq;

namespace C3DTools.Core.Stations;

/// <summary>A station range where the curve spacing applies (NĐ … NC of one curve).</summary>
public readonly struct StationZone
{
    public StationZone(double from, double to)
    {
        From = Math.Min(from, to);
        To = Math.Max(from, to);
    }

    public double From { get; }
    public double To { get; }
    public bool Contains(double station) => station > From + StakePlanner.Tolerance && station < To - StakePlanner.Tolerance;
}

/// <summary>"Phát sinh cọc": where the stakes of a route go. Names come from StakeNamer.</summary>
public static class StakePlanner
{
    public const double Tolerance = 0.001;
    private const int MaxStakes = 5000;

    /// <summary>
    /// "Phát sinh": stakes on from…to. Detail stakes at round multiples of straightSpacing on tangents and of
    /// curveSpacing inside curve zones; every H and Km; every curve key stake; the two ends. Within 1 mm only the
    /// most important stake stays (curve key &gt; Km &gt; H &gt; detail). Names are empty.
    /// </summary>
    public static List<RouteStake> Generate(double from, double to, double straightSpacing, double curveSpacing,
        IEnumerable<StationZone> curveZones, IEnumerable<RouteStake> curveKeys)
    {
        if (!(to > from)) throw new ArgumentException("Lý trình cuối phải lớn hơn lý trình đầu.", nameof(to));
        if (!(straightSpacing > 0)) throw new ArgumentOutOfRangeException(nameof(straightSpacing), "Khoảng cách trong đoạn thẳng phải lớn hơn 0.");
        if (!(curveSpacing > 0)) throw new ArgumentOutOfRangeException(nameof(curveSpacing), "Khoảng cách trong đoạn cong phải lớn hơn 0.");
        var zones = (curveZones ?? Enumerable.Empty<StationZone>()).ToList();
        var estimate = (to - from) / Math.Min(straightSpacing, curveSpacing);
        if (estimate > MaxStakes) throw new ArgumentException($"Quá nhiều cọc (khoảng {estimate:0}); hãy tăng khoảng cách.", nameof(straightSpacing));

        var candidates = new List<RouteStake> { new RouteStake(from, RoleOf(from)), new RouteStake(to, RoleOf(to)) };
        foreach (var s in Multiples(from, to, 1000)) candidates.Add(new RouteStake(s, StakeRole.Km));
        foreach (var s in Multiples(from, to, 100)) candidates.Add(new RouteStake(s, RoleOf(s)));
        foreach (var s in Multiples(from, to, straightSpacing).Where(s => !zones.Any(z => z.Contains(s))))
            candidates.Add(new RouteStake(s, RoleOf(s)));
        foreach (var z in zones)
            foreach (var s in Multiples(Math.Max(from, z.From), Math.Min(to, z.To), curveSpacing))
                candidates.Add(new RouteStake(s, RoleOf(s)));
        candidates.AddRange((curveKeys ?? Enumerable.Empty<RouteStake>())
            .Where(k => k != null && k.Station >= from - Tolerance && k.Station <= to + Tolerance));

        return Dedupe(candidates);
    }

    /// <summary>"Phát sinh" over part of a route: existing stakes outside from…to stay, those inside are replaced.</summary>
    public static List<RouteStake> Replace(IEnumerable<RouteStake> existing, IEnumerable<RouteStake> generated, double from, double to)
    {
        var kept = (existing ?? Enumerable.Empty<RouteStake>())
            .Where(s => s.Station < from - Tolerance || s.Station > to + Tolerance);
        return Dedupe(kept.Concat(generated ?? Enumerable.Empty<RouteStake>()));
    }

    /// <summary>
    /// "Chèn": adds stakes at the stations; existing stakes keep their names. A new stake is named by its station, or
    /// with subStakeStyle ("Kiểu cọc phụ") after the stake before it plus a letter: C5a, C5b. Stations already staked are skipped.
    /// </summary>
    public static List<RouteStake> Insert(IEnumerable<RouteStake> existing, IEnumerable<double> stations, bool subStakeStyle,
        int stationDecimals, out int skipped)
    {
        var result = (existing ?? Enumerable.Empty<RouteStake>()).OrderBy(s => s.Station).ToList();
        skipped = 0;
        foreach (var station in (stations ?? Enumerable.Empty<double>()).OrderBy(s => s))
        {
            if (result.Any(s => Math.Abs(s.Station - station) <= Tolerance))
            {
                skipped++;
                continue;
            }

            var index = result.FindIndex(s => s.Station > station);
            if (index < 0) index = result.Count;
            var name = StakeNamer.StationName(station, stationDecimals);
            if (subStakeStyle && index > 0) name = NextSubName(result, index);
            result.Insert(index, new RouteStake(station, StakeRole.Detail, name));
        }

        return result;
    }

    /// <summary>Km for a whole kilometre, Hundred for a whole 100 m, otherwise Detail.</summary>
    public static StakeRole RoleOf(double station)
    {
        if (IsMultiple(station, 1000)) return StakeRole.Km;
        return IsMultiple(station, 100) ? StakeRole.Hundred : StakeRole.Detail;
    }

    /// <summary>After C5 comes C5a; after C5a comes C5b (a name ending in a lower-case letter is a sub stake).</summary>
    private static string NextSubName(List<RouteStake> sorted, int index)
    {
        var before = sorted[index - 1];
        var name = before.Name.Length > 0 ? before.Name : StakeNamer.StationName(before.Station, 2);
        var last = name[name.Length - 1];
        return name.Length > 1 && last >= 'a' && last < 'z' ? name.Substring(0, name.Length - 1) + (char)(last + 1) : name + "a";
    }

    private static List<RouteStake> Dedupe(IEnumerable<RouteStake> stakes)
    {
        var result = new List<RouteStake>();
        foreach (var s in stakes.OrderBy(s => s.Station).ThenByDescending(Rank))
        {
            var last = result.Count - 1;
            if (last >= 0 && s.Station - result[last].Station <= Tolerance)
            {
                if (Rank(s) > Rank(result[last])) result[last] = s;
                continue;
            }

            result.Add(s);
        }

        return result;
    }

    private static int Rank(RouteStake s) => s.Role switch
    {
        StakeRole.CurveKey => 3,
        StakeRole.Km => 2,
        StakeRole.Hundred => 1,
        _ => s.Name.Length > 0 ? 0 : -1,
    };

    private static IEnumerable<double> Multiples(double from, double to, double step)
    {
        // Multiply instead of accumulating, as StationPlanner does, so long routes stay exact.
        for (var k = (long)Math.Ceiling((from - Tolerance) / step); k * step <= to + Tolerance; k++)
            yield return k * step;
    }

    private static bool IsMultiple(double station, double step) =>
        Math.Abs(station - Math.Round(station / step, MidpointRounding.AwayFromZero) * step) <= Tolerance;
}
