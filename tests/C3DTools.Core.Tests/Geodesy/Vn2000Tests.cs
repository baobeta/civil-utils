using System;
using C3DTools.Core.Geodesy;
using Xunit;

namespace C3DTools.Core.Tests.Geodesy;

public class Vn2000Tests
{
    [Theory]
    [InlineData(3, 0.9999)]
    [InlineData(6, 0.9996)]
    public void Zone_width_sets_the_scale_factor(int zone, double k0)
    {
        Assert.Equal(k0, Vn2000.ScaleFactorForZone(zone));
    }

    [Fact]
    public void Rejects_other_zone_widths()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Vn2000.ScaleFactorForZone(4));
    }

    [Fact]
    public void Meridian_105_45_to_106_15_matches_pyproj()
    {
        // pyproj: tmerc lon_0=105.75 k=0.9999 (600000, 2300000) → tmerc lon_0=106.25 k=0.9999.
        var p = Vn2000.Convert(600000, 2300000, 105.75, 3, 106.25, 3);

        Assert.InRange(Math.Abs(p.X - 547944.8294), 0, 0.001);
        Assert.InRange(Math.Abs(p.Y - 2299770.8404), 0, 0.001);
    }

    [Fact]
    public void Meridian_shift_there_and_back_is_identity()
    {
        var there = Vn2000.Convert(612345.678, 2312345.678, 105.75, 3, 106.25, 3);
        var back = Vn2000.Convert(there.X, there.Y, 106.25, 3, 105.75, 3);

        Assert.InRange(Math.Abs(back.X - 612345.678), 0, 0.001);
        Assert.InRange(Math.Abs(back.Y - 2312345.678), 0, 0.001);
    }

    [Fact]
    public void Six_degree_zone_to_three_degree_zone_matches_pyproj()
    {
        // Same geographic point 21°01'42.5"N 105°51'15"E in UTM 48N and in VN-2000 3° 105°45'.
        var p = Vn2000.Convert(588758.1399, 2325536.1593, 105, 6, 105.75, 3);

        Assert.InRange(Math.Abs(p.X - 510827.1166), 0, 0.001);
        Assert.InRange(Math.Abs(p.Y - 2326000.1429), 0, 0.001);
    }

    [Fact]
    public void Same_meridian_and_zone_is_identity()
    {
        var t = new Vn2000Transform(105.75, 3, 105.75, 3);

        Assert.True(t.IsIdentity);
        var p = t.Apply(600000, 2300000);
        Assert.Equal(600000, p.X, 9);
        Assert.Equal(2300000, p.Y, 9);
        Assert.Equal(0, t.RotationDelta(600000, 2300000), 12);
        Assert.Equal(1, t.ScaleRatio(600000, 2300000), 12);
    }

    [Fact]
    public void Rotation_delta_and_scale_ratio_match_the_mapped_direction()
    {
        var t = new Vn2000Transform(105.75, 3, 106.25, 3);
        const double x = 600000, y = 2300000, d = 1.0;
        var p0 = t.Apply(x, y);
        foreach (var angle in new[] { 0.0, 0.7, 2.0, -1.2 })
        {
            var p1 = t.Apply(x + d * Math.Cos(angle), y + d * Math.Sin(angle));
            var mapped = Math.Atan2(p1.Y - p0.Y, p1.X - p0.X);
            var length = Math.Sqrt((p1.X - p0.X) * (p1.X - p0.X) + (p1.Y - p0.Y) * (p1.Y - p0.Y));

            Assert.Equal(0, Math.IEEERemainder(mapped - angle - t.RotationDelta(x, y), 2 * Math.PI), 7);
            Assert.Equal(length / d, t.ScaleRatio(x, y), 7);
        }

        // Moving the meridian east by 30' turns grid north clockwise: the delta is negative here.
        Assert.True(t.RotationDelta(x, y) < 0);
    }
}
