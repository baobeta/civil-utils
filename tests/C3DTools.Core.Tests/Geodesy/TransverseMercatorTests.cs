using System;
using C3DTools.Core.Geodesy;
using Xunit;

namespace C3DTools.Core.Tests.Geodesy;

public class TransverseMercatorTests
{
    // Expected values: pyproj 3.x (PROJ tmerc, WGS-84), computed independently in the scratchpad.
    [Theory]
    [InlineData(21.0284, 105.8542, 105, 0.9996, 588761.6466, 2325528.1841, 0.306533283, 0.9996973603)]
    [InlineData(10.7769, 106.7009, 105.75, 0.9999, 603992.9740, 1191830.2482, 0.177820357, 1.0000337696)]
    [InlineData(16.0544, 108.2022, 107.75, 0.9999, 548383.4212, 1775538.3174, 0.125058312, 0.9999289397)]
    [InlineData(8.6, 104.7, 104.5, 0.9999, 522013.0607, 950920.8126, 0.029907190, 0.9999059948)]
    [InlineData(23.3, 102.2, 103, 0.9999, 418170.6895, 2577729.6120, -0.316454046, 0.9999826907)]
    [InlineData(12.24, 109.19, 108.25, 0.9999, 602271.0149, 1353676.5562, 0.199304093, 1.0000293589)]
    [InlineData(22.5, 110.0, 105, 0.9996, 1014740.5718, 2496781.6230, 1.917645103, 1.0028751777)]
    [InlineData(9.0, 102.0, 105, 0.9996, 170113.6876, 996204.1341, -0.469730526, 1.0009469739)]
    public void Forward_matches_pyproj_within_a_millimetre(double lat, double lon, double cm, double k0,
        double e, double n, double convergenceDeg, double scale)
    {
        var tm = new TransverseMercator(cm, k0);

        tm.Forward(lat, lon, out var easting, out var northing);

        Assert.InRange(Math.Abs(easting - e), 0, 0.001);
        Assert.InRange(Math.Abs(northing - n), 0, 0.001);
        Assert.Equal(convergenceDeg, tm.ConvergenceDegrees(lat, lon), 7);
        Assert.Equal(scale, tm.ScaleFactor(lat, lon), 8);
    }

    [Fact]
    public void Utm_48N_sanity_for_Ha_Noi()
    {
        // 21°01'42.5"N 105°51'15"E; pyproj: E 588758.1399, N 2325536.1593.
        var tm = new TransverseMercator(105, 0.9996);

        tm.Forward(21 + 1 / 60.0 + 42.5 / 3600, 105 + 51 / 60.0 + 15 / 3600.0, out var e, out var n);

        Assert.InRange(e, 586000, 590000);
        Assert.InRange(Math.Abs(e - 588758.1399), 0, 0.001);
        Assert.InRange(Math.Abs(n - 2325536.1593), 0, 0.001);
    }

    [Fact]
    public void Round_trip_is_below_a_millimetre_across_Vietnam()
    {
        var zones = new[] { (105.0, 0.9996), (105.75, 0.9999), (107.75, 0.9999), (104.5, 0.9999) };
        var count = 0;
        for (var i = 0; i < 20; i++)
        {
            var lat = 8 + 15 * i / 19.0;
            var lon = 102 + 8 * ((i * 7) % 20) / 19.0;
            var (cm, k0) = zones[i % zones.Length];
            var tm = new TransverseMercator(cm, k0);

            tm.Forward(lat, lon, out var e, out var n);
            tm.Inverse(e, n, out var lat2, out var lon2);
            tm.Forward(lat2, lon2, out var e2, out var n2);

            Assert.InRange(Math.Abs(lat2 - lat) * 111_000, 0, 0.001);
            Assert.InRange(Math.Abs(lon2 - lon) * 111_000, 0, 0.001);
            Assert.InRange(Math.Sqrt((e2 - e) * (e2 - e) + (n2 - n) * (n2 - n)), 0, 0.001);
            count++;
        }

        Assert.Equal(20, count);
    }

    [Fact]
    public void Central_meridian_maps_to_false_easting_with_k0()
    {
        var tm = new TransverseMercator(105, 0.9999);

        tm.Forward(15, 105, out var e, out _);

        Assert.Equal(500000, e, 6);
        Assert.Equal(0.9999, tm.ScaleFactor(15, 105), 10);
        Assert.Equal(0, tm.ConvergenceDegrees(15, 105), 10);
    }

    [Fact]
    public void Rejects_a_nonpositive_scale_factor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TransverseMercator(105, 0));
    }
}
