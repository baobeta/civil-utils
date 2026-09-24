using System;
using System.Collections.Generic;
using System.Linq;

namespace C3DTools.Core.Curves;

/// <summary>YTCA's widening method: nominal width = smallest offset along the route; W = offset at the curve midpoint − nominal.</summary>
public static class WideningEstimator
{
    private const double Negligible = 0.005;

    public static double Nominal(IReadOnlyList<double> offsets)
    {
        if (offsets == null || offsets.Count == 0) throw new ArgumentException("Cần ít nhất một giá trị offset.", nameof(offsets));
        return offsets.Min(o => Math.Abs(o));
    }

    public static double Widening(double offsetAtMid, double nominal)
    {
        var w = Math.Abs(offsetAtMid) - nominal;
        return w < Negligible ? 0 : w;
    }
}
