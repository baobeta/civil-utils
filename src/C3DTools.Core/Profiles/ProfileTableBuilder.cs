using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using C3DTools.Core.Presets;
using C3DTools.Core.Stations;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Profiles;

public enum ProfileTableRowKind
{
    /// <summary>One cell per station.</summary>
    PerStation,

    /// <summary>Merged cells between stations (khoảng cách lẻ, dốc dọc, cong đứng).</summary>
    Span,
}

/// <summary>A merged cell from one station to another, with up to two text lines.</summary>
public sealed class ProfileTableSpan
{
    public ProfileTableSpan(double from, double to, string line1, string line2 = "")
    {
        From = from;
        To = to;
        Line1 = line1 ?? "";
        Line2 = line2 ?? "";
    }

    public double From { get; }
    public double To { get; }
    public string Line1 { get; }
    public string Line2 { get; }
}

public sealed class ProfileTableRow
{
    public ProfileTableRow(string key, string label, ProfileTableRowKind kind, IReadOnlyList<string> cells, IReadOnlyList<ProfileTableSpan> spans)
    {
        Key = key;
        Label = label ?? "";
        Kind = kind;
        Cells = cells ?? new string[0];
        Spans = spans ?? new ProfileTableSpan[0];
    }

    public string Key { get; }
    public string Label { get; }
    public ProfileTableRowKind Kind { get; }

    /// <summary>PerStation: one text per station (empty when unknown). Span: empty.</summary>
    public IReadOnlyList<string> Cells { get; }

    /// <summary>Span: the merged cells. PerStation: empty.</summary>
    public IReadOnlyList<ProfileTableSpan> Spans { get; }
}

/// <summary>The profile data table: stations as columns, rows as the preset orders them, and the per-station export.</summary>
public sealed class ProfileTableModel
{
    public ProfileTableModel(IReadOnlyList<StakeStation> stations, IReadOnlyList<ProfileTableRow> rows, IReadOnlyList<string> messages, TableData export)
    {
        Stations = stations;
        Rows = rows;
        Messages = messages;
        Export = export;
    }

    public IReadOnlyList<StakeStation> Stations { get; }
    public IReadOnlyList<ProfileTableRow> Rows { get; }

    /// <summary>Rows that were left out (unknown keys).</summary>
    public IReadOnlyList<string> Messages { get; }

    /// <summary>One row per station (CSV / Excel): Tên cọc, Lý trình, KC lẻ, KC cộng dồn, CĐ TN, CĐ TK, Chênh cao, Dốc dọc.</summary>
    public TableData Export { get; }
}

/// <summary>A design grade from one PVI (or the profile start) to the next (or the profile end). Grade is a fraction.</summary>
public sealed class GradeLine
{
    public GradeLine(double start, double end, double grade)
    {
        Start = start;
        End = end;
        Grade = grade;
    }

    public double Start { get; }
    public double End { get; }
    public double Grade { get; }
    public double Length => End - Start;
}

/// <summary>
/// CTTRACDOC: builds the Vietnamese profile data table (bảng số liệu trắc dọc) from named stations, the ground
/// (surface profile) and design elevations at them, and the design profile's entities. Chênh cao = TK − TN
/// (positive = đắp, negative = đào). Dốc dọc: one merged cell per grade line, PVI to PVI, "i=+3.00%" / "L=250.00".
/// Cong đứng: one merged cell TĐ–TC per vertical curve, "R=2000 K=100".
/// </summary>
public static class ProfileTableBuilder
{
    public const string StakeName = "StakeName";
    public const string PartialDistance = "PartialDistance";
    public const string CumulativeDistance = "CumulativeDistance";
    public const string Station = "Station";
    public const string GroundElevation = "GroundElevation";
    public const string DesignElevation = "DesignElevation";
    public const string CutFill = "CutFill";
    public const string Grade = "Grade";
    public const string VerticalCurve = "VerticalCurve";

    public static readonly string[] ExportHeaders = { "Tên cọc", "Lý trình", "KC lẻ", "KC cộng dồn", "CĐ TN", "CĐ TK", "Chênh cao", "Dốc dọc" };

    /// <summary>Every supported row with its default label and decimals, in the usual order.</summary>
    public static IReadOnlyList<TableRowSpec> KnownRows() => new List<TableRowSpec>
    {
        new TableRowSpec(StakeName, "Tên cọc", 0),
        new TableRowSpec(PartialDistance, "Khoảng cách lẻ", 2),
        new TableRowSpec(CumulativeDistance, "Khoảng cách cộng dồn", 2),
        new TableRowSpec(Station, "Lý trình", 2),
        new TableRowSpec(GroundElevation, "Cao độ tự nhiên", 2),
        new TableRowSpec(DesignElevation, "Cao độ thiết kế", 2),
        new TableRowSpec(CutFill, "Chênh cao", 2),
        new TableRowSpec(Grade, "Độ dốc dọc", 2),
        new TableRowSpec(VerticalCurve, "Đường cong đứng", 2),
    };

    /// <summary>The supported key for a preset key (case-insensitive, "Distance"/"ElevationDifference" accepted); null when unknown.</summary>
    public static string Canonical(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var k = key.Trim();
        if (string.Equals(k, "Distance", StringComparison.OrdinalIgnoreCase)) return PartialDistance;
        if (string.Equals(k, "ElevationDifference", StringComparison.OrdinalIgnoreCase)) return CutFill;
        return KnownRows().Select(r => r.Key).FirstOrDefault(r => string.Equals(r, k, StringComparison.OrdinalIgnoreCase));
    }

    /// <param name="stations">Columns, in station order.</param>
    /// <param name="ground">Surface elevation per station (null = none); may be null.</param>
    /// <param name="design">Design elevation per station (null = none); may be null.</param>
    /// <param name="segments">The design profile's entities (grade and vertical curve rows); may be null.</param>
    public static ProfileTableModel Build(IReadOnlyList<StakeStation> stations, IReadOnlyList<double?> ground, IReadOnlyList<double?> design,
        IEnumerable<ProfileSegment> segments, IEnumerable<TableRowSpec> rows, int stationDecimals = 2)
    {
        if (stations == null) throw new ArgumentNullException(nameof(stations));
        if (ground != null && ground.Count != stations.Count) throw new ArgumentException("Cần một cao độ tự nhiên cho mỗi cọc.", nameof(ground));
        if (design != null && design.Count != stations.Count) throw new ArgumentException("Cần một cao độ thiết kế cho mỗi cọc.", nameof(design));

        var n = stations.Count;
        double? G(int i) => ground?[i];
        double? D(int i) => design?[i];
        double? CutFillAt(int i) => G(i).HasValue && D(i).HasValue ? D(i) - G(i) : null;
        var segmentList = (segments ?? Enumerable.Empty<ProfileSegment>()).Where(s => s != null).ToList();
        var grades = GradeLines(segmentList);
        var first = n > 0 ? stations[0].Station : 0;
        var last = n > 0 ? stations[n - 1].Station : 0;

        var result = new List<ProfileTableRow>();
        var messages = new List<string>();
        foreach (var spec in rows ?? Enumerable.Empty<TableRowSpec>())
        {
            if (spec == null) continue;
            var key = Canonical(spec.Key);
            var d = Math.Max(0, Math.Min(6, spec.Decimals));
            string[] PerStation(Func<int, string> cell) => Enumerable.Range(0, n).Select(cell).ToArray();
            switch (key)
            {
                case StakeName:
                    result.Add(Cells(key, spec, PerStation(i => stations[i].Name)));
                    break;
                case PartialDistance:
                    var gaps = new List<ProfileTableSpan>();
                    for (var i = 1; i < n; i++)
                        gaps.Add(new ProfileTableSpan(stations[i - 1].Station, stations[i].Station, F(stations[i].Station - stations[i - 1].Station, d)));
                    result.Add(Spans(key, spec, gaps));
                    break;
                case CumulativeDistance:
                    result.Add(Cells(key, spec, PerStation(i => F(stations[i].Station - first, d))));
                    break;
                case Station:
                    result.Add(Cells(key, spec, PerStation(i => StationFormatter.Format(stations[i].Station, d, withKmPrefix: false))));
                    break;
                case GroundElevation:
                    result.Add(Cells(key, spec, PerStation(i => Opt(G(i), d))));
                    break;
                case DesignElevation:
                    result.Add(Cells(key, spec, PerStation(i => Opt(D(i), d))));
                    break;
                case CutFill:
                    result.Add(Cells(key, spec, PerStation(i => CutFillAt(i).HasValue ? Signed(CutFillAt(i).Value, d) : "")));
                    break;
                case Grade:
                    var spans = new List<ProfileTableSpan>();
                    foreach (var g in grades)
                    {
                        var from = Math.Max(g.Start, first);
                        var to = Math.Min(g.End, last);
                        if (n > 1 && to - from > 1e-6)
                            spans.Add(new ProfileTableSpan(from, to, "i=" + Signed(g.Grade * 100, d) + "%", "L=" + F(g.Length, d)));
                    }

                    result.Add(Spans(key, spec, spans));
                    break;
                case VerticalCurve:
                    var curves = new List<ProfileTableSpan>();
                    foreach (var c in Profiles.VerticalCurve.FromSegments(segmentList).Where(c => c.HasCurve))
                    {
                        var from = Math.Max(c.StartStation, first);
                        var to = Math.Min(c.EndStation, last);
                        if (n > 1 && to - from > 1e-6)
                            curves.Add(new ProfileTableSpan(from, to,
                                "R=" + NumberFormat.Trimmed(c.Elements.R, d) + " K=" + NumberFormat.Trimmed(c.Elements.K, d)));
                    }

                    result.Add(Spans(key, spec, curves));
                    break;
                default:
                    messages.Add($"Bảng trắc dọc: bỏ qua dòng \"{spec.Key}\" ({spec.Label}), preset có khoá chưa hỗ trợ.");
                    break;
            }
        }

        int Dec(string key, int fallback)
        {
            var spec = (rows ?? Enumerable.Empty<TableRowSpec>()).FirstOrDefault(r => r != null && Canonical(r.Key) == key);
            return spec == null ? fallback : Math.Max(0, Math.Min(6, spec.Decimals));
        }

        var export = new TableData((string[])ExportHeaders.Clone());
        for (var i = 0; i < n; i++)
        {
            var s = stations[i].Station;
            var grade = grades.FirstOrDefault(g => s >= g.Start - 1e-6 && s < g.End - 1e-6) ?? grades.LastOrDefault(g => s >= g.Start - 1e-6);
            export.AddRow(
                stations[i].Name,
                StationFormatter.Format(s, Dec(Station, stationDecimals), withKmPrefix: false),
                F(i == 0 ? 0 : s - stations[i - 1].Station, Dec(PartialDistance, 2)),
                F(s - first, Dec(CumulativeDistance, 2)),
                Opt(G(i), Dec(GroundElevation, 2)),
                Opt(D(i), Dec(DesignElevation, 2)),
                CutFillAt(i).HasValue ? Signed(CutFillAt(i).Value, Dec(CutFill, 2)) : "",
                grade == null ? "" : Signed(grade.Grade * 100, Dec(Grade, 2)));
        }

        return new ProfileTableModel(stations, result, messages, export);
    }

    /// <summary>Grade lines PVI to PVI over the profile's extent (curves and tangent corners are PVIs).</summary>
    public static List<GradeLine> GradeLines(IEnumerable<ProfileSegment> segments)
    {
        var list = (segments ?? Enumerable.Empty<ProfileSegment>()).Where(s => s != null).ToList();
        var result = new List<GradeLine>();
        if (list.Count == 0) return result;

        var start = list.Min(s => s.StartStation);
        var end = list.Max(s => s.EndStation);
        var pvis = Profiles.VerticalCurve.FromSegments(list);
        if (pvis.Count == 0)
        {
            var tangent = list.FirstOrDefault(s => !s.IsCurve);
            if (tangent != null) result.Add(new GradeLine(start, end, tangent.Grade));
            return result;
        }

        var from = start;
        foreach (var p in pvis)
        {
            if (p.PviStation > from) result.Add(new GradeLine(from, p.PviStation, p.GradeIn));
            from = p.PviStation;
        }

        if (end > from) result.Add(new GradeLine(from, end, pvis[pvis.Count - 1].GradeOut));
        return result;
    }

    private static ProfileTableRow Cells(string key, TableRowSpec spec, string[] cells) =>
        new ProfileTableRow(key, spec.Label, ProfileTableRowKind.PerStation, cells, null);

    private static ProfileTableRow Spans(string key, TableRowSpec spec, List<ProfileTableSpan> spans) =>
        new ProfileTableRow(key, spec.Label, ProfileTableRowKind.Span, null, spans);

    /// <summary>Fixed decimals, half away from zero, dot separator.</summary>
    internal static string F(double v, int decimals) =>
        NumberFormat.Fixed(Math.Round(v, decimals, MidpointRounding.AwayFromZero), decimals);

    private static string Opt(double? v, int decimals) => v.HasValue ? F(v.Value, decimals) : "";

    /// <summary>"+1.25", "-0.50", "0.00".</summary>
    internal static string Signed(double v, int decimals)
    {
        var rounded = Math.Round(v, decimals, MidpointRounding.AwayFromZero);
        if (rounded == 0) return F(0, decimals);
        return (rounded > 0 ? "+" : "") + rounded.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }
}
