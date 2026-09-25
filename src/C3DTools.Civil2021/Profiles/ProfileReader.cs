using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Core.Profiles;

namespace C3DTools.Civil2021.Profiles;

/// <summary>Profile.Entities → Core ProfileSegments; the profiles of an alignment for the dialogs' combos.</summary>
internal static class ProfileReader
{
    /// <summary>
    /// Tangents, symmetric/asymmetric parabolas and circular curves in the order Civil 3D lists them. Core expects
    /// grades as fractions (0.03 = 3 %); the unit Civil 3D 2021 returns is not verified at runtime, so tangent grades
    /// come from the geometry (Δelevation / Δstation) and curve GradeIn/GradeOut are checked against the tangent
    /// points (ProfileSegment.NormalizeGrade); warn is called once when they had to be divided by 100.
    /// Throws when the profile cannot be read.
    /// </summary>
    public static List<ProfileSegment> Read(Profile profile, System.Action<string> warn)
    {
        var result = new List<ProfileSegment>();
        var rescaled = false;
        foreach (ProfileEntity entity in profile.Entities)
        {
            switch (entity)
            {
                case ProfileTangent t:
                    var run = t.EndStation - t.StartStation;
                    result.Add(new ProfileSegment
                    {
                        Kind = ProfileSegmentKind.Tangent,
                        StartStation = t.StartStation,
                        EndStation = t.EndStation,
                        StartElevation = t.StartElevation,
                        EndElevation = t.EndElevation,
                        Grade = run > 1e-9 ? (t.EndElevation - t.StartElevation) / run : 0,
                    });
                    break;
                case ProfileParabolaSymmetric p:
                    result.Add(Curve(p, ProfileSegmentKind.ParabolaSymmetric, p.PVIStation, p.PVIElevation, p.GradeIn, p.GradeOut, ref rescaled));
                    break;
                case ProfileParabolaAsymmetric a:
                    var s = Curve(a, ProfileSegmentKind.ParabolaAsymmetric, a.PVIStation, a.PVIElevation, a.GradeIn, a.GradeOut, ref rescaled);
                    s.AsymmetricLength1 = a.AsymmetricLength1;
                    s.AsymmetricLength2 = a.AsymmetricLength2;
                    result.Add(s);
                    break;
                case ProfileCircular c:
                    var circular = Curve(c, ProfileSegmentKind.Circular, c.PVIStation, c.PVIElevation, c.GradeIn, c.GradeOut, ref rescaled);
                    circular.Radius = c.Radius;
                    result.Add(circular);
                    break;
            }
        }

        if (rescaled) warn?.Invoke($"Trắc dọc {profile.Name}: Civil trả về độ dốc theo %; đã quy đổi.");
        return result;
    }

    private static ProfileSegment Curve(ProfileEntity e, ProfileSegmentKind kind, double pviStation, double pviElevation,
        double gradeIn, double gradeOut, ref bool rescaled)
    {
        // TĐ and TC lie on the tangents, so the grades follow from them and the PVI.
        var before = pviStation - e.StartStation;
        var after = e.EndStation - pviStation;
        var geometricIn = before > 1e-9 ? (pviElevation - e.StartElevation) / before : 0;
        var geometricOut = after > 1e-9 ? (e.EndElevation - pviElevation) / after : 0;
        var gIn = ProfileSegment.NormalizeGrade(gradeIn, geometricIn, out var r1);
        var gOut = ProfileSegment.NormalizeGrade(gradeOut, geometricOut, out var r2);
        rescaled |= r1 || r2;
        return new ProfileSegment
        {
            Kind = kind,
            StartStation = e.StartStation,
            EndStation = e.EndStation,
            StartElevation = e.StartElevation,
            EndElevation = e.EndElevation,
            Length = e.Length,
            PviStation = pviStation,
            PviElevation = pviElevation,
            GradeIn = gIn,
            GradeOut = gOut,
        };
    }

    /// <summary>(id, name, type) of every profile of the alignment; unreadable ones are skipped.</summary>
    public static List<(ObjectId id, string name, ProfileType type)> ProfilesOf(Transaction tr, Alignment alignment)
    {
        var result = new List<(ObjectId, string, ProfileType)>();
        foreach (ObjectId id in alignment.GetProfileIds())
        {
            try
            {
                if (tr.GetObject(id, OpenMode.ForRead) is Profile p) result.Add((id, p.Name, p.ProfileType));
            }
            catch (System.Exception)
            {
                // A broken data reference: leave it out of the combo.
            }
        }

        return result;
    }
}
