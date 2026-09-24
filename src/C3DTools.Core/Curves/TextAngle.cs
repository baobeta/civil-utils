using System;

namespace C3DTools.Core.Curves;

public static class TextAngle
{
    /// <summary>Text rotation in (−π/2, π/2], so text is never upside down (ytc:coc / ytc:bang).</summary>
    public static double Readable(double radians)
    {
        var a = Math.IEEERemainder(radians, 2 * Math.PI);   // [-π, π]
        if (a > Math.PI / 2 + 1e-9) a -= Math.PI;
        else if (a <= -Math.PI / 2 + 1e-9) a += Math.PI;
        return a;
    }
}
