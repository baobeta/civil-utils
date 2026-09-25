using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace C3DTools.Core.Stations;

public enum StakeOrigin { Interval, Curve, Extra }

/// <summary>One stake of the CTTOADO list: its station, name and where it came from.</summary>
public sealed class StakeStation
{
    public StakeStation(double station, string name, StakeOrigin origin)
    {
        Station = station;
        Name = name ?? "";
        Origin = origin;
    }

    public double Station { get; }
    public string Name { get; }
    public StakeOrigin Origin { get; }
}

/// <summary>
/// CTTOADO stations: interval stakes (StationPlanner: Km, H, C), curve stakes (NĐ/TĐ/P/TC/NC names kept) and
/// extra stations, merged in station order. Within 1 mm only one stake stays, the named one (curve over interval
/// over extra); coincident curve stakes share a row ("TC1/NĐ2"). Detail stakes, extras included, are then numbered
/// C1, C2, … along the route.
/// </summary>
public static class StakeStationList
{
    public const double Tolerance = 0.001;

    /// <param name="curveStakes">Named stakes; those outside start–end are dropped.</param>
    /// <param name="extras">Extra stations (user input, geometry points); those outside start–end are dropped.</param>
    public static List<StakeStation> Build(double start, double end, double interval,
        IEnumerable<StakeStation> curveStakes, IEnumerable<double> extras)
    {
        var specials = new List<SpecialStation>();
        foreach (var stake in (curveStakes ?? Enumerable.Empty<StakeStation>())
                     .Where(s => s != null && InRange(s.Station, start, end))
                     .OrderBy(s => s.Station))
        {
            var last = specials.Count - 1;
            if (last >= 0 && stake.Station - specials[last].Station <= Tolerance)
                specials[last] = new SpecialStation(specials[last].Station, specials[last].Name + "/" + stake.Name);
            else
                specials.Add(new SpecialStation(stake.Station, stake.Name));
        }

        var result = StationPlanner.Build(start, end, interval, specials, Tolerance)
            .Select(p => new StakeStation(p.Station, p.Name, p.IsSpecial ? StakeOrigin.Curve : StakeOrigin.Interval))
            .ToList();

        foreach (var station in (extras ?? Enumerable.Empty<double>()).Where(s => InRange(s, start, end)).OrderBy(s => s))
        {
            if (result.Any(s => Math.Abs(s.Station - station) <= Tolerance)) continue;
            result.Add(new StakeStation(station, "", StakeOrigin.Extra));
        }

        var detail = 0;
        return result.OrderBy(s => s.Station)
            .Select(s => IsDetail(s)
                ? new StakeStation(s.Station, "C" + (++detail).ToString(CultureInfo.InvariantCulture), s.Origin)
                : s)
            .ToList();
    }

    private static bool IsDetail(StakeStation s) =>
        s.Origin == StakeOrigin.Extra || (s.Origin == StakeOrigin.Interval && s.Name.StartsWith("C", StringComparison.Ordinal));

    private static bool InRange(double station, double start, double end) =>
        station >= start - Tolerance && station <= end + Tolerance;
}
