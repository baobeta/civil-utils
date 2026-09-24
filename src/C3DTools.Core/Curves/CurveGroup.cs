using System;

namespace C3DTools.Core.Curves;

/// <summary>One PI: optional spiral in, circular arc, optional spiral out.</summary>
public sealed class CurveGroup
{
    public CurveGroup(int index, double radius, int turn, double deltaRadians,
        double spiralIn, double spiralOut, double startStation, double arcStartStation, double arcEndStation)
    {
        Index = index;
        Radius = radius;
        Turn = turn;
        DeltaRadians = deltaRadians;
        SpiralIn = spiralIn;
        SpiralOut = spiralOut;
        StartStation = startStation;
        ArcStartStation = arcStartStation;
        ArcEndStation = arcEndStation;
    }

    /// <summary>1-based PI number ("Đ1", "Đ2"...).</summary>
    public int Index { get; }
    public double Radius { get; }
    public int Turn { get; }
    public double DeltaRadians { get; }
    public double DeltaDegrees => DeltaRadians * 180.0 / Math.PI;
    public double SpiralIn { get; }
    public double SpiralOut { get; }

    /// <summary>NĐ when there is a spiral in, otherwise TĐ.</summary>
    public double StartStation { get; }
    public double ArcStartStation { get; }
    public double ArcEndStation { get; }
    public double EndStation => ArcEndStation + SpiralOut;
    public double ArcMidStation => (ArcStartStation + ArcEndStation) / 2;
}
