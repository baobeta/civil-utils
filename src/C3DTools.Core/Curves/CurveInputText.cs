using System.Globalization;
using System.Linq;

namespace C3DTools.Core.Curves;

/// <summary>
/// The curve inputs that do not fit the numeric XData of a CTYTC tag, as one invariant text:
/// "v1;speed;superelevated;rate;onSpiral;runoffIn;runoffOut;offsetIn;offsetOut" (speed empty = the route's).
/// </summary>
public static class CurveInputText
{
    private const string Version = "v1";

    public static string Format(CurveInput input)
    {
        string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);
        return string.Join(";", Version,
            input.DesignSpeed.HasValue ? N(input.DesignSpeed.Value) : "",
            input.Superelevated ? "1" : "0", N(input.SuperRate), input.RunoffOnSpiral ? "1" : "0",
            N(input.RunoffIn), N(input.RunoffOut), N(input.OffsetIn), N(input.OffsetOut));
    }

    /// <summary>Copies the values into input; false (input unchanged) when the text is not in this format.</summary>
    public static bool TryApply(string text, CurveInput input)
    {
        var parts = (text ?? "").Split(';');
        if (parts.Length != 9 || parts[0] != Version) return false;
        var numbers = new double[9];
        for (var i = 3; i < 9; i++)
            if (i != 4 && !double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i])) return false;
        double? speed = null;
        if (parts[1].Length > 0)
        {
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return false;
            speed = v;
        }

        if (!new[] { parts[2], parts[4] }.All(p => p == "0" || p == "1")) return false;
        input.DesignSpeed = speed;
        input.Superelevated = parts[2] == "1";
        input.SuperRate = numbers[3];
        input.RunoffOnSpiral = parts[4] == "1";
        input.RunoffIn = numbers[5];
        input.RunoffOut = numbers[6];
        input.OffsetIn = numbers[7];
        input.OffsetOut = numbers[8];
        return true;
    }
}
