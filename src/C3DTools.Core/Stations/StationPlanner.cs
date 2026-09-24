using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace C3DTools.Core.Stations;

public sealed class SpecialStation
{
    public SpecialStation(double station, string name)
    {
        Station = station;
        Name = name;
    }

    public double Station { get; }
    public string Name { get; }
}

public sealed class PlannedStation
{
    public PlannedStation(double station, string name, bool isSpecial)
    {
        Station = station;
        Name = name;
        IsSpecial = isSpecial;
    }

    public double Station { get; }
    public string Name { get; }
    public bool IsSpecial { get; }
}

/// <summary>Plans sample line stations for C-06.</summary>
public static class StationPlanner
{
    public static IReadOnlyList<PlannedStation> Build(
        double start, double end, double interval, IEnumerable<SpecialStation> specials, double tolerance = 0.01)
    {
        if (!(end > start)) throw new ArgumentException("Lý trình cuối phải lớn hơn lý trình đầu.", nameof(end));
        if (!(interval > 0)) throw new ArgumentOutOfRangeException(nameof(interval), "Khoảng cách cọc phải lớn hơn 0.");

        var specialList = (specials ?? Enumerable.Empty<SpecialStation>()).ToList();
        foreach (var s in specialList)
        {
            if (s.Station < start - tolerance || s.Station > end + tolerance)
                throw new ArgumentOutOfRangeException(nameof(specials), $"Cọc {s.Name} nằm ngoài phạm vi tuyến.");
        }

        // Multiply instead of accumulating so 0.1-step errors do not build up over long alignments.
        var regular = new List<double> { start };
        for (var k = (long)Math.Ceiling((start + tolerance) / interval); k * interval < end - tolerance; k++)
            regular.Add(k * interval);
        regular.Add(end);

        var result = new List<PlannedStation>();
        var detailIndex = 0;
        foreach (var station in regular)
        {
            if (specialList.Any(s => Math.Abs(s.Station - station) <= tolerance)) continue;
            result.Add(new PlannedStation(station, RegularName(station, tolerance, ref detailIndex), false));
        }

        result.AddRange(specialList.Select(s => new PlannedStation(s.Station, s.Name, true)));
        return result.OrderBy(p => p.Station).ToList();
    }

    // Placeholder convention until discovery confirms how projects name stations (PRD §12).
    private static string RegularName(double station, double tolerance, ref int detailIndex)
    {
        var km = Math.Round(station / 1000.0);
        if (Math.Abs(station - km * 1000.0) <= tolerance)
            return "Km" + ((long)km).ToString(CultureInfo.InvariantCulture);

        var hundreds = Math.Round(station / 100.0);
        if (Math.Abs(station - hundreds * 100.0) <= tolerance)
            return "H" + ((long)hundreds % 10).ToString(CultureInfo.InvariantCulture);

        detailIndex++;
        return "C" + detailIndex.ToString(CultureInfo.InvariantCulture);
    }
}
