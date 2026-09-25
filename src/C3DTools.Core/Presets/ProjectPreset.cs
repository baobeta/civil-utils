using System;
using System.Collections.Generic;
using C3DTools.Core.Curves;
using C3DTools.Core.Drainage;
using C3DTools.Core.Points;

namespace C3DTools.Core.Presets;

public sealed class ProjectPreset
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0";

    public int StationDecimals { get; set; } = 2;
    public int ElevationDecimals { get; set; } = 3;
    public int VolumeDecimals { get; set; } = 2;

    /// <summary>Civil 3D style / label set names keyed by role, e.g. "Alignment" → "Tim tuyến".</summary>
    public Dictionary<string, string> Styles { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public PointFileOptions PointFile { get; set; } = new PointFileOptions();

    /// <summary>Null until the project defines its thresholds; C-08 refuses to run without them.</summary>
    public PipeRules PipeRules { get; set; }

    /// <summary>km/h. Default for CTYTC's design-speed prompt.</summary>
    public double? DesignSpeed { get; set; }

    /// <summary>Null until the project defines TCVN tables; CTYTC then skips checks and W.</summary>
    public CurveRules CurveRules { get; set; }

    public CurveBoxOptions CurveBox { get; set; } = new CurveBoxOptions();

    public FontConversionOptions FontConversion { get; set; } = new FontConversionOptions();

    /// <summary>Empty until the project defines its layer standard.</summary>
    public List<LayerMapRule> LayerMap { get; set; } = new List<LayerMapRule>();

    public StakeTableOptions StakeTable { get; set; } = new StakeTableOptions();

    public VerticalRules VerticalRules { get; set; } = new VerticalRules();

    public ProfileTableOptions ProfileTable { get; set; } = new ProfileTableOptions();

    public SectionTableOptions SectionTable { get; set; } = new SectionTableOptions();

    public SheetLayoutOptions SheetLayout { get; set; } = new SheetLayoutOptions();

    public Vn2000Options Vn2000 { get; set; } = new Vn2000Options();

    public SurfaceOptions Surface { get; set; } = new SurfaceOptions();

    public CulvertOptions Culvert { get; set; } = new CulvertOptions();
}
