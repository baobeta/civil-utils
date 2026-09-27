using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Core.Curves;
using C3DTools.Core.Presets;
using C3DTools.Core.Stations;
using C3DTools.Core.Tables;

namespace C3DTools.Civil2021.Curves;

/// <summary>An alignment's curves as stakes: NĐ/TĐ/P/TC/NC and the NĐ…NC zones where the curve spacing applies.</summary>
internal sealed class AlignmentCurves
{
    public List<RouteStake> Keys { get; } = new List<RouteStake>();
    public List<StationZone> Zones { get; } = new List<StationZone>();

    /// <summary>Read like CTYTC "Chỉ cắm cọc + khung"; a failure leaves both lists empty and says why.</summary>
    public static AlignmentCurves Read(Document doc, ProjectPreset preset, Alignment alignment)
    {
        var result = new AlignmentCurves();
        try
        {
            var source = new AlignmentSource(doc, alignment.ObjectId, alignment.Handle.ToString(), alignment.Name);
            var session = new CurveDesignSession(preset);
            source.LoadInto(session);
            if (session.Design == null) return result;
            var asBuilt = AsBuiltDesign.Read(alignment, source, session.Design, new List<BoxSite>(), doc.Editor);
            foreach (var stake in RouteStakes.FromStations(asBuilt, alignment.StartingStation, alignment.EndingStation))
                if (stake.Kind != StakeKind.Start && stake.Kind != StakeKind.End)
                    result.Keys.Add(new RouteStake(stake.Station, StakeRole.CurveKey, stake.Name, stake.Kind, stake.CurveNumber));
            foreach (var c in asBuilt.Curves) result.Zones.Add(new StationZone(c.StationStart, c.StationEnd));
        }
        catch (Exception ex)
        {
            doc.Editor.WriteMessage($"\nKhông đọc được đường cong của tuyến ({ex.Message}); chỉ có cọc chi tiết.");
        }

        return result;
    }
}

/// <summary>Stakes as Civil 3D sample lines: one per stake, named by the stake, in a sample line group of the alignment.</summary>
internal static class SampleLineStakes
{
    public static List<(ObjectId id, string name)> Groups(Transaction tr, Alignment alignment) =>
        alignment.GetSampleLineGroupIds().Cast<ObjectId>().ToList()
            .Select(id => (id, name: (tr.GetObject(id, OpenMode.ForRead) as SampleLineGroup)?.Name))
            .Where(g => g.name != null)
            .ToList();

    /// <summary>The group's sample lines in station order.</summary>
    public static List<(ObjectId id, NamedStation stake)> Read(Transaction tr, ObjectId groupId)
    {
        var group = (SampleLineGroup)tr.GetObject(groupId, OpenMode.ForRead);
        return group.GetSampleLineIds().Cast<ObjectId>()
            .Select(id => (SampleLine)tr.GetObject(id, OpenMode.ForRead))
            .Select(line => (line.ObjectId, new NamedStation(line.Station, line.Name)))
            .OrderBy(x => x.Item2.Station)
            .ToList();
    }

    /// <summary>
    /// Makes the group hold exactly the planned stakes: lines of removed stakes are erased, lines at kept stations are
    /// renamed, new ones are created ±halfWidth across the alignment. Names are freed before they are reused: a line
    /// to erase first takes a temporary name, and kept lines are renamed in two passes.
    /// </summary>
    public static (int created, int renamed, int erased, int viewsLost) Write(Transaction tr, Alignment alignment, ObjectId groupId,
        IReadOnlyList<RouteStake> planned, IReadOnlyList<string> labels, double halfWidth, Action<string> warn = null)
    {
        var existing = Read(tr, groupId);
        var keep = new Dictionary<int, ObjectId>();   // planned index → line kept
        var erased = 0;
        var viewsLost = 0;
        var stamp = DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture);
        foreach (var (id, stake) in existing)
        {
            var index = FindIndex(planned, stake.Station);
            if (index >= 0 && !keep.ContainsKey(index)) keep[index] = id;
            else
            {
                var line = (SampleLine)tr.GetObject(id, OpenMode.ForWrite);
                using (var views = line.GetSectionViewIds()) viewsLost += views.Count;   // erasing the line takes its section views with it
                line.Name = "~x" + stamp + "-" + erased.ToString(CultureInfo.InvariantCulture);   // an erased line still holds its name until commit
                line.Erase();
                erased++;
            }
        }

        var renamed = keep.Count(k => ((SampleLine)tr.GetObject(k.Value, OpenMode.ForRead)).Name != labels[k.Key]);
        Rename(tr, keep.Values.ToList(), keep.Keys.Select(i => labels[i]).ToList());

        var created = 0;
        var failed = 0;
        for (var i = 0; i < planned.Count; i++)
        {
            if (keep.ContainsKey(i)) continue;
            // A station a hair past the ends (rounding) would make PointLocation throw.
            var station = Math.Max(alignment.StartingStation, Math.Min(alignment.EndingStation, planned[i].Station));
            try
            {
                var points = new Point2dCollection();   // plain managed collection in 2021: nothing to dispose
                double x = 0, y = 0;
                alignment.PointLocation(station, -halfWidth, ref x, ref y);
                points.Add(new Point2d(x, y));
                alignment.PointLocation(station, halfWidth, ref x, ref y);
                points.Add(new Point2d(x, y));
                SampleLine.Create(labels[i], groupId, points);

                created++;
            }
            catch (Exception ex)
            {
                if (failed++ < 5) warn?.Invoke($"Cọc {labels[i]} tại {NumberFormat.Fixed(planned[i].Station, 2)}: không tạo được ({ex.Message}); bỏ qua.");
            }
        }

        if (failed > 5) warn?.Invoke($"… và {failed - 5} cọc khác không tạo được.");
        return (created, renamed, erased, viewsLost);
    }

    /// <summary>Renames the lines: first to temporary unique names, then to the final ones.</summary>
    public static void Rename(Transaction tr, IReadOnlyList<ObjectId> ids, IReadOnlyList<string> names)
    {
        var lines = ids.Select(id => (SampleLine)tr.GetObject(id, OpenMode.ForWrite)).ToList();
        var stamp = DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture);
        for (var i = 0; i < lines.Count; i++)
            if (lines[i].Name != names[i]) lines[i].Name = "~" + stamp + "-" + i.ToString(CultureInfo.InvariantCulture);
        for (var i = 0; i < lines.Count; i++)
            if (lines[i].Name != names[i]) lines[i].Name = names[i];
    }

    /// <summary>A new sample line group named after the alignment when name is taken.</summary>
    public static ObjectId CreateGroup(Transaction tr, Alignment alignment, string name)
    {
        var taken = new HashSet<string>(Groups(tr, alignment).Select(g => g.name), StringComparer.OrdinalIgnoreCase);
        var final = name.Trim();
        for (var k = 2; taken.Contains(final); k++) final = name.Trim() + "-" + k.ToString(CultureInfo.InvariantCulture);
        return SampleLineGroup.Create(final, alignment.ObjectId);
    }

    public static string Summary((int created, int renamed, int erased, int viewsLost) r) =>
        $"{r.created} cọc mới, {r.renamed} cọc đổi tên, {r.erased} cọc xoá" + (r.viewsLost > 0 ? $" (kèm {r.viewsLost} trắc ngang đã vẽ của các cọc đó)" : "");

    private static int FindIndex(IReadOnlyList<RouteStake> planned, double station)
    {
        for (var i = 0; i < planned.Count; i++)
            if (Math.Abs(planned[i].Station - station) <= StakePlanner.Tolerance) return i;
        return -1;
    }
}
