using System;
using System.Collections.Generic;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Curves;

/// <summary>The five lines of the curve box, as YTC.lsp draws them (D4, D7): every line, zeros included, no title.</summary>
public static class CurveBoxText
{
    public static IReadOnlyList<string> Build(DesignedCurve c, CurveBoxOptions o)
    {
        if (c == null) throw new ArgumentNullException(nameof(c));
        if (o == null) throw new ArgumentNullException(nameof(o));

        string N(double value) => NumberFormat.Trimmed(value, o.LengthDecimals);
        var e = c.Elements;
        string E(Func<CurveElements, double> pick) => e == null ? "?" : N(pick(e));

        var input = c.Input ?? new CurveInput();
        var angle = AngleFormatter.Dms(c.DeltaRadians * 180 / Math.PI, o.AngleSecondDecimals);
        return new[]
        {
            "A=" + angle + "  P=" + E(x => x.P),
            "R=" + N(input.Radius) + "  K=" + E(x => x.K),
            "T1=" + E(x => x.T1) + "  T2=" + E(x => x.T2),
            "L1=" + N(input.SpiralIn) + "  L2=" + N(input.SpiralOut),
            "Wb=" + N(input.Wb) + "  Wl=" + N(input.Wl),
        };
    }
}
