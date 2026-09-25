using System;
using C3DTools.Core.Curves;

namespace C3DTools.Core.Geodesy;

/// <summary>
/// VN-2000 plane coordinates: Transverse Mercator on WGS-84, false easting 500 000 m, false northing 0;
/// 3° zones use k0 = 0.9999, 6° zones k0 = 0.9996. VN-2000's datum shift to WGS-84 is the same on both sides of a
/// meridian change, so converting between meridians is inverse + forward on the ellipsoid with no datum transform.
/// x = easting (drawing X), y = northing (drawing Y).
/// </summary>
public static class Vn2000
{
    public const double FalseEasting = 500000;
    public const double FalseNorthing = 0;

    public static double ScaleFactorForZone(int zoneWidthDeg)
    {
        switch (zoneWidthDeg)
        {
            case 3: return 0.9999;
            case 6: return 0.9996;
            default: throw new ArgumentOutOfRangeException(nameof(zoneWidthDeg), "Múi chiếu phải là 3° hoặc 6°.");
        }
    }

    public static TransverseMercator Projection(double meridianDeg, int zoneWidthDeg) =>
        new TransverseMercator(meridianDeg, ScaleFactorForZone(zoneWidthDeg), FalseEasting, FalseNorthing);

    public static PlanPoint Convert(double x, double y, double fromMeridianDeg, int fromZoneWidth, double toMeridianDeg, int toZoneWidth) =>
        new Vn2000Transform(fromMeridianDeg, fromZoneWidth, toMeridianDeg, toZoneWidth).Apply(x, y);
}

/// <summary>One meridian/zone change: points, and how directions and lengths change at a point.</summary>
public sealed class Vn2000Transform
{
    private readonly TransverseMercator _from;
    private readonly TransverseMercator _to;

    public Vn2000Transform(double fromMeridianDeg, int fromZoneWidth, double toMeridianDeg, int toZoneWidth)
    {
        _from = Vn2000.Projection(fromMeridianDeg, fromZoneWidth);
        _to = Vn2000.Projection(toMeridianDeg, toZoneWidth);
        IsIdentity = fromMeridianDeg == toMeridianDeg && fromZoneWidth == toZoneWidth;
    }

    public bool IsIdentity { get; }

    public PlanPoint Apply(double x, double y)
    {
        if (IsIdentity) return new PlanPoint(x, y);
        _from.Inverse(x, y, out var lat, out var lon);
        _to.Forward(lat, lon, out var e, out var n);
        return new PlanPoint(e, n);
    }

    /// <summary>
    /// Radians to add to a direction (angle from the X axis, counter-clockwise) at (x, y): γ_to − γ_from, the change of
    /// grid convergence. Used for block rotation.
    /// </summary>
    public double RotationDelta(double x, double y)
    {
        if (IsIdentity) return 0;
        _from.Inverse(x, y, out var lat, out var lon);
        return (_to.ConvergenceDegrees(lat, lon) - _from.ConvergenceDegrees(lat, lon)) * Math.PI / 180;
    }

    /// <summary>k_to / k_from at (x, y): a short length there is multiplied by this.</summary>
    public double ScaleRatio(double x, double y)
    {
        if (IsIdentity) return 1;
        _from.Inverse(x, y, out var lat, out var lon);
        return _to.ScaleFactor(lat, lon) / _from.ScaleFactor(lat, lon);
    }
}
