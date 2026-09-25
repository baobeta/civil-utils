using System;
using System.Collections.Generic;

namespace C3DTools.Core.Curves;

public sealed class CurveGeometry
{
    public PlanPoint ArcCentre { get; set; }

    /// <summary>NĐ: where the curve leaves the incoming tangent (TĐ when L1 = 0).</summary>
    public PlanPoint Start { get; set; }

    /// <summary>NC: where the curve joins the outgoing tangent (TC when L2 = 0).</summary>
    public PlanPoint End { get; set; }

    /// <summary>ARC start/end angles, counter-clockwise as AutoCAD draws them: TĐ → TC on a left turn, TC → TĐ on a right turn.</summary>
    public double ArcDrawStartAngle { get; set; }
    public double ArcDrawEndAngle { get; set; }

    /// <summary>TĐ (end of the entry spiral), or NĐ when there is no spiral.</summary>
    public PlanPoint ArcStart { get; set; }

    /// <summary>TC (start of the exit spiral), or NC when there is no spiral.</summary>
    public PlanPoint ArcEnd { get; set; }

    /// <summary>Point of the arc nearest the PI (the P stake and the leader start).</summary>
    public PlanPoint Mid { get; set; }

    /// <summary>NĐ → TĐ, 21 points; empty when L1 = 0.</summary>
    public IReadOnlyList<PlanPoint> SpiralIn { get; set; }

    /// <summary>TC → NC, 21 points; empty when L2 = 0.</summary>
    public IReadOnlyList<PlanPoint> SpiralOut { get; set; }
}

/// <summary>Port of the drawing geometry of c:YTC (ARC + clothoid LWPOLYLINEs) for one curve.</summary>
public static class CurveGeometryBuilder
{
    private const int Segments = 20;

    /// <param name="turn">-1 = left turn, 1 = right turn (DesignedCurve.Turn). The LISP's sgn is -turn.</param>
    public static CurveGeometry Build(PlanPoint pi, PlanPoint before, PlanPoint after,
        double radius, double spiralIn, double spiralOut, int turn, CurveElements elements)
    {
        if (elements == null) throw new ArgumentNullException(nameof(elements));
        if (turn != -1 && turn != 1) throw new ArgumentOutOfRangeException(nameof(turn));

        Unit(pi.X - before.X, pi.Y - before.Y, out var x1, out var y1);
        Unit(after.X - pi.X, after.Y - pi.Y, out var x2, out var y2);
        double sgn = -turn;
        double n1x = -y1 * sgn, n1y = x1 * sgn;   // lnorm(v1)·sgn: towards the centre
        double n2x = -y2 * sgn, n2y = x2 * sgn;

        var start = new PlanPoint(pi.X - x1 * elements.T1, pi.Y - y1 * elements.T1);   // NĐ
        var end = new PlanPoint(pi.X + x2 * elements.T2, pi.Y + y2 * elements.T2);     // NC

        // Same as the LISP's pi + inw·(R+P) when L1 = L2, and still exact when L1 ≠ L2.
        var d1 = radius + elements.Shift1;
        var centre = new PlanPoint(start.X + x1 * elements.TangentOffset1 + n1x * d1,
            start.Y + y1 * elements.TangentOffset1 + n1y * d1);

        var pin = new List<PlanPoint>();
        var pout = new List<PlanPoint>();
        if (spiralIn > 0)
            for (var k = 0; k <= Segments; k++)
            {
                var xy = Clothoid(spiralIn * k / Segments, radius, spiralIn);
                pin.Add(new PlanPoint(start.X + x1 * xy.X + n1x * xy.Y, start.Y + y1 * xy.X + n1y * xy.Y));
            }

        if (spiralOut > 0)
            for (var k = Segments; k >= 0; k--)
            {
                var xy = Clothoid(spiralOut * k / Segments, radius, spiralOut);
                pout.Add(new PlanPoint(end.X - x2 * xy.X + n2x * xy.Y, end.Y - y2 * xy.X + n2y * xy.Y));
            }

        Unit(pi.X - centre.X, pi.Y - centre.Y, out var mx, out var my);
        var arcStart = pin.Count > 0 ? pin[pin.Count - 1] : start;
        var arcEnd = pout.Count > 0 ? pout[0] : end;
        var angleStart = Math.Atan2(arcStart.Y - centre.Y, arcStart.X - centre.X);
        var angleEnd = Math.Atan2(arcEnd.Y - centre.Y, arcEnd.X - centre.X);
        return new CurveGeometry
        {
            ArcCentre = centre,
            Start = start,
            End = end,
            ArcStart = arcStart,
            ArcEnd = arcEnd,
            // c:YTC: (if (> sgn 0) (arc cc r (angle cc sc) (angle cc cs)) (arc cc r (angle cc cs) (angle cc sc)))
            ArcDrawStartAngle = sgn > 0 ? angleStart : angleEnd,
            ArcDrawEndAngle = sgn > 0 ? angleEnd : angleStart,
            Mid = new PlanPoint(centre.X + mx * radius, centre.Y + my * radius),
            SpiralIn = pin,
            SpiralOut = pout,
        };
    }

    /// <summary>ytc:clo: local clothoid point at length l from the tangent point, A² = R·L, series to l¹¹.</summary>
    public static PlanPoint Clothoid(double l, double radius, double length)
    {
        var a = radius * length;   // A²
        return new PlanPoint(
            l - Math.Pow(l, 5) / (40 * a * a) + Math.Pow(l, 9) / (3456 * Math.Pow(a, 4)),
            Math.Pow(l, 3) / (6 * a) - Math.Pow(l, 7) / (336 * Math.Pow(a, 3)) + Math.Pow(l, 11) / (42240 * Math.Pow(a, 5)));
    }

    private static void Unit(double x, double y, out double ux, out double uy)
    {
        var l = Math.Sqrt(x * x + y * y);
        if (l < 1e-12) throw new ArgumentException("Không xác định được hướng: hai điểm trùng nhau hoặc đỉnh không chuyển hướng.");
        ux = x / l;
        uy = y / l;
    }
}
