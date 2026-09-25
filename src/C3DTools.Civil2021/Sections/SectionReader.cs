using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Core.Sections;
using C3DTools.Core.Stations;
using C3DTools.Core.Tables;
using Section = Autodesk.Civil.DatabaseServices.Section;

namespace C3DTools.Civil2021.Sections;

/// <summary>One section of a sample line: its source name (surface / corridor) and kind.</summary>
internal sealed class SectionLine
{
    public ObjectId Id { get; set; }
    public string SourceName { get; set; }
    public SectionSourceType SourceType { get; set; }

    /// <summary>TIN/grid surface sections: the natural ground candidates.</summary>
    public bool IsSurface => SourceType == SectionSourceType.TinSurface || SourceType == SectionSourceType.GridSurface;

    /// <summary>Corridor surfaces first, then corridors, then everything else.</summary>
    public int DesignRank => SourceType == SectionSourceType.CorridorSurface ? 0 : SourceType == SectionSourceType.Corridor ? 1 : 2;
}

/// <summary>A picked section view with its sample line and that line's sections.</summary>
internal sealed class SectionViewSource
{
    public ObjectId ViewId { get; set; }
    public string Handle { get; set; }
    public string Name { get; set; }
    public ObjectId SampleLineId { get; set; }
    public string SampleLineName { get; set; }
    public double Station { get; set; }

    /// <summary>The sample line group; Null when unknown.</summary>
    public ObjectId GroupId { get; set; }

    public List<SectionLine> Sections { get; } = new List<SectionLine>();
}

/// <summary>
/// Reads section views through the verified 2021 members: SectionView.ParentEntityId (taken as the sample line; when
/// it is not one, every alignment's sample lines are searched with SampleLine.GetSectionViewIds), SampleLine.GetSectionIds,
/// Section.SourceName / SourceType / SectionPoints. SectionPoint.Location is read as X = offset, Y = elevation and
/// checked against Section.LeftOffset/RightOffset (Windows run still to confirm).
/// </summary>
internal static class SectionReader
{
    public static SectionViewSource Read(Transaction tr, SectionView view, Action<string> say)
    {
        var source = new SectionViewSource { ViewId = view.ObjectId, Handle = view.Handle.ToString(), Name = view.Name };
        var line = SampleLineOf(tr, view);
        if (line == null)
        {
            say?.Invoke($"Không tìm được cọc mặt cắt (sample line) của trắc ngang {view.Name}; bỏ qua.");
            return null;
        }

        source.SampleLineId = line.ObjectId;
        source.SampleLineName = line.Name;
        source.Station = line.Station;
        source.GroupId = line.GroupId;
        try
        {
            foreach (ObjectId id in line.GetSectionIds())
            {
                if (!(tr.GetObject(id, OpenMode.ForRead) is Section section)) continue;
                source.Sections.Add(new SectionLine { Id = id, SourceName = section.SourceName, SourceType = section.SourceType });
            }
        }
        catch (Exception ex)
        {
            say?.Invoke($"Không đọc được các mặt cắt của cọc {line.Name}: {ex.Message}");
        }

        return source;
    }

    /// <summary>
    /// The section's vertices as (offset, elevation). Null (with a message) when they cannot be read or do not lie in
    /// the section's offset range, which would mean Location is not (offset, elevation).
    /// </summary>
    public static SectionProfile Points(Transaction tr, ObjectId sectionId, Action<string> say)
    {
        try
        {
            var section = (Section)tr.GetObject(sectionId, OpenMode.ForRead);
            var points = new List<(double, double)>();
            foreach (SectionPoint p in section.SectionPoints) points.Add((p.Location.X, p.Location.Y));
            var profile = new SectionProfile(points);
            if (!profile.IsUsable)
            {
                say?.Invoke($"Mặt cắt {section.SourceName} tại lý trình {StationFormatter.Format(section.Station, 2)} không có đủ điểm.");
                return null;
            }

            double left = section.LeftOffset, right = section.RightOffset;
            var lo = Math.Min(left, right) - 0.01 - 0.01 * Math.Abs(right - left);
            var hi = Math.Max(left, right) + 0.01 + 0.01 * Math.Abs(right - left);
            if (right - left > 1e-6 && (profile.MinOffset < lo || profile.MaxOffset > hi))
            {
                say?.Invoke($"Điểm của mặt cắt {section.SourceName} nằm ngoài khoảng offset {NumberFormat.Fixed(left, 2)}…{NumberFormat.Fixed(right, 2)}; không dùng được (cần kiểm tra SectionPoint.Location trên Civil 3D 2021).");
                return null;
            }

            return profile;
        }
        catch (Exception ex)
        {
            say?.Invoke($"Không đọc được điểm của mặt cắt: {ex.Message}");
            return null;
        }
    }

    /// <summary>Every section view of the view's section view group (the view itself when the group cannot be found).</summary>
    public static List<ObjectId> GroupViews(Transaction tr, SectionViewSource source, Action<string> say)
    {
        try
        {
            if (!source.GroupId.IsNull && tr.GetObject(source.GroupId, OpenMode.ForRead) is SampleLineGroup group)
            {
                foreach (SectionViewGroup viewGroup in group.SectionViewGroups)
                {
                    var ids = viewGroup.GetSectionViewIds().Cast<ObjectId>().ToList();
                    if (ids.Contains(source.ViewId)) return ids;
                }
            }
        }
        catch (Exception ex)
        {
            say?.Invoke($"Không đọc được nhóm trắc ngang (section view group): {ex.Message}; chỉ dùng trắc ngang đã chọn.");
        }

        return new List<ObjectId> { source.ViewId };
    }

    private static SampleLine SampleLineOf(Transaction tr, SectionView view)
    {
        try
        {
            if (!view.ParentEntityId.IsNull && tr.GetObject(view.ParentEntityId, OpenMode.ForRead) is SampleLine direct) return direct;
        }
        catch (Exception)
        {
            // Fall back to the search below.
        }

        try
        {
            foreach (ObjectId alignmentId in CivilApplication.ActiveDocument.GetAlignmentIds())
            {
                if (!(tr.GetObject(alignmentId, OpenMode.ForRead) is Alignment alignment)) continue;
                foreach (ObjectId groupId in alignment.GetSampleLineGroupIds())
                {
                    if (!(tr.GetObject(groupId, OpenMode.ForRead) is SampleLineGroup group)) continue;
                    foreach (ObjectId lineId in group.GetSampleLineIds())
                    {
                        if (tr.GetObject(lineId, OpenMode.ForRead) is SampleLine line
                            && line.GetSectionViewIds().Cast<ObjectId>().Contains(view.ObjectId)) return line;
                    }
                }
            }
        }
        catch (Exception)
        {
            // No sample line found: the caller explains.
        }

        return null;
    }
}
