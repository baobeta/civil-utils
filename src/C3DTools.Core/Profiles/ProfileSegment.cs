using System;

namespace C3DTools.Core.Profiles;

public enum ProfileSegmentKind { Tangent, ParabolaSymmetric, ParabolaAsymmetric, Circular }

/// <summary>
/// One entity of a design profile, as Civil 3D lists them (Profile.Entities). Grades are fractions (0.03 = 3 %).
/// Tangent: start/end station and elevation, Grade. Curves: the PVI (station, elevation), GradeIn/GradeOut and the
/// curve size: Length (parabola; AsymmetricLength1/2 for the asymmetric one) or Radius (circular).
/// </summary>
public sealed class ProfileSegment
{
    public ProfileSegmentKind Kind { get; set; }
    public double StartStation { get; set; }
    public double EndStation { get; set; }
    public double StartElevation { get; set; }
    public double EndElevation { get; set; }

    /// <summary>Tangent grade, fraction.</summary>
    public double Grade { get; set; }

    public double PviStation { get; set; }
    public double PviElevation { get; set; }

    /// <summary>Fraction.</summary>
    public double GradeIn { get; set; }

    /// <summary>Fraction.</summary>
    public double GradeOut { get; set; }

    /// <summary>Curve length along the station axis (symmetric parabola, circular when known).</summary>
    public double Length { get; set; }

    /// <summary>Asymmetric parabola: length before the PVI.</summary>
    public double AsymmetricLength1 { get; set; }

    /// <summary>Asymmetric parabola: length after the PVI.</summary>
    public double AsymmetricLength2 { get; set; }

    /// <summary>Circular curve radius.</summary>
    public double Radius { get; set; }

    public bool IsCurve => Kind != ProfileSegmentKind.Tangent;

    /// <summary>
    /// Guards the grade unit a host API reports: when reported is about 100× the grade from the geometry
    /// (ratio 50–200), it was in percent and is returned divided by 100 (rescaled = true); otherwise unchanged.
    /// </summary>
    public static double NormalizeGrade(double reported, double geometric, out bool rescaled)
    {
        rescaled = false;
        if (Math.Abs(geometric) < 1e-6) return reported;
        var ratio = reported / geometric;
        if (ratio < 50 || ratio > 200) return reported;
        rescaled = true;
        return reported / 100;
    }

    /// <summary>A tangent between two points; Grade = Δelevation / Δstation.</summary>
    public static ProfileSegment Tangent(double startStation, double startElevation, double endStation, double endElevation)
    {
        if (!(endStation > startStation)) throw new ArgumentException("Đoạn thẳng cần lý trình cuối lớn hơn lý trình đầu.");
        return new ProfileSegment
        {
            Kind = ProfileSegmentKind.Tangent,
            StartStation = startStation,
            StartElevation = startElevation,
            EndStation = endStation,
            EndElevation = endElevation,
            Grade = (endElevation - startElevation) / (endStation - startStation),
        };
    }

    /// <summary>A symmetric parabola of this length at the PVI; start/end from the PVI ± L/2.</summary>
    public static ProfileSegment Parabola(double pviStation, double pviElevation, double gradeIn, double gradeOut, double length) =>
        Curve(ProfileSegmentKind.ParabolaSymmetric, pviStation, pviElevation, gradeIn, gradeOut, length, 0, 0);

    /// <summary>An asymmetric parabola: length1 before, length2 after the PVI.</summary>
    public static ProfileSegment AsymmetricParabola(double pviStation, double pviElevation, double gradeIn, double gradeOut,
        double length1, double length2) =>
        Curve(ProfileSegmentKind.ParabolaAsymmetric, pviStation, pviElevation, gradeIn, gradeOut, length1 + length2, length1, length2);

    /// <summary>A circular curve of this radius at the PVI (L = R·|i1 − i2|).</summary>
    public static ProfileSegment Circular(double pviStation, double pviElevation, double gradeIn, double gradeOut, double radius)
    {
        var s = Curve(ProfileSegmentKind.Circular, pviStation, pviElevation, gradeIn, gradeOut, radius * Math.Abs(gradeIn - gradeOut), 0, 0);
        s.Radius = radius;
        return s;
    }

    private static ProfileSegment Curve(ProfileSegmentKind kind, double pviStation, double pviElevation, double gradeIn, double gradeOut,
        double length, double length1, double length2)
    {
        var before = kind == ProfileSegmentKind.ParabolaAsymmetric ? length1 : length / 2;
        var after = kind == ProfileSegmentKind.ParabolaAsymmetric ? length2 : length / 2;
        return new ProfileSegment
        {
            Kind = kind,
            PviStation = pviStation,
            PviElevation = pviElevation,
            GradeIn = gradeIn,
            GradeOut = gradeOut,
            Length = length,
            AsymmetricLength1 = length1,
            AsymmetricLength2 = length2,
            StartStation = pviStation - before,
            EndStation = pviStation + after,
            StartElevation = pviElevation - gradeIn * before,
            EndElevation = pviElevation + gradeOut * after,
        };
    }
}
