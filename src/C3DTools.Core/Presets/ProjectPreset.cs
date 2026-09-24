using System;
using System.Collections.Generic;
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
}
