using System;
using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Curves;

namespace C3DTools.Core.Profiles;

/// <summary>
/// "Dồn dịch đỉnh trắc dọc phía sau": where a station of the old alignment lies after its curves were redesigned.
/// Tangent points keep their place on the ground, so after curve k every station moves by the change of
/// (K − T1 − T2) of curves 1…k; inside an old curve the move is interpolated.
/// </summary>
public sealed class StationShift
{
    private readonly List<(double start, double end, double before, double after)> _spans = new List<(double, double, double, double)>();

    /// <summary>Old and new curves are matched by PI index; curves without elements on either side are ignored.</summary>
    public StationShift(IEnumerable<DesignedCurve> oldCurves, IEnumerable<DesignedCurve> newCurves)
    {
        var fresh = (newCurves ?? Enumerable.Empty<DesignedCurve>()).Where(c => c.Elements != null).ToDictionary(c => c.PiIndex);
        var shift = 0.0;
        foreach (var old in (oldCurves ?? Enumerable.Empty<DesignedCurve>()).Where(c => c.Elements != null).OrderBy(c => c.StationStart))
        {
            if (!fresh.TryGetValue(old.PiIndex, out var now)) continue;
            var before = shift;
            shift += Gain(now) - Gain(old);
            _spans.Add((old.StationStart, old.StationEnd, before, shift));
        }
    }

    /// <summary>Total move at the end of the route.</summary>
    public double EndShift => _spans.Count == 0 ? 0 : _spans[_spans.Count - 1].after;

    public bool IsIdentity => _spans.All(s => Math.Abs(s.before) < 1e-9 && Math.Abs(s.after) < 1e-9);

    public double Map(double station)
    {
        var shift = 0.0;
        foreach (var (start, end, before, after) in _spans)
        {
            if (station <= start) return station + before;
            if (station < end) return station + before + (after - before) * (station - start) / (end - start);
            shift = after;
        }

        return station + shift;
    }

    private static double Gain(DesignedCurve c) => c.StationEnd - c.StationStart - c.Elements.T1 - c.Elements.T2;
}
