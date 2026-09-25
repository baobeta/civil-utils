using System.Collections.Generic;
using Autodesk.Civil;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Core.Curves;

namespace C3DTools.Civil2021.Curves;

/// <summary>Tangent line of an alignment, with its stations.</summary>
internal sealed class AlignmentTangent
{
    public AlignmentTangent(PlanPoint start, PlanPoint end, double startStation, double endStation)
    {
        Start = start;
        End = end;
        StartStation = startStation;
        EndStation = endStation;
    }

    public PlanPoint Start { get; }
    public PlanPoint End { get; }
    public double StartStation { get; }
    public double EndStation { get; }
}

/// <summary>Flattens an alignment's entities into Core's plain AlignmentSegment list.</summary>
internal static class AlignmentGeometryReader
{
    public static List<AlignmentSegment> Read(Alignment alignment, List<string> warnings)
    {
        var list = new List<AlignmentSegment>();
        var nonClothoid = false;
        foreach (var sub in SubEntities(alignment))
        {
            switch (sub)
            {
                case AlignmentSubEntityLine line:
                    list.Add(new AlignmentSegment(SegmentKind.Line, line.StartStation, line.Length, 0, 0));
                    break;
                case AlignmentSubEntityArc arc:
                    list.Add(new AlignmentSegment(SegmentKind.Arc, arc.StartStation, arc.Length,
                        arc.Radius, arc.Clockwise ? 1 : -1));
                    break;
                case AlignmentSubEntitySpiral spiral:
                    if (spiral.SpiralDefinition != SpiralType.Clothoid) nonClothoid = true;
                    var r = IsFinite(spiral.RadiusIn) && spiral.RadiusIn > 0 ? spiral.RadiusIn : spiral.RadiusOut;
                    list.Add(new AlignmentSegment(SegmentKind.Spiral, spiral.StartStation, spiral.Length,
                        r, spiral.Direction == SpiralDirectionType.DirectionRight ? 1 : -1));
                    break;
            }
        }

        if (nonClothoid)
            warnings?.Add("Tuyến có đường cong chuyển tiếp không phải Clothoid; T, P có thể sai lệch.");
        list.Sort((a, b) => a.StartStation.CompareTo(b.StartStation));
        return list;
    }

    /// <summary>The alignment's straight sub-entities in station order.</summary>
    public static List<AlignmentTangent> ReadTangents(Alignment alignment)
    {
        var list = new List<AlignmentTangent>();
        foreach (var sub in SubEntities(alignment))
        {
            if (sub is AlignmentSubEntityLine line)
                list.Add(new AlignmentTangent(new PlanPoint(line.StartPoint.X, line.StartPoint.Y),
                    new PlanPoint(line.EndPoint.X, line.EndPoint.Y), line.StartStation, line.EndStation));
        }

        list.Sort((a, b) => a.StartStation.CompareTo(b.StartStation));
        return list;
    }

    private static IEnumerable<AlignmentSubEntity> SubEntities(Alignment alignment)
    {
        var entities = alignment.Entities;
        for (var i = 0; i < entities.Count; i++)
        {
            var entity = entities.GetEntityByOrder(i);
            for (var j = 0; j < entity.SubEntityCount; j++) yield return entity[j];
        }
    }

    private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
}
