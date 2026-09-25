using System.Collections.Generic;
using Newtonsoft.Json;

namespace C3DTools.Core.Presets;

// Preset sections for the 0.3–0.4 commands. All additive: a preset without them loads with these defaults.
// Lists that have default items use ObjectCreationHandling.Replace so loading does not append to the defaults.

/// <summary>CTFONT: the font SHX/TCVN3 text styles are converted to.</summary>
public sealed class FontConversionOptions
{
    public string TargetFont { get; set; } = "Arial";
}

/// <summary>CTLAYER: objects on layers matching Pattern (wildcards like "*COC*") move to Layer.</summary>
public sealed class LayerMapRule
{
    public string Pattern { get; set; } = "";
    public string Layer { get; set; } = "";

    /// <summary>ACI colour of the target layer when it has to be created.</summary>
    public short Color { get; set; } = 7;

    public string Linetype { get; set; } = "Continuous";
}

/// <summary>CTTOADO: the stake coordinate table.</summary>
public sealed class StakeTableOptions
{
    public int XDecimals { get; set; } = 3;
    public int YDecimals { get; set; } = 3;
    public int ZDecimals { get; set; } = 2;
    public bool IncludeZ { get; set; } = true;

    /// <summary>
    /// True (VN-2000 stake-out sheets): column X is the northing (drawing Y), column Y the easting (drawing X).
    /// XDecimals / YDecimals stay with the drawing X / Y value whichever column it is printed in.
    /// </summary>
    public bool NorthingAsX { get; set; } = true;
}

/// <summary>One value per design speed (km/h).</summary>
public sealed class SpeedValueRule
{
    public double DesignSpeed { get; set; }
    public double Value { get; set; }
}

/// <summary>CTCONGDUNG: vertical curve and grade limits by design speed. Empty until the project fills them; checks are then skipped.</summary>
public sealed class VerticalRules
{
    public string Source { get; set; } = "";
    public List<SpeedValueRule> MinRadiusCrest { get; set; } = new List<SpeedValueRule>();
    public List<SpeedValueRule> MinRadiusSag { get; set; } = new List<SpeedValueRule>();
    public List<SpeedValueRule> MinLength { get; set; } = new List<SpeedValueRule>();

    /// <summary>Percent.</summary>
    public List<SpeedValueRule> MaxGrade { get; set; } = new List<SpeedValueRule>();
}

/// <summary>One row of a profile/section data table: Key is what fills it, Label is printed in the header column.</summary>
public sealed class TableRowSpec
{
    public TableRowSpec() { }

    public TableRowSpec(string key, string label, int decimals)
    {
        Key = key;
        Label = label;
        Decimals = decimals;
    }

    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public int Decimals { get; set; } = 2;
}

/// <summary>
/// CTTRACDOC: the data table under a profile view, rows top to bottom. Keys: see ProfileTableBuilder
/// ("Distance" and "ElevationDifference" are read as PartialDistance and CutFill).
/// </summary>
public sealed class ProfileTableOptions
{
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<TableRowSpec> Rows { get; set; } = new List<TableRowSpec>
    {
        new TableRowSpec("StakeName", "Tên cọc", 0),
        new TableRowSpec("Distance", "Khoảng cách lẻ", 2),
        new TableRowSpec("CumulativeDistance", "Khoảng cách cộng dồn", 2),
        new TableRowSpec("Station", "Lý trình", 2),
        new TableRowSpec("GroundElevation", "Cao độ tự nhiên", 2),
        new TableRowSpec("DesignElevation", "Cao độ thiết kế", 2),
        new TableRowSpec("ElevationDifference", "Chênh cao", 2),
        new TableRowSpec("Grade", "Độ dốc dọc", 2),
    };

    public double RowHeight { get; set; } = 8;
    public double TextHeight { get; set; } = 2.5;

    /// <summary>Per-station values (tên cọc, lý trình, cao độ, …) written vertically along the stake line.</summary>
    public bool RotateStationText { get; set; } = true;
    /// <summary>
    /// False (default, as 0.3 CTTRACDOC): TextHeight and RowHeight are drawing units. True: they are mm on paper and are
    /// multiplied by SheetLayout.Scale / 1000 (2.5 mm at 1:200 = 0.5 m).
    /// </summary>
    public bool ScaleTextToPlot { get; set; }
}

/// <summary>CTTRACNGANG: the data table under a section view.</summary>
public sealed class SectionTableOptions
{
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<TableRowSpec> Rows { get; set; } = new List<TableRowSpec>
    {
        new TableRowSpec("GroundElevation", "Cao độ tự nhiên", 2),
        new TableRowSpec("DesignElevation", "Cao độ thiết kế", 2),
        new TableRowSpec("Distance", "Khoảng cách lẻ", 2),
        new TableRowSpec("CutArea", "Diện tích đào", 2),
        new TableRowSpec("FillArea", "Diện tích đắp", 2),
    };

    public double RowHeight { get; set; } = 8;
    public double TextHeight { get; set; } = 2.5;

    /// <summary>Per-offset values (cao độ, khoảng cách tim) written vertically along the offset tick.</summary>
    public bool RotateOffsetText { get; set; } = true;
    /// <summary>
    /// True (default): TextHeight and RowHeight are mm on paper, multiplied by SheetLayout.Scale / 1000 in the drawing
    /// (drawing unit = metre; 2.5 mm at 1:200 = 0.5 m). False: they are drawing units.
    /// </summary>
    public bool ScaleTextToPlot { get; set; } = true;
}

/// <summary>CTXEPTRANG: sheet size (mm, landscape), margins and how many views go on one sheet.</summary>
public sealed class SheetLayoutOptions
{
    public string Paper { get; set; } = "A3";
    public double Width { get; set; } = 420;
    public double Height { get; set; } = 297;
    public double MarginLeft { get; set; } = 25;
    public double MarginRight { get; set; } = 10;
    public double MarginTop { get; set; } = 10;
    public double MarginBottom { get; set; } = 10;
    public int Columns { get; set; } = 3;
    public int Rows { get; set; } = 2;

    /// <summary>mm on paper between neighbouring views.</summary>
    public double Gap { get; set; } = 10;

    /// <summary>Plot scale 1:Scale; the drawing unit is the metre, so 1 mm on paper = Scale / 1000 drawing units.</summary>
    public double Scale { get; set; } = 200;
}

/// <summary>CTVN2000: a province's central meridian (degrees + minutes).</summary>
public sealed class Vn2000Province
{
    public string Province { get; set; } = "";
    public int MeridianDeg { get; set; }
    public int MeridianMin { get; set; }

    /// <summary>False until someone has checked the meridian against the official text.</summary>
    public bool Verified { get; set; }

    /// <summary>Where the value comes from, e.g. the decision number.</summary>
    public string Source { get; set; } = "";

    [JsonIgnore]
    public double Meridian => MeridianDeg + MeridianMin / 60.0;
}

public sealed class Vn2000Options
{
    public List<Vn2000Province> Provinces { get; set; } = new List<Vn2000Province>();
}

/// <summary>CTMATDIA.</summary>
public sealed class SurfaceOptions
{
    /// <summary>m. Triangles with a longer edge are dropped.</summary>
    public double MaxEdgeLength { get; set; } = 50;

    /// <summary>m. Contour intervals used when the surface style cannot be read.</summary>
    public double MajorInterval { get; set; } = 5;

    public double MinorInterval { get; set; } = 1;

    /// <summary>Most decimals of a contour label; trailing zeros are dropped (12.50 → "12.5").</summary>
    public int ContourDecimals { get; set; } = 2;

    public double ContourTextHeight { get; set; } = 2.5;

    /// <summary>m along the picked line: a label closer than this to the previous one is skipped.</summary>
    public double ContourLabelSpacing { get; set; } = 5;
}

/// <summary>CTBANGCONG: the culvert/pipe table.</summary>
public sealed class CulvertOptions
{
    public int ElevationDecimals { get; set; } = 2;
    public int LengthDecimals { get; set; } = 2;
    public int SlopeDecimals { get; set; } = 2;

    /// <summary>
    /// True when Pipe.StartPoint/EndPoint Z is on the pipe centreline: invert = Z − inner height / 2.
    /// False (default): Z is taken as the invert. Not yet verified on a real 2021 network (spike #6).
    /// </summary>
    public bool EndpointIsCentreline { get; set; }
}
