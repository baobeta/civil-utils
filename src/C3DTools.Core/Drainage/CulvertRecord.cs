using System;
using System.Globalization;

namespace C3DTools.Core.Drainage;

/// <summary>One culvert (pipe) of the CTBANGCONG schedule, as read from the drawing. Lengths and elevations in metres.</summary>
public sealed class CulvertRecord
{
    /// <summary>Identifies the pipe across re-reads (its handle), so dialog edits survive a new preview.</summary>
    public string Key { get; set; } = "";

    /// <summary>The Civil 3D pipe name, for messages and the preview grid.</summary>
    public string PipeName { get; set; } = "";

    public string NetworkName { get; set; } = "";

    /// <summary>Schedule name; empty = automatic C1, C2… by station.</summary>
    public string Name { get; set; } = "";

    public double Station { get; set; }

    /// <summary>Offset of the pipe midpoint from the alignment; negative = left.</summary>
    public double Offset { get; set; }

    /// <summary>Degrees between the culvert and the normal to the alignment (0 = square crossing).</summary>
    public double SkewDeg { get; set; }

    /// <summary>"Cống tròn", "Cống hộp", … (Culverts constants), or any text the user types.</summary>
    public string Kind { get; set; } = "";

    /// <summary>"D1000", "B×H 2.0×2.0".</summary>
    public string SizeText { get; set; } = "";

    /// <summary>Plan length.</summary>
    public double Length { get; set; }

    /// <summary>Inside height (diameter of a round pipe).</summary>
    public double InnerHeight { get; set; }

    public double WallThickness { get; set; }

    public double InvertUpstream { get; set; }
    public double InvertDownstream { get; set; }

    /// <summary>Rim of the end structure or surface elevation; null when unknown.</summary>
    public double? GroundUpstream { get; set; }

    public double? GroundDownstream { get; set; }

    public string Note { get; set; } = "";

    /// <summary>Percent; positive when the downstream invert is lower. NaN without a length.</summary>
    public double SlopePercent => Length > 0 ? (InvertUpstream - InvertDownstream) / Length * 100 : double.NaN;

    /// <summary>Least ground − outer top over the ends that have a ground elevation; null when none has.</summary>
    public double? CoverMin
    {
        get
        {
            var up = Cover(GroundUpstream, InvertUpstream);
            var down = Cover(GroundDownstream, InvertDownstream);
            if (up == null) return down;
            if (down == null) return up;
            return Math.Min(up.Value, down.Value);
        }
    }

    /// <summary>"Trái 2.50", "Phải 2.50" or "Tim".</summary>
    public string SideText
    {
        get
        {
            var text = Math.Abs(Offset).ToString("F2", CultureInfo.InvariantCulture);
            if (text == "0.00") return "Tim";
            return (Offset < 0 ? "Trái " : "Phải ") + text;
        }
    }

    private double? Cover(double? ground, double invert)
    {
        if (ground == null || double.IsNaN(ground.Value) || double.IsInfinity(ground.Value)) return null;
        return ground.Value - (invert + InnerHeight + WallThickness);
    }
}

/// <summary>Kind, size text, skew and invert rules of the culvert schedule.</summary>
public static class Culverts
{
    public const string Circular = "Cống tròn";
    public const string Box = "Cống hộp";
    public const string Arch = "Cống vòm";
    public const string Elliptical = "Cống elip";
    public const string Egg = "Cống trứng";
    public const string Slab = "Cống bản";
    public const string Other = "Cống";

    /// <summary>The kinds offered in the dialog's Loại column.</summary>
    public static readonly string[] Kinds = { Circular, Box, Slab, Arch, Elliptical, Egg, Other };

    /// <summary>
    /// From the Civil 3D cross-section shape (SweptShapeType name: Circular, Rectangular, Arched, Elliptical,
    /// HorizontalElliptical, EggShaped), else from words in the part description/family.
    /// </summary>
    public static string Kind(string shape, string description)
    {
        switch (shape ?? "")
        {
            case "Circular": return Circular;
            case "Rectangular": return Box;
            case "Arched": return Arch;
            case "Elliptical":
            case "HorizontalElliptical": return Elliptical;
            case "EggShaped": return Egg;
        }

        var d = (description ?? "").ToLowerInvariant();
        if (Has(d, "box", "rect", "hộp", "chữ nhật")) return Box;
        if (Has(d, "slab", "bản")) return Slab;
        if (Has(d, "arch", "vòm")) return Arch;
        if (Has(d, "ellip", "elip")) return Elliptical;
        if (Has(d, "egg", "trứng")) return Egg;
        if (Has(d, "circ", "pipe", "round", "tròn")) return Circular;
        return Other;
    }

    /// <summary>Round: "D" + inside diameter in mm ("D1000"). Others: "B×H 2.0×2.0" in metres. height ≤ 0 uses width.</summary>
    public static string SizeText(string kind, double width, double height)
    {
        if (kind == Circular)
            return "D" + Math.Round(width * 1000, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
        if (!(height > 0)) height = width;
        return "B×H " + Metres(width) + "×" + Metres(height);
    }

    /// <summary>
    /// Deviation from a square crossing, in degrees 0…90, of a culvert with plan direction (pdx, pdy) against the
    /// alignment tangent (tdx, tdy). Direction signs do not matter. NaN for a zero vector.
    /// </summary>
    public static double SkewDegrees(double pdx, double pdy, double tdx, double tdy)
    {
        var p = Math.Sqrt(pdx * pdx + pdy * pdy);
        var t = Math.Sqrt(tdx * tdx + tdy * tdy);
        if (!(p > 0) || !(t > 0)) return double.NaN;
        var cos = Math.Abs(pdx * tdx + pdy * tdy) / (p * t);
        if (cos > 1) cos = 1;
        var between = Math.Acos(cos) * 180 / Math.PI;   // 0…90 between the two lines
        return 90 - between;
    }

    /// <summary>The invert from an endpoint Z: Z itself, or Z − inner/2 when the endpoint is on the pipe centreline.</summary>
    public static double Invert(double z, double innerHeight, bool endpointIsCentreline) =>
        endpointIsCentreline ? z - innerHeight / 2 : z;

    private static bool Has(string text, params string[] words)
    {
        foreach (var w in words)
        {
            if (text.IndexOf(w, StringComparison.Ordinal) >= 0) return true;
        }

        return false;
    }

    private static string Metres(double value) => value.ToString("0.0#", CultureInfo.InvariantCulture);
}
