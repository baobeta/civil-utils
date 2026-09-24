using System;
using System.Collections.Generic;

namespace C3DTools.Core.Curves;

public sealed class BoxPlacement
{
    public PlanPoint Centre { get; set; }

    /// <summary>Text and frame rotation, already readable (never upside down).</summary>
    public double Rotation { get; set; }

    public PlanPoint LeaderStart { get; set; }
    public PlanPoint LeaderEnd { get; set; }

    /// <summary>Frame corners, counter-clockwise from bottom-left in the box's own axes.</summary>
    public IReadOnlyList<PlanPoint> Corners { get; set; }
}

/// <summary>Port of the second ytc:bang: box outside the PI, vertical axis on the bisector, leader through the PI.</summary>
public static class CurveBoxPlacement
{
    /// <summary>ytc:bang line spacing: 1.7h.</summary>
    public static double LineSpacing(double textHeight) => 1.7 * textHeight;

    /// <summary>ytc:bang frame width: widest line + 2h.</summary>
    public static double FrameWidth(double textWidth, double textHeight) => textWidth + 2 * textHeight;

    /// <summary>ytc:bang frame height: lines × 1.7h + 0.6h.</summary>
    public static double FrameHeight(int lineCount, double textHeight) => lineCount * LineSpacing(textHeight) + 0.6 * textHeight;

    public static BoxPlacement Place(PlanPoint pi, PlanPoint before, PlanPoint after, PlanPoint curveMid,
        double boxWidth, double boxHeight, double textHeight)
    {
        Unit(pi.X - before.X, pi.Y - before.Y, out var x1, out var y1);
        Unit(after.X - pi.X, after.Y - pi.Y, out var x2, out var y2);
        Unit(x2 - x1, y2 - y1, out var ix, out var iy);
        double ox = -ix, oy = -iy;

        var d = 4 * textHeight + boxHeight / 2;
        var centre = new PlanPoint(pi.X + ox * d, pi.Y + oy * d);
        var rotation = TextAngle.Readable(Math.Atan2(oy, ox) - Math.PI / 2);

        double rx = Math.Cos(rotation), ry = Math.Sin(rotation);   // box's horizontal axis
        double ux = -ry, uy = rx;                                  // box's vertical axis
        double hw = boxWidth / 2, hh = boxHeight / 2;
        PlanPoint Corner(double sx, double sy) =>
            new PlanPoint(centre.X + rx * hw * sx + ux * hh * sy, centre.Y + ry * hw * sx + uy * hh * sy);

        return new BoxPlacement
        {
            Centre = centre,
            Rotation = rotation,
            LeaderStart = curveMid,
            LeaderEnd = new PlanPoint(pi.X + ox * 4 * textHeight, pi.Y + oy * 4 * textHeight),
            Corners = new[] { Corner(-1, -1), Corner(1, -1), Corner(1, 1), Corner(-1, 1) },
        };
    }

    private static void Unit(double x, double y, out double ux, out double uy)
    {
        var l = Math.Sqrt(x * x + y * y);
        if (l < 1e-12) throw new ArgumentException("Không xác định được hướng: hai điểm trùng nhau hoặc đỉnh không chuyển hướng.");
        ux = x / l;
        uy = y / l;
    }
}
