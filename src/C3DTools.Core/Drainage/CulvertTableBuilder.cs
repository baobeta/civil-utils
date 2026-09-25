using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using C3DTools.Core.Presets;
using C3DTools.Core.Stations;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Drainage;

/// <summary>CTBANGCONG: the culvert schedule, one row per culvert in station order.</summary>
public static class CulvertTableBuilder
{
    public static readonly string[] Headers =
    {
        "STT", "Tên", "Lý trình", "Loại", "Khẩu độ", "Chiều dài (m)", "CĐ đáy thượng lưu", "CĐ đáy hạ lưu",
        "Độ dốc (%)", "Chiều sâu chôn min (m)", "Ghi chú", "Cảnh báo",
    };

    /// <summary>Skew below this (degrees) counts as a square crossing: no "Chéo" note.</summary>
    public const double SkewNoteThreshold = 0.5;

    /// <summary>By station, then offset; stable for equal stations.</summary>
    public static List<CulvertRecord> Sorted(IEnumerable<CulvertRecord> records) =>
        (records ?? Enumerable.Empty<CulvertRecord>()).Where(r => r != null).OrderBy(r => r.Station).ThenBy(r => r.Offset).ToList();

    /// <summary>
    /// The printed name of each record of a sorted list: its own Name, else C1, C2… counting only the unnamed ones
    /// and skipping numbers another record already uses as its name.
    /// </summary>
    public static string[] Names(IReadOnlyList<CulvertRecord> sorted)
    {
        var taken = new HashSet<string>(sorted.Select(r => (r.Name ?? "").Trim()).Where(n => n.Length > 0), StringComparer.OrdinalIgnoreCase);
        var names = new string[sorted.Count];
        var next = 1;
        for (var i = 0; i < sorted.Count; i++)
        {
            var own = (sorted[i].Name ?? "").Trim();
            if (own.Length > 0)
            {
                names[i] = own;
                continue;
            }

            string candidate;
            do candidate = "C" + (next++).ToString(CultureInfo.InvariantCulture);
            while (taken.Contains(candidate));
            names[i] = candidate;
        }

        return names;
    }

    /// <summary>"Chéo 15°" for a skewed crossing, else empty.</summary>
    public static string DefaultNote(CulvertRecord record) =>
        record != null && record.SkewDeg >= SkewNoteThreshold
            ? "Chéo " + NumberFormat.Trimmed(record.SkewDeg, 1) + "°"
            : "";

    /// <summary>PipeChecker slope/cover issues joined with "; ", without the "Cống X: " prefix. Empty without rules.</summary>
    public static string Warning(CulvertRecord record, string name, PipeRules rules)
    {
        if (record == null || rules == null) return "";
        var data = new PipeData
        {
            Name = name ?? "",
            Length = record.Length,
            StartInvert = record.InvertUpstream,
            EndInvert = record.InvertDownstream,
            InnerDiameter = record.InnerHeight,
            WallThickness = record.WallThickness,
            // Unknown ground → NaN cover, which no "<" check flags.
            StartGround = record.GroundUpstream ?? double.NaN,
            EndGround = record.GroundDownstream ?? double.NaN,
        };
        var prefix = "Cống " + data.Name + ": ";
        return string.Join("; ", PipeChecker.Check(data, rules).Issues.Select(i =>
            i.Message.StartsWith(prefix, StringComparison.Ordinal) ? i.Message.Substring(prefix.Length) : i.Message));
    }

    /// <summary>The schedule with the preset's station and Culvert decimals; warnings when the preset has PipeRules.</summary>
    public static TableData Build(IEnumerable<CulvertRecord> records, ProjectPreset preset)
    {
        var options = preset?.Culvert ?? new CulvertOptions();
        var stationDecimals = preset?.StationDecimals ?? 2;
        var rules = preset?.PipeRules;
        var sorted = Sorted(records);
        var names = Names(sorted);
        var table = new TableData(Headers);
        for (var i = 0; i < sorted.Count; i++)
        {
            var r = sorted[i];
            table.AddRow(
                (i + 1).ToString(CultureInfo.InvariantCulture),
                names[i],
                StationFormatter.Format(r.Station, stationDecimals),
                r.Kind ?? "",
                r.SizeText ?? "",
                Number(r.Length, options.LengthDecimals),
                Number(r.InvertUpstream, options.ElevationDecimals),
                Number(r.InvertDownstream, options.ElevationDecimals),
                Number(r.SlopePercent, options.SlopeDecimals),
                r.CoverMin.HasValue ? Number(r.CoverMin.Value, options.ElevationDecimals) : "",
                r.Note ?? "",
                Warning(r, names[i], rules));
        }

        return table;
    }

    /// <summary>Fixed decimals, invariant; empty for NaN/∞.</summary>
    public static string Number(double value, int decimals) =>
        double.IsNaN(value) || double.IsInfinity(value) ? "" : NumberFormat.Fixed(value, decimals);
}
