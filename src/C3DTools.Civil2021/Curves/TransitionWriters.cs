using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Core.Curves;
using C3DTools.Core.Profiles;
using C3DTools.Core.Tables;

namespace C3DTools.Civil2021.Curves;

/// <summary>"Tạo polyline các đoạn nối": the widened pavement edges as LWPOLYLINEs on YTC_MEP.</summary>
internal static class EdgeWriter
{
    public const string Layer = "YTC_MEP";

    public static int Write(RouteDrawing d, IEnumerable<EdgeLine> lines)
    {
        d.EnsureLayer(Layer, 4);
        var count = 0;
        foreach (var line in lines)
        {
            if (line.Points.Count < 2) continue;
            d.Add(CurveGeometryWriter.Polyline(line.Points, false), Layer, new YtcTag { Number = line.CurveNumber, Kind = YtcKind.Edge });
            count++;
        }

        return count;
    }

    /// <summary>(station, offset) → point on an existing alignment; offset positive to the left, as Core expects.</summary>
    public static Func<double, double, PlanPoint> OnAlignment(Alignment alignment) => (station, offset) =>
    {
        double x = 0, y = 0;
        // Civil 3D offsets are positive to the right of the alignment.
        alignment.PointLocation(station, -offset, ref x, ref y);
        return new PlanPoint(x, y);
    };
}

/// <summary>
/// "Siêu cao → Alignment": the critical stations of each superelevated curve written into the alignment
/// (SuperelevationCurves.AddUserDefinedCurve + CriticalStations.Add + SetSlope, verified by reflection only).
/// Runs in a nested transaction: a failure leaves the alignment as it was and only the table is written.
/// </summary>
internal static class SuperelevationWriter
{
    private static readonly SuperelevationCrossSegmentType[] LeftLanes =
        { SuperelevationCrossSegmentType.LeftInLaneCrossSlope, SuperelevationCrossSegmentType.LeftOutLaneCrossSlope };

    private static readonly SuperelevationCrossSegmentType[] RightLanes =
        { SuperelevationCrossSegmentType.RightInLaneCrossSlope, SuperelevationCrossSegmentType.RightOutLaneCrossSlope };

    public static bool Write(Transaction tr, ObjectId alignmentId, IReadOnlyList<SuperelevationPoint> points, Editor ed)
    {
        if (points.Count == 0) return true;
        using (var nested = tr.TransactionManager.StartTransaction())
        {
            try
            {
                var alignment = (Alignment)nested.GetObject(alignmentId, OpenMode.ForWrite);
                var subs = SubEntities(alignment);
                foreach (var group in points.GroupBy(p => p.CurveNumber))
                {
                    var first = group.Min(p => p.Station);
                    var last = group.Max(p => p.Station);
                    var start = subs.FirstOrDefault(s => s.EndStation > first + 1e-6) ?? subs[0];
                    var end = subs.LastOrDefault(s => s.StartStation < last - 1e-6) ?? subs[subs.Count - 1];
                    var curve = alignment.SuperelevationCurves.AddUserDefinedCurve(start, end);
                    foreach (var p in group.OrderBy(p => p.Station))
                    {
                        var region = p.IsEntry ? SuperelevationAttainmentRegionType.BeginingAttainmentRegion : SuperelevationAttainmentRegionType.EndingAttainmentRegion;
                        curve.CriticalStations.Add(p.Station, ToCivil(p.Kind), region);
                        var station = curve.CriticalStations.GetCriticalStationAt(p.Station, 0.001);
                        // Civil 3D stores cross slopes as fractions (−0.02 for −2 %).
                        foreach (var lane in LeftLanes) station.SetSlope(lane, p.LeftSlope / 100);
                        foreach (var lane in RightLanes) station.SetSlope(lane, p.RightSlope / 100);
                    }
                }

                nested.Commit();
                ed.WriteMessage($"\nĐã ghi siêu cao vào Alignment {alignment.Name}: {points.Count} điểm.");
                return true;
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\nKhông ghi được siêu cao vào Alignment ({ex.Message}); chỉ xuất bảng siêu cao.");
                return false;
            }
        }
    }

    private static SuperelevationCriticalStationType ToCivil(SuperelevationPointKind kind) => kind switch
    {
        SuperelevationPointKind.BeginNormalCrown => SuperelevationCriticalStationType.BeginNormalCrown,
        SuperelevationPointKind.LevelCrown => SuperelevationCriticalStationType.LevelCrown,
        SuperelevationPointKind.ReverseCrown => SuperelevationCriticalStationType.ReverseCrown,
        SuperelevationPointKind.BeginFullSuper => SuperelevationCriticalStationType.BeginFullSuper,
        SuperelevationPointKind.EndFullSuper => SuperelevationCriticalStationType.EndFullSuper,
        _ => SuperelevationCriticalStationType.EndNormalCrown,
    };

    private static List<AlignmentSubEntity> SubEntities(Alignment alignment)
    {
        var list = new List<AlignmentSubEntity>();
        var entities = alignment.Entities;
        for (var i = 0; i < entities.Count; i++)
        {
            var entity = entities.GetEntityByOrder(i);
            for (var j = 0; j < entity.SubEntityCount; j++) list.Add(entity[j]);
        }

        list.Sort((a, b) => a.StartStation.CompareTo(b.StartStation));
        if (list.Count == 0) throw new InvalidOperationException("Alignment không có đoạn nào.");
        return list;
    }
}

/// <summary>"Dồn dịch đỉnh trắc dọc phía sau": moves the PVIs of the alignment's layout profiles with the new stations.</summary>
internal static class ProfileShifter
{
    public static void Shift(Transaction tr, Alignment alignment, StationShift shift, Editor ed)
    {
        if (shift.IsIdentity) return;
        var moved = 0;
        foreach (ObjectId id in alignment.GetProfileIds())
        {
            using (var nested = tr.TransactionManager.StartTransaction())
            {
                try
                {
                    var profile = (Profile)nested.GetObject(id, OpenMode.ForRead);
                    if (profile.ProfileType != ProfileType.FG) continue;   // surface profiles follow the alignment by themselves
                    profile.UpgradeOpen();
                    var pvis = new List<ProfilePVI>();
                    foreach (ProfilePVI pvi in profile.PVIs) pvis.Add(pvi);
                    var targets = pvis.ToDictionary(p => p, p => Math.Min(alignment.EndingStation, shift.Map(p.Station)));
                    // Forward moves from the last PVI back, backward moves from the first on: PVIs never pass each other.
                    foreach (var pvi in pvis.Where(p => targets[p] > p.Station).OrderByDescending(p => p.Station)) pvi.Station = targets[pvi];
                    foreach (var pvi in pvis.Where(p => targets[p] < p.Station).OrderBy(p => p.Station)) pvi.Station = targets[pvi];
                    nested.Commit();
                    moved++;
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\nKhông dồn dịch được trắc dọc ({ex.Message}); trắc dọc đó giữ nguyên.");
                }
            }
        }

        if (moved > 0) ed.WriteMessage($"\nĐã dồn dịch {moved} trắc dọc thiết kế, cuối tuyến lệch {NumberFormat.Fixed(shift.EndShift, 2)} m.");
    }
}
