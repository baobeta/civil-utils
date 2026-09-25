using System;

namespace C3DTools.Core.Profiles;

/// <summary>
/// Elements of one vertical curve (đường cong đứng) from its grades and size. Grades are fractions (0.03 = 3 %).
/// A is the algebraic grade difference in percent, |i1 − i2|·100. With a = A/100 (fraction):
/// parabola R = L/a, T = L/2, E = a·L/8 (the same as the textbook A·L/800 with A in percent);
/// circular (TCVN approximation) L = R·a, T = L/2, E = a·L/8 = T²/(2R).
/// K is the curve length (Vietnamese notation, as in the horizontal curve box), so K = L.
/// </summary>
public sealed class VerticalCurveElements
{
    private VerticalCurveElements() { }

    public ProfileSegmentKind Kind { get; private set; }

    /// <summary>Fraction.</summary>
    public double GradeIn { get; private set; }

    /// <summary>Fraction.</summary>
    public double GradeOut { get; private set; }

    /// <summary>Percent, ≥ 0.</summary>
    public double A { get; private set; }

    public double L { get; private set; }

    /// <summary>Given (circular) or equivalent L/a (parabola). 0 when the grades do not change.</summary>
    public double R { get; private set; }

    /// <summary>Curve length (K = L).</summary>
    public double K => L;

    /// <summary>Tangent length before the PVI (L/2, or L1 of an asymmetric parabola).</summary>
    public double T1 { get; private set; }

    /// <summary>Tangent length after the PVI (L/2, or L2 of an asymmetric parabola).</summary>
    public double T2 { get; private set; }

    /// <summary>T1 of a symmetric curve; (T1 + T2)/2 of an asymmetric one.</summary>
    public double T => (T1 + T2) / 2;

    /// <summary>External distance: from the PVI to the curve, vertically.</summary>
    public double E { get; private set; }

    /// <summary>Lồi: the grade decreases (i1 &gt; i2).</summary>
    public bool IsCrest => GradeIn > GradeOut;

    public bool IsAsymmetric => Kind == ProfileSegmentKind.ParabolaAsymmetric;

    /// <summary>
    /// Distance from TĐ to the high (crest) or low (sag) point, when the grades change sign; null otherwise.
    /// </summary>
    public double? HighLowOffset { get; private set; }

    /// <param name="lengthOrRadius">Length for a symmetric parabola, radius for a circular curve.</param>
    public static VerticalCurveElements Compute(double gradeIn, double gradeOut, double lengthOrRadius, ProfileSegmentKind kind)
    {
        if (kind == ProfileSegmentKind.Tangent) throw new ArgumentException("Đoạn thẳng không có yếu tố cong đứng.", nameof(kind));
        if (kind == ProfileSegmentKind.ParabolaAsymmetric) return ComputeAsymmetric(gradeIn, gradeOut, lengthOrRadius / 2, lengthOrRadius / 2);
        if (!(lengthOrRadius >= 0)) throw new ArgumentOutOfRangeException(nameof(lengthOrRadius));

        var a = Math.Abs(gradeIn - gradeOut);
        double length, radius;
        if (kind == ProfileSegmentKind.Circular)
        {
            radius = lengthOrRadius;
            length = radius * a;
        }
        else
        {
            length = lengthOrRadius;
            radius = a > 0 ? length / a : 0;
        }

        var e = new VerticalCurveElements
        {
            Kind = kind,
            GradeIn = gradeIn,
            GradeOut = gradeOut,
            A = a * 100,
            L = length,
            R = radius,
            T1 = length / 2,
            T2 = length / 2,
            E = a * length / 8,
        };
        if (gradeIn * gradeOut < 0 && a > 0) e.HighLowOffset = gradeIn / (gradeIn - gradeOut) * length;
        return e;
    }

    /// <summary>Asymmetric parabola: E = a·L1·L2 / (2(L1 + L2)); R = (L1 + L2)/a is the equivalent radius.</summary>
    public static VerticalCurveElements ComputeAsymmetric(double gradeIn, double gradeOut, double length1, double length2)
    {
        if (!(length1 >= 0) || !(length2 >= 0)) throw new ArgumentOutOfRangeException(nameof(length1));
        var a = Math.Abs(gradeIn - gradeOut);
        var length = length1 + length2;
        var e = new VerticalCurveElements
        {
            Kind = ProfileSegmentKind.ParabolaAsymmetric,
            GradeIn = gradeIn,
            GradeOut = gradeOut,
            A = a * 100,
            L = length,
            R = a > 0 ? length / a : 0,
            T1 = length1,
            T2 = length2,
            E = length > 0 ? a * length1 * length2 / (2 * length) : 0,
        };
        e.HighLowOffset = AsymmetricVertex(gradeIn, gradeOut, length1, length2);
        return e;
    }

    /// <summary>
    /// Two parabolas meeting under the PVI with a common grade. Branch 1 (TĐ → PVI): grade i1 + r1·x,
    /// r1 = (i2 − i1)·L2/(L·L1). Branch 2 (PVI → TC), u measured back from TC: grade i2 − r2·u, r2 = (i2 − i1)·L1/(L·L2).
    /// The vertex is where the grade is 0 on its own branch; distance from TĐ, or null.
    /// </summary>
    private static double? AsymmetricVertex(double g1, double g2, double l1, double l2)
    {
        var l = l1 + l2;
        if (!(g1 * g2 < 0) || !(l1 > 0) || !(l2 > 0)) return null;
        var r1 = (g2 - g1) * l2 / (l * l1);
        var x1 = -g1 / r1;
        if (x1 >= 0 && x1 <= l1) return x1;
        var r2 = (g2 - g1) * l1 / (l * l2);
        var u2 = g2 / r2;
        if (u2 >= 0 && u2 <= l2) return l - u2;
        return null;
    }
}
