using System;

namespace C3DTools.Core.Geodesy;

/// <summary>
/// Transverse Mercator on the WGS-84 ellipsoid (a = 6378137, f = 1/298.257223563), Krüger series to n⁶ as given by
/// Karney (2011), "Transverse Mercator with an accuracy of a few nanometers": well below 1 mm within ±4° of the central meridian.
/// Angles in degrees, easting/northing in metres.
/// </summary>
public sealed class TransverseMercator
{
    public const double SemiMajorAxis = 6378137.0;
    public const double Flattening = 1 / 298.257223563;

    private static readonly double N = Flattening / (2 - Flattening);
    private static readonly double E2 = Flattening * (2 - Flattening);
    private static readonly double E = Math.Sqrt(E2);

    /// <summary>Rectifying radius A = a/(1+n)·(1 + n²/4 + n⁴/64 + n⁶/256).</summary>
    private static readonly double RectifyingRadius = SemiMajorAxis / (1 + N) * (1 + N * N / 4 + Math.Pow(N, 4) / 64 + Math.Pow(N, 6) / 256);

    private static readonly double[] Alpha = ForwardCoefficients(N);
    private static readonly double[] Beta = InverseCoefficients(N);

    private readonly double _lon0;

    public TransverseMercator(double centralMeridianDeg, double scaleFactor, double falseEasting = 500000, double falseNorthing = 0)
    {
        if (!(scaleFactor > 0)) throw new ArgumentOutOfRangeException(nameof(scaleFactor), "Hệ số tỷ lệ phải lớn hơn 0.");
        CentralMeridian = centralMeridianDeg;
        ScaleFactorAtMeridian = scaleFactor;
        FalseEasting = falseEasting;
        FalseNorthing = falseNorthing;
        _lon0 = centralMeridianDeg * Math.PI / 180;
    }

    public double CentralMeridian { get; }
    public double ScaleFactorAtMeridian { get; }
    public double FalseEasting { get; }
    public double FalseNorthing { get; }

    public void Forward(double latDeg, double lonDeg, out double easting, out double northing)
    {
        Project(latDeg, lonDeg, out var xi, out var eta, out _, out _);
        easting = FalseEasting + ScaleFactorAtMeridian * RectifyingRadius * eta;
        northing = FalseNorthing + ScaleFactorAtMeridian * RectifyingRadius * xi;
    }

    public void Inverse(double easting, double northing, out double latDeg, out double lonDeg)
    {
        var xi = (northing - FalseNorthing) / (ScaleFactorAtMeridian * RectifyingRadius);
        var eta = (easting - FalseEasting) / (ScaleFactorAtMeridian * RectifyingRadius);
        var xip = xi;
        var etap = eta;
        for (var j = 1; j <= 6; j++)
        {
            xip -= Beta[j] * Math.Sin(2 * j * xi) * Math.Cosh(2 * j * eta);
            etap -= Beta[j] * Math.Cos(2 * j * xi) * Math.Sinh(2 * j * eta);
        }

        var sinhEtap = Math.Sinh(etap);
        var cosXip = Math.Cos(xip);
        var taup = Math.Sin(xip) / Math.Sqrt(sinhEtap * sinhEtap + cosXip * cosXip);
        var lambda = Math.Atan2(sinhEtap, cosXip);
        var tau = Tau(taup);

        latDeg = Math.Atan(tau) * 180 / Math.PI;
        lonDeg = (_lon0 + lambda) * 180 / Math.PI;
    }

    /// <summary>Meridian convergence γ (degrees): the bearing of grid north measured clockwise from true north; positive east of the meridian in the northern hemisphere.</summary>
    public double ConvergenceDegrees(double latDeg, double lonDeg)
    {
        Project(latDeg, lonDeg, out _, out _, out var gamma, out _);
        return gamma * 180 / Math.PI;
    }

    /// <summary>Point scale factor k (grid length / ellipsoid length).</summary>
    public double ScaleFactor(double latDeg, double lonDeg)
    {
        Project(latDeg, lonDeg, out _, out _, out _, out var k);
        return k;
    }

    /// <summary>ξ, η on the unit rectifying sphere, convergence γ (radians) and scale k.</summary>
    private void Project(double latDeg, double lonDeg, out double xi, out double eta, out double gamma, out double k)
    {
        var phi = latDeg * Math.PI / 180;
        var lambda = Math.IEEERemainder(lonDeg * Math.PI / 180 - _lon0, 2 * Math.PI);
        var tau = Math.Tan(phi);
        var taup = TauPrime(tau);
        var cosLambda = Math.Cos(lambda);
        var sinLambda = Math.Sin(lambda);
        var hyp = Math.Sqrt(taup * taup + cosLambda * cosLambda);

        var xip = Math.Atan2(taup, cosLambda);
        var etap = Asinh(sinLambda / hyp);

        xi = xip;
        eta = etap;
        double p = 1, q = 0;
        for (var j = 1; j <= 6; j++)
        {
            var s = Math.Sin(2 * j * xip);
            var c = Math.Cos(2 * j * xip);
            var sh = Math.Sinh(2 * j * etap);
            var ch = Math.Cosh(2 * j * etap);
            xi += Alpha[j] * s * ch;
            eta += Alpha[j] * c * sh;
            p += 2 * j * Alpha[j] * c * ch;
            q += 2 * j * Alpha[j] * s * sh;
        }

        gamma = Math.Atan2(sinLambda * taup, cosLambda * Math.Sqrt(1 + taup * taup)) + Math.Atan2(q, p);
        var sinPhi = Math.Sin(phi);
        k = ScaleFactorAtMeridian * RectifyingRadius / SemiMajorAxis
            * Math.Sqrt(1 - E2 * sinPhi * sinPhi) * Math.Sqrt(1 + tau * tau) / hyp * Math.Sqrt(p * p + q * q);
    }

    /// <summary>τ' = tan of the conformal latitude.</summary>
    private static double TauPrime(double tau)
    {
        var tau1 = Math.Sqrt(1 + tau * tau);
        var sigma = Math.Sinh(E * Atanh(E * tau / tau1));
        return tau * Math.Sqrt(1 + sigma * sigma) - sigma * tau1;
    }

    /// <summary>Inverts TauPrime by Newton's method (Karney 2011, eqs. 19–21).</summary>
    private static double Tau(double taup)
    {
        var tau = taup;
        for (var i = 0; i < 10; i++)
        {
            var tau1 = Math.Sqrt(1 + tau * tau);
            var tp = TauPrime(tau);
            var dtau = (taup - tp) * (1 + (1 - E2) * tau * tau) / ((1 - E2) * tau1 * Math.Sqrt(1 + tp * tp));
            tau += dtau;
            if (Math.Abs(dtau) < 1e-15 * Math.Max(1, Math.Abs(tau))) break;
        }

        return tau;
    }

    private static double Asinh(double x) => Math.Log(x + Math.Sqrt(x * x + 1));

    private static double Atanh(double x) => 0.5 * Math.Log((1 + x) / (1 - x));

    /// <summary>α1..α6 (index 0 unused).</summary>
    private static double[] ForwardCoefficients(double n)
    {
        double n2 = n * n, n3 = n2 * n, n4 = n3 * n, n5 = n4 * n, n6 = n5 * n;
        return new[]
        {
            0,
            n / 2 - 2 * n2 / 3 + 5 * n3 / 16 + 41 * n4 / 180 - 127 * n5 / 288 + 7891 * n6 / 37800,
            13 * n2 / 48 - 3 * n3 / 5 + 557 * n4 / 1440 + 281 * n5 / 630 - 1983433 * n6 / 1935360,
            61 * n3 / 240 - 103 * n4 / 140 + 15061 * n5 / 26880 + 167603 * n6 / 181440,
            49561 * n4 / 161280 - 179 * n5 / 168 + 6601661 * n6 / 7257600,
            34729 * n5 / 80640 - 3418889 * n6 / 1995840,
            212378941 * n6 / 319334400,
        };
    }

    /// <summary>β1..β6 (index 0 unused).</summary>
    private static double[] InverseCoefficients(double n)
    {
        double n2 = n * n, n3 = n2 * n, n4 = n3 * n, n5 = n4 * n, n6 = n5 * n;
        return new[]
        {
            0,
            n / 2 - 2 * n2 / 3 + 37 * n3 / 96 - n4 / 360 - 81 * n5 / 512 + 96199 * n6 / 604800,
            n2 / 48 + n3 / 15 - 437 * n4 / 1440 + 46 * n5 / 105 - 1118711 * n6 / 3870720,
            17 * n3 / 480 - 37 * n4 / 840 - 209 * n5 / 4480 + 5569 * n6 / 90720,
            4397 * n4 / 161280 - 11 * n5 / 504 - 830251 * n6 / 7257600,
            4583 * n5 / 161280 - 108847 * n6 / 3991680,
            20648693 * n6 / 638668800,
        };
    }
}
