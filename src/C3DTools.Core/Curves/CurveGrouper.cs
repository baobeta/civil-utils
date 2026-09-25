using System;
using System.Collections.Generic;
using C3DTools.Core.Stations;

namespace C3DTools.Core.Curves;

public sealed class CurveGroupingResult
{
    public CurveGroupingResult(List<CurveGroup> groups, List<string> warnings)
    {
        Groups = groups;
        Warnings = warnings;
    }

    public List<CurveGroup> Groups { get; }
    public List<string> Warnings { get; }
}

public static class CurveGrouper
{
    // Civil 3D stores radii as doubles; a spiral's end radius can differ from the arc's in the last digits.
    private const double RadiusTolerance = 1e-4;

    public static CurveGroupingResult Group(IReadOnlyList<AlignmentSegment> segments)
    {
        if (segments == null) throw new ArgumentNullException(nameof(segments));

        var groups = new List<CurveGroup>();
        var warnings = new List<string>();
        var i = 0;
        while (i < segments.Count)
        {
            if (segments[i].Kind == SegmentKind.Line)
            {
                i++;
                continue;
            }

            var run = new List<AlignmentSegment>();
            while (i < segments.Count && segments[i].Kind != SegmentKind.Line) run.Add(segments[i++]);

            var group = TryBuild(run, groups.Count + 1, out var problem);
            if (group != null) groups.Add(group);
            else warnings.Add($"Đường cong tại {StationFormatter.Format(run[0].StartStation, 2)}: {problem} Bỏ qua.");
        }

        return new CurveGroupingResult(groups, warnings);
    }

    private static CurveGroup TryBuild(List<AlignmentSegment> run, int index, out string problem)
    {
        problem = null;
        var arcs = run.FindAll(s => s.Kind == SegmentKind.Arc);
        if (arcs.Count != 1)
        {
            problem = arcs.Count == 0
                ? "chỉ có đường cong chuyển tiếp, không có cong tròn."
                : "cong ghép nhiều bán kính chưa được hỗ trợ.";
            return null;
        }

        var arc = arcs[0];
        var arcIndex = run.IndexOf(arc);
        if (arcIndex > 1 || run.Count - arcIndex - 1 > 1)
        {
            problem = "có nhiều hơn một đoạn chuyển tiếp ở một phía.";
            return null;
        }

        var spiralIn = arcIndex == 1 ? run[0] : null;
        var spiralOut = arcIndex + 1 < run.Count ? run[arcIndex + 1] : null;
        foreach (var s in new[] { spiralIn, spiralOut })
        {
            if (s == null) continue;
            if (s.Turn != arc.Turn || Math.Abs(s.Radius - arc.Radius) > RadiusTolerance * arc.Radius)
            {
                problem = "đường cong chuyển tiếp không nối đúng bán kính cong tròn.";
                return null;
            }
        }

        var l1 = spiralIn?.Length ?? 0;
        var l2 = spiralOut?.Length ?? 0;
        var delta = arc.Length / arc.Radius + (l1 + l2) / (2 * arc.Radius);
        return new CurveGroup(index, arc.Radius, arc.Turn, delta, l1, l2,
            run[0].StartStation, arc.StartStation, arc.StartStation + arc.Length);
    }
}
