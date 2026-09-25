using System;
using System.Collections.Generic;
using System.Globalization;
using C3DTools.Core.Curves;

namespace C3DTools.Core.Geodesy;

/// <summary>Central meridian as typed: "105°45'", "105 45", "105d45", "105.75" or "105,75"; seconds optional ("105°45'36\"").</summary>
public static class MeridianText
{
    private static readonly char[] Separators = { '°', 'º', 'd', 'D', '\'', '′', '’', '"', '″', ' ', '\t' };

    public static bool TryParse(string text, out double degrees)
    {
        degrees = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = new List<string>();
        foreach (var part in text.Trim().Split(Separators, StringSplitOptions.RemoveEmptyEntries)) parts.Add(part);
        if (parts.Count == 0 || parts.Count > 3) return false;

        if (parts.Count == 1)
        {
            if (!NumberInput.TryParse(parts[0], out degrees)) return false;
            return degrees >= -180 && degrees <= 180;
        }

        if (!int.TryParse(parts[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var deg)) return false;
        if (!NumberInput.TryParse(parts[1], out var min) || min < 0 || min >= 60) return false;
        double sec = 0;
        if (parts.Count == 3 && (!NumberInput.TryParse(parts[2], out sec) || sec < 0 || sec >= 60)) return false;
        if (parts.Count == 3 && min != Math.Floor(min)) return false;

        var value = Math.Abs(deg) + min / 60 + sec / 3600;
        degrees = parts[0].StartsWith("-", StringComparison.Ordinal) ? -value : value;
        return degrees >= -180 && degrees <= 180;
    }

    /// <summary>"105°45'"; seconds appear only when the meridian is not a whole minute ("105°45'36\"").</summary>
    public static string Format(double degrees)
    {
        var sign = degrees < 0 ? "-" : "";
        var totalSeconds = Math.Round(Math.Abs(degrees) * 3600, 2);
        var deg = (int)Math.Floor(totalSeconds / 3600);
        var rest = totalSeconds - deg * 3600;
        var min = (int)Math.Floor(rest / 60 + 1e-9);
        var sec = rest - min * 60;
        if (sec < 0) sec = 0;
        var text = sign + deg.ToString(CultureInfo.InvariantCulture) + "°" + min.ToString("00", CultureInfo.InvariantCulture) + "'";
        if (sec > 0.005) text += sec.ToString("0.##", CultureInfo.InvariantCulture) + "\"";
        return text;
    }
}
