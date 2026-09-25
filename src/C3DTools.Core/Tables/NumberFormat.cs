using System;
using System.Globalization;

namespace C3DTools.Core.Tables;

public static class NumberFormat
{
    /// <summary>Fixed decimals with a dot separator regardless of Windows regional settings.</summary>
    public static string Fixed(double value, int decimals) =>
        value.ToString("F" + decimals, CultureInfo.InvariantCulture);

    /// <summary>Rounds half away from zero, then drops trailing zeros and the dot (90.00 → "90"), like YTC.lsp's ytc:n.</summary>
    public static string Trimmed(double value, int decimals)
    {
        if (decimals < 0 || decimals > 6) throw new ArgumentOutOfRangeException(nameof(decimals));

        var scale = Math.Pow(10, decimals);
        var rounded = Math.Floor(Math.Abs(value) * scale + 0.5) / scale;
        var text = rounded.ToString("F" + decimals, CultureInfo.InvariantCulture);
        if (text.IndexOf('.') >= 0) text = text.TrimEnd('0').TrimEnd('.');
        return value < 0 && rounded > 0 ? "-" + text : text;
    }
}
