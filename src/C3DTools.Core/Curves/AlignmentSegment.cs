namespace C3DTools.Core.Curves;

public enum SegmentKind { Line, Arc, Spiral }

/// <summary>One alignment sub-entity as plain data. The host fills it from Civil 3D.</summary>
public sealed class AlignmentSegment
{
    public AlignmentSegment(SegmentKind kind, double startStation, double length, double radius, int turn)
    {
        Kind = kind;
        StartStation = startStation;
        Length = length;
        Radius = radius;
        Turn = turn;
    }

    public SegmentKind Kind { get; }
    public double StartStation { get; }
    public double Length { get; }

    /// <summary>Arc radius; for a spiral, the radius at its curved end. 0 for lines.</summary>
    public double Radius { get; }

    /// <summary>+1 turns right (clockwise), -1 turns left, 0 for lines.</summary>
    public int Turn { get; }
}
