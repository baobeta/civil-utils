using System;
using System.Collections.Generic;
using System.Linq;

namespace C3DTools.Core.Curves;

/// <summary>
/// "Rmax…" / "Lmax…": the largest radius, or the largest (equal) spiral length, whose tangents still fit between the
/// neighbouring curves. Results are rounded down to the centimetre so they always fit.
/// </summary>
public static class CurveLimits
{
    private const int Iterations = 80;
    private const double RadiusCap = 1e7;

    /// <summary>Tangent length free for T1 (before the PI) and T2 (after it), after the neighbours' T2 and T1.</summary>
    public static void Available(IReadOnlyList<PlanPoint> pis, RouteDesign design, DesignedCurve curve, out double before, out double after)
    {
        if (pis == null) throw new ArgumentNullException(nameof(pis));
        if (design == null) throw new ArgumentNullException(nameof(design));
        if (curve == null) throw new ArgumentNullException(nameof(curve));
        var i = curve.PiIndex;
        var previous = design.Curves.FirstOrDefault(c => c.PiIndex == i - 1 && c.Elements != null);
        var next = design.Curves.FirstOrDefault(c => c.PiIndex == i + 1 && c.Elements != null);
        before = Distance(pis[i - 1], pis[i]) - (previous?.Elements.T2 ?? 0);
        after = Distance(pis[i], pis[i + 1]) - (next?.Elements.T1 ?? 0);
    }

    /// <summary>Largest R with T1 ≤ before and T2 ≤ after for these spiral lengths; null when no radius fits.</summary>
    public static double? MaxRadius(double deltaRadians, double spiralIn, double spiralOut, double before, double after)
    {
        if (!(deltaRadians > 0 && deltaRadians < Math.PI) || before <= 0 || after <= 0 || spiralIn < 0 || spiralOut < 0) return null;
        bool Fits(double r) => TryElements(r, deltaRadians, spiralIn, spiralOut, out var e) && e.T1 <= before && e.T2 <= after;

        var low = Math.Max((spiralIn + spiralOut) / (2 * deltaRadians) * (1 + 1e-9), 1e-3);
        if (!Fits(low)) return null;
        var high = Math.Max(low * 2, 1);
        while (Fits(high))
        {
            if (high >= RadiusCap) return RadiusCap;
            high *= 2;
        }

        for (var k = 0; k < Iterations; k++)
        {
            var mid = (low + high) / 2;
            if (Fits(mid)) low = mid;
            else high = mid;
        }

        return Math.Floor(low * 100) / 100;
    }

    /// <summary>Largest L (= L1 = L2) with T1 ≤ before and T2 ≤ after at radius R; null when even L = 0 does not fit.</summary>
    public static double? MaxSpiral(double radius, double deltaRadians, double before, double after)
    {
        if (!(radius > 0) || !(deltaRadians > 0 && deltaRadians < Math.PI) || before <= 0 || after <= 0) return null;
        bool Fits(double l) => TryElements(radius, deltaRadians, l, l, out var e) && e.T1 <= before && e.T2 <= after;

        if (!Fits(0)) return null;
        double low = 0, high = radius * deltaRadians;   // L1 + L2 = 2RΔ leaves no circular arc
        if (Fits(high)) return Math.Floor(high * 100) / 100;
        for (var k = 0; k < Iterations; k++)
        {
            var mid = (low + high) / 2;
            if (Fits(mid)) low = mid;
            else high = mid;
        }

        return Math.Floor(low * 100) / 100;
    }

    /// <summary>Clothoid parameter A = √(R·L); 0 without a spiral.</summary>
    public static double SpiralParameter(double radius, double length) => radius > 0 && length > 0 ? Math.Sqrt(radius * length) : 0;

    /// <summary>L = A² / R.</summary>
    public static double SpiralLength(double radius, double parameter) => radius > 0 && parameter > 0 ? parameter * parameter / radius : 0;

    private static bool TryElements(double r, double delta, double l1, double l2, out CurveElements e)
    {
        e = null;
        if ((l1 + l2) / (2 * r) > delta) return false;
        try
        {
            e = CurveElementsCalculator.Compute(r, delta, l1, l2);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static double Distance(PlanPoint a, PlanPoint b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
