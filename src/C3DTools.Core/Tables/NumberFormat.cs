using System.Globalization;

namespace C3DTools.Core.Tables;

public static class NumberFormat
{
    /// <summary>Fixed decimals with a dot separator regardless of Windows regional settings.</summary>
    public static string Fixed(double value, int decimals) =>
        value.ToString("F" + decimals, CultureInfo.InvariantCulture);
}
