using System;
using System.Collections.Generic;
using System.Linq;

namespace C3DTools.Core.Stations;

/// <summary>An existing stake as read from the drawing (a sample line): its station and name.</summary>
public readonly struct NamedStation
{
    public NamedStation(double station, string name)
    {
        Station = station;
        Name = name ?? "";
    }

    public double Station { get; }
    public string Name { get; }
}

/// <summary>CTDANHCOC: gives existing stakes their role, so StakeNamer can rename them.</summary>
public static class StakeClassifier
{
    /// <summary>Stations of NĐ/TĐ/P/TC/NC may differ from a sample line by rounding: 1 cm.</summary>
    public const double KeyTolerance = 0.01;

    /// <summary>In station order. A stake on a curve key station takes that key's kind and curve number; the rest go by station (Km, H, detail).</summary>
    public static List<RouteStake> Classify(IEnumerable<NamedStation> stakes, IEnumerable<RouteStake> curveKeys)
    {
        var keys = (curveKeys ?? Enumerable.Empty<RouteStake>()).Where(k => k != null && k.Role == StakeRole.CurveKey).ToList();
        var result = new List<RouteStake>();
        foreach (var s in (stakes ?? Enumerable.Empty<NamedStation>()).OrderBy(s => s.Station))
        {
            var key = keys.Where(k => Math.Abs(k.Station - s.Station) <= KeyTolerance)
                .OrderBy(k => Math.Abs(k.Station - s.Station))
                .FirstOrDefault();
            result.Add(key != null
                ? new RouteStake(s.Station, StakeRole.CurveKey, s.Name, key.CurveKind, key.CurveNumber)
                : new RouteStake(s.Station, StakePlanner.RoleOf(s.Station), s.Name));
        }

        return result;
    }
}
