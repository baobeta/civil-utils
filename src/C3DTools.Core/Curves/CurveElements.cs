using System;

namespace C3DTools.Core.Curves;

public sealed class CurveElements
{
    public double T1 { get; set; }
    public double T2 { get; set; }
    public double P { get; set; }

    /// <summary>Total curve length including spirals.</summary>
    public double K { get; set; }

    /// <summary>Circular arc length only (what Civil 3D calls Curve Length).</summary>
    public double K0 { get; set; }

    public double Shift1 { get; set; }
    public double Shift2 { get; set; }
    public double TangentOffset1 { get; set; }
    public double TangentOffset2 { get; set; }
}

/// <summary>TCVN curve elements for a clothoid–arc–clothoid curve (L = 0 means no spiral).</summary>
public static class CurveElementsCalculator
{
    public static CurveElements Compute(double radius, double deltaRadians, double spiralIn, double spiralOut)
    {
        if (!(radius > 0)) throw new ArgumentOutOfRangeException(nameof(radius));
        if (!(deltaRadians > 0 && deltaRadians < Math.PI)) throw new ArgumentOutOfRangeException(nameof(deltaRadians));
        if (spiralIn < 0) throw new ArgumentOutOfRangeException(nameof(spiralIn));
        if (spiralOut < 0) throw new ArgumentOutOfRangeException(nameof(spiralOut));

        var circularLength = radius * deltaRadians - (spiralIn + spiralOut) / 2;
        if (circularLength < 0)
            throw new ArgumentException("Chiều dài đường cong chuyển tiếp quá lớn so với góc chuyển hướng.");

        Shift(spiralIn, radius, out var p1, out var m1);
        Shift(spiralOut, radius, out var p2, out var m2);
        var d1 = radius + p1;
        var d2 = radius + p2;
        var sin = Math.Sin(deltaRadians);
        var cos = Math.Cos(deltaRadians);

        // PI at the origin, tangent 1 along +x; the shifted centre sits d1 from tangent 1 and d2 from tangent 2.
        var centreX = (d1 * cos - d2) / sin;

        return new CurveElements
        {
            T1 = m1 + (d2 - d1 * cos) / sin,
            T2 = m2 + (d1 - d2 * cos) / sin,
            P = Math.Sqrt(centreX * centreX + d1 * d1) - radius,
            K = radius * deltaRadians + (spiralIn + spiralOut) / 2,
            K0 = circularLength,
            Shift1 = p1,
            Shift2 = p2,
            TangentOffset1 = m1,
            TangentOffset2 = m2,
        };
    }

    private static void Shift(double length, double radius, out double p, out double m)
    {
        if (length == 0)
        {
            p = 0;
            m = 0;
            return;
        }

        var t = length / (2 * radius);
        var x = length * (1 - t * t / 10 + Math.Pow(t, 4) / 216);
        var y = length * (t / 3 - Math.Pow(t, 3) / 42 + Math.Pow(t, 5) / 1320);
        p = y - radius * (1 - Math.Cos(t));
        m = x - radius * Math.Sin(t);
    }
}
