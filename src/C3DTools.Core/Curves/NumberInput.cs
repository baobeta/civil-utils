using System.Globalization;

namespace C3DTools.Core.Curves;

/// <summary>Parses numbers typed in the grid: "12.5" or "12,5" (Vietnamese users type commas), independent of Windows culture.</summary>
public static class NumberInput
{
    public static bool TryParse(string text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var s = text.Trim();
        var comma = s.IndexOf(',');
        if (comma >= 0)
        {
            if (s.IndexOf(',', comma + 1) >= 0 || s.IndexOf('.') >= 0) return false;
            s = s.Replace(',', '.');
        }

        return double.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out value);
    }
}
