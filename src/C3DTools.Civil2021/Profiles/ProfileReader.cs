using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Core.Profiles;

namespace C3DTools.Civil2021.Profiles;

/// <summary>Profile.Entities → Core ProfileSegments; the profiles of an alignment for the dialogs' combos.</summary>
internal static class ProfileReader
{
    /// <summary>
    /// Tangents, symmetric/asymmetric parabolas and circular curves in the order Civil 3D lists them.
    /// Civil 3D grades are fractions (0.03 = 3 %), as Core expects. Throws when the profile cannot be read.
    /// </summary>
    public static List<ProfileSegment> Read(Profile profile)
    {
        var result = new List<ProfileSegment>();
        foreach (ProfileEntity entity in profile.Entities)
        {
            switch (entity)
            {
                case ProfileTangent t:
                    result.Add(new ProfileSegment
                    {
                        Kind = ProfileSegmentKind.Tangent,
                        StartStation = t.StartStation,
                        EndStation = t.EndStation,
                        StartElevation = t.StartElevation,
                        EndElevation = t.EndElevation,
                        Grade = t.Grade,
                    });
                    break;
                case ProfileParabolaSymmetric p:
                    result.Add(Curve(p, ProfileSegmentKind.ParabolaSymmetric, p.PVIStation, p.PVIElevation, p.GradeIn, p.GradeOut));
                    break;
                case ProfileParabolaAsymmetric a:
                    var s = Curve(a, ProfileSegmentKind.ParabolaAsymmetric, a.PVIStation, a.PVIElevation, a.GradeIn, a.GradeOut);
                    s.AsymmetricLength1 = a.AsymmetricLength1;
                    s.AsymmetricLength2 = a.AsymmetricLength2;
                    result.Add(s);
                    break;
                case ProfileCircular c:
                    var circular = Curve(c, ProfileSegmentKind.Circular, c.PVIStation, c.PVIElevation, c.GradeIn, c.GradeOut);
                    circular.Radius = c.Radius;
                    result.Add(circular);
                    break;
            }
        }

        return result;
    }

    private static ProfileSegment Curve(ProfileEntity e, ProfileSegmentKind kind, double pviStation, double pviElevation, double gradeIn, double gradeOut) =>
        new ProfileSegment
        {
            Kind = kind,
            StartStation = e.StartStation,
            EndStation = e.EndStation,
            StartElevation = e.StartElevation,
            EndElevation = e.EndElevation,
            Length = e.Length,
            PviStation = pviStation,
            PviElevation = pviElevation,
            GradeIn = gradeIn,
            GradeOut = gradeOut,
        };

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
