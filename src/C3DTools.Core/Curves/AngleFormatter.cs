using System;
using System.Globalization;

namespace C3DTools.Core.Curves;

public static class AngleFormatter
{
    public static string Dms(double degrees, int secondDecimals = 0)
    {
        if (secondDecimals < 0 || secondDecimals > 3) throw new ArgumentOutOfRangeException(nameof(secondDecimals));

        // Round once in seconds so the carry into minutes and degrees is exact.
        var totalSeconds = Math.Round(Math.Abs(degrees) * 3600, secondDecimals, MidpointRounding.AwayFromZero);
        var d = (long)Math.Floor(totalSeconds / 3600);
        var m = (int)Math.Floor((totalSeconds - d * 3600) / 60);
        var s = Math.Round(totalSeconds - d * 3600 - m * 60, secondDecimals, MidpointRounding.AwayFromZero);

        var width = secondDecimals == 0 ? 2 : 3 + secondDecimals;
        var sign = degrees < 0 && totalSeconds > 0 ? "-" : "";
        return sign + d.ToString(CultureInfo.InvariantCulture) + "°"
            + m.ToString("00", CultureInfo.InvariantCulture) + "'"
            + s.ToString("F" + secondDecimals, CultureInfo.InvariantCulture).PadLeft(width, '0') + "\"";
    }
}
