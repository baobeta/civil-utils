using System;
using System.Collections.Generic;
using System.Linq;

namespace C3DTools.Core.Profiles;

/// <summary>
/// One PVI (đỉnh) of a design profile with its curve placed on the stations: TĐ/TC stations and elevations,
/// the curve elevation under the PVI and the high/low point. A PVI between two tangents with no curve has
/// Elements == null (HasCurve false).
/// </summary>
public sealed class VerticalCurve
{
    private VerticalCurve() { }

    /// <summary>1-based along the profile.</summary>
    public int Number { get; private set; }

    public string Name => "Đ" + Number.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public double PviStation { get; private set; }
    public double PviElevation { get; private set; }

    /// <summary>Fraction.</summary>
    public double GradeIn { get; private set; }

    /// <summary>Fraction.</summary>
    public double GradeOut { get; private set; }

    /// <summary>Percent, |i1 − i2|·100, also without a curve.</summary>
    public double A => Math.Abs(GradeIn - GradeOut) * 100;

    public bool IsCrest => GradeIn > GradeOut;

    public VerticalCurveElements Elements { get; private set; }
    public bool HasCurve => Elements != null;

    /// <summary>TĐ (= PVI without a curve).</summary>
    public double StartStation { get; private set; }

    /// <summary>TC (= PVI without a curve).</summary>
    public double EndStation { get; private set; }

    public double StartElevation { get; private set; }
    public double EndElevation { get; private set; }

    /// <summary>Elevation of the curve at the PVI station: PVI ∓ E.</summary>
    public double CurveElevationAtPvi { get; private set; }

    public double? HighLowStation { get; private set; }
    public double? HighLowElevation { get; private set; }

    /// <summary>A curve segment (not a tangent) as the number-th PVI.</summary>
    public static VerticalCurve FromSegment(ProfileSegment segment, int number)
    {
        if (segment == null) throw new ArgumentNullException(nameof(segment));
        if (!segment.IsCurve) throw new ArgumentException("Cần một đoạn cong đứng.", nameof(segment));

        VerticalCurveElements e;
        switch (segment.Kind)
        {
            case ProfileSegmentKind.ParabolaAsymmetric:
                e = VerticalCurveElements.ComputeAsymmetric(segment.GradeIn, segment.GradeOut, segment.AsymmetricLength1, segment.AsymmetricLength2);
                break;
            case ProfileSegmentKind.Circular when segment.Radius > 0:
                e = VerticalCurveElements.Compute(segment.GradeIn, segment.GradeOut, segment.Radius, ProfileSegmentKind.Circular);
                break;
            case ProfileSegmentKind.Circular:
                // Radius unknown: the length gives the equivalent radius, as for a parabola.
                var a = Math.Abs(segment.GradeIn - segment.GradeOut);
                e = VerticalCurveElements.Compute(segment.GradeIn, segment.GradeOut, a > 0 ? segment.Length / a : 0, ProfileSegmentKind.Circular);
                break;
            default:
                e = VerticalCurveElements.Compute(segment.GradeIn, segment.GradeOut, segment.Length, segment.Kind);
                break;
        }

        var c = new VerticalCurve
        {
            Number = number,
            PviStation = segment.PviStation,
            PviElevation = segment.PviElevation,
            GradeIn = segment.GradeIn,
            GradeOut = segment.GradeOut,
            Elements = e,
            StartStation = segment.PviStation - e.T1,
            EndStation = segment.PviStation + e.T2,
            StartElevation = segment.PviElevation - segment.GradeIn * e.T1,
            EndElevation = segment.PviElevation + segment.GradeOut * e.T2,
            CurveElevationAtPvi = segment.PviElevation + (e.IsCrest ? -e.E : e.E),
        };
        if (segment.Kind == ProfileSegmentKind.Circular && segment.EndStation > segment.StartStation)
        {
            // Civil 3D's own TĐ/TC of the arc rather than the L = R·a approximation.
            c.StartStation = segment.StartStation;
            c.EndStation = segment.EndStation;
            c.StartElevation = segment.StartElevation;
            c.EndElevation = segment.EndElevation;
        }

        if (e.HighLowOffset.HasValue)
        {
            var x = e.HighLowOffset.Value;
            c.HighLowStation = c.StartStation + x;
            // On the branch from TĐ, y = y(TĐ) + i1·x + r·x²/2 with r·x = −i1 at the vertex: y(TĐ) + i1·x/2.
            // On the branch into TC (asymmetric, beyond L1), measured back u from TC: y(TC) − i2·u/2.
            // For a symmetric curve both give the same value.
            c.HighLowElevation = x <= e.T1
                ? c.StartElevation + segment.GradeIn * x / 2
                : c.EndElevation - segment.GradeOut * (c.EndStation - c.HighLowStation.Value) / 2;
        }

        return c;
    }

    /// <summary>A PVI between two tangents without a curve.</summary>
    public static VerticalCurve Corner(double station, double elevation, double gradeIn, double gradeOut, int number) =>
        new VerticalCurve
        {
            Number = number,
            PviStation = station,
            PviElevation = elevation,
            GradeIn = gradeIn,
            GradeOut = gradeOut,
            StartStation = station,
            EndStation = station,
            StartElevation = elevation,
            EndElevation = elevation,
            CurveElevationAtPvi = elevation,
        };

    /// <summary>
    /// The PVIs of a profile's entities in station order: every curve, plus a corner wherever two tangents meet
    /// with different grades (≥ 0.001 %). Numbered 1, 2, … along the profile.
    /// </summary>
    public static List<VerticalCurve> FromSegments(IEnumerable<ProfileSegment> segments)
    {
        var ordered = (segments ?? Enumerable.Empty<ProfileSegment>()).Where(s => s != null).OrderBy(s => s.StartStation).ToList();
        var result = new List<VerticalCurve>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var s = ordered[i];
            if (s.IsCurve)
            {
                result.Add(FromSegment(s, result.Count + 1));
                continue;
            }

            if (i + 1 >= ordered.Count) continue;
            var next = ordered[i + 1];
            if (next.IsCurve || Math.Abs(next.Grade - s.Grade) < 1e-5) continue;
            result.Add(Corner(s.EndStation, s.EndElevation, s.Grade, next.Grade, result.Count + 1));
        }

        return result;
    }
}
