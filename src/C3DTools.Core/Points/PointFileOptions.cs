namespace C3DTools.Core.Points;

public sealed class PointFileOptions
{
    /// <summary>Column order using P (number), N, E, Z, D (description). P, N and E are required.</summary>
    public string Columns { get; set; } = "PNEZD";

    public char Delimiter { get; set; } = ',';

    // VN-2000 plausibility ranges, used only to detect swapped N/E.
    public double MinNorthing { get; set; } = 900_000;
    public double MaxNorthing { get; set; } = 2_700_000;
    public double MinEasting { get; set; } = 100_000;
    public double MaxEasting { get; set; } = 900_000;
}
