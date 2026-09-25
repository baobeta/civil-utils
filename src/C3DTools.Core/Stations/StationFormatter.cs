using System;
using System.Globalization;

namespace C3DTools.Core.Stations;

/// <summary>Formats and parses Vietnamese station text such as "Km1+234.56" (or "1+234.56" without the prefix).</summary>
public static class StationFormatter
{
    public static string Format(double station, int decimals, bool withKmPrefix = true)
    {
        if (decimals < 0 || decimals > 6) throw new ArgumentOutOfRangeException(nameof(decimals));

        // Round once up front so 999.999 becomes Km1+000.00, not Km0+1000.00.
        var rounded = Math.Round(Math.Abs(station), decimals, MidpointRounding.AwayFromZero);
        var km = (long)Math.Floor(rounded / 1000.0);
        var metres = Math.Round(rounded - km * 1000.0, decimals, MidpointRounding.AwayFromZero);
        if (metres >= 1000.0)
        {
            km += 1;
            metres -= 1000.0;
        }

        var width = decimals == 0 ? 3 : 4 + decimals;
        var metresText = metres.ToString("F" + decimals, CultureInfo.InvariantCulture).PadLeft(width, '0');
        var sign = station < 0 && rounded > 0 ? "-" : "";
        return sign + (withKmPrefix ? "Km" : "") + km.ToString(CultureInfo.InvariantCulture) + "+" + metresText;
    }

    public static bool TryParse(string text, out double station)
    {
        station = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var s = text.Trim();
        var negative = s.StartsWith("-", StringComparison.Ordinal);
        if (negative) s = s.Substring(1);
        if (s.StartsWith("Km", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);

        var parts = s.Split('+');
        if (parts.Length != 2) return false;
        if (!long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var km)) return false;
        if (!double.TryParse(parts[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var metres)) return false;
        if (metres >= 1000.0) return false;

        station = (km * 1000.0 + metres) * (negative ? -1 : 1);
        return true;
    }
}
