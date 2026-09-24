using System.Collections.Generic;

namespace C3DTools.Core.Curves;

/// <summary>TCVN tables transcribed into the project preset. No built-in values.</summary>
public sealed class CurveRules
{
    public string Source { get; set; } = "";   // e.g. "TCVN 4054:2005, Bảng 11/12/14"
    public List<SpeedRadiusRule> MinRadius { get; set; } = new List<SpeedRadiusRule>();
    public List<RadiusRangeRule> MinSpiral { get; set; } = new List<RadiusRangeRule>();
    public List<RadiusRangeRule> Widening { get; set; } = new List<RadiusRangeRule>();
}

public sealed class SpeedRadiusRule
{
    public double DesignSpeed { get; set; }

    /// <summary>Rmin giới hạn: below this is an error.</summary>
    public double MinRadius { get; set; }

    /// <summary>Rmin thông thường: below this is a warning; also the suggested R.</summary>
    public double NormalRadius { get; set; }
}

/// <summary>Value applies when RadiusFrom &lt; R ≤ RadiusTo. DesignSpeed 0 = any speed.</summary>
public sealed class RadiusRangeRule
{
    public double DesignSpeed { get; set; }
    public double RadiusFrom { get; set; }
    public double RadiusTo { get; set; }
    public double Value { get; set; }
}

public sealed class CurveBoxOptions
{
    public double TextHeight { get; set; } = 2.5;
    public double OffsetFromAlignment { get; set; } = 10;
    public int LengthDecimals { get; set; } = 2;
    public int AngleSecondDecimals { get; set; } = 0;
    public bool ShowZeroValues { get; set; }   // decision D4
}
