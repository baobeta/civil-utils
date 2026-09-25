using System;
using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Presets;
using C3DTools.Core.Profiles;
using C3DTools.Core.Stations;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Sections;

public enum SectionTableRowKind
{
    /// <summary>One cell per offset.</summary>
    PerOffset,

    /// <summary>Merged cells: between offsets (khoảng cách lẻ) or across the whole table (diện tích).</summary>
    Span,
}

public sealed class SectionTableRow
{
    public SectionTableRow(string key, string label, int decimals, SectionTableRowKind kind, IReadOnlyList<string> cells, IReadOnlyList<ProfileTableSpan> spans)
    {
        Key = key;
        Label = label ?? "";
        Decimals = decimals;
        Kind = kind;
        Cells = cells ?? new string[0];
        Spans = spans ?? new ProfileTableSpan[0];
    }

    public string Key { get; }
    public string Label { get; }
    public int Decimals { get; }
    public SectionTableRowKind Kind { get; }

    /// <summary>PerOffset: one text per offset (empty when unknown). Span: empty.</summary>
    public IReadOnlyList<string> Cells { get; }

    /// <summary>Span: merged cells, From/To are offsets. PerOffset: empty.</summary>
    public IReadOnlyList<ProfileTableSpan> Spans { get; }
}

/// <summary>Cut (đào: design below ground) and fill (đắp: design above ground) areas between two section lines.</summary>
public sealed class SectionCutFill
{
    public SectionCutFill(double cut, double fill, int loops)
    {
        Cut = cut;
        Fill = fill;
        Loops = loops;
    }

    public double Cut { get; }
    public double Fill { get; }

    /// <summary>Closed regions between the lines (each wholly cut or wholly fill).</summary>
    public int Loops { get; }
}

/// <summary>The data table of one section: offsets as columns, rows as the preset orders them, and the section's areas.</summary>
public sealed class SectionTableModel
{
    public SectionTableModel(string name, double station, IReadOnlyList<double> offsets, IReadOnlyList<SectionTableRow> rows,
        IReadOnlyList<string> messages, SectionCutFill areas, double? centreDifference)
    {
        Name = name ?? "";
        Station = station;
        Offsets = offsets;
        Rows = rows;
        Messages = messages;
        Areas = areas;
        CentreDifference = centreDifference;
    }

    /// <summary>Sample line name.</summary>
    public string Name { get; }

    public double Station { get; }

    /// <summary>Columns, left to right.</summary>
    public IReadOnlyList<double> Offsets { get; }

    public IReadOnlyList<SectionTableRow> Rows { get; }

    /// <summary>Rows that were left out (unknown keys).</summary>
    public IReadOnlyList<string> Messages { get; }

    /// <summary>Zero when either line is missing.</summary>
    public SectionCutFill Areas { get; }

    /// <summary>Design − ground at offset 0 (chênh cao tim); null when either is missing there.</summary>
    public double? CentreDifference { get; }
}

/// <summary>
/// CTTRACNGANG: the table under one section view from its ground (surface) line and design line (null = none).
/// Columns are the union of both lines' vertex offsets; each line's elevation there is interpolated (empty outside it).
/// Areas: the region between the lines is split where they cross; each closed loop's area is the shoelace sum;
/// đào where the design is below the ground, đắp where it is above.
/// </summary>
public static class SectionTableBuilder
{
    public const string Offset = "Offset";
    public const string GroundElevation = "GroundElevation";
    public const string DesignElevation = "DesignElevation";
    public const string PartialDistance = "PartialDistance";
    public const string CutArea = "CutArea";
    public const string FillArea = "FillArea";

    /// <summary>Offsets closer than this (m) are one column.</summary>
    public const double OffsetTolerance = 1e-4;

    public static readonly string[] ExportHeaders = { "Tên cọc", "Lý trình", "Diện tích đào", "Diện tích đắp", "Chênh cao tim" };

    /// <summary>Every supported row with its default label and decimals, in the usual order.</summary>
    public static IReadOnlyList<TableRowSpec> KnownRows() => new List<TableRowSpec>
    {
        new TableRowSpec(GroundElevation, "Cao độ tự nhiên", 2),
        new TableRowSpec(DesignElevation, "Cao độ thiết kế", 2),
        new TableRowSpec(PartialDistance, "Khoảng cách lẻ", 2),
        new TableRowSpec(Offset, "Khoảng cách tim", 2),
        new TableRowSpec(CutArea, "Diện tích đào", 2),
        new TableRowSpec(FillArea, "Diện tích đắp", 2),
    };

    /// <summary>The supported key for a preset key (case-insensitive, "Distance" accepted); null when unknown.</summary>
    public static string Canonical(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var k = key.Trim();
        if (string.Equals(k, "Distance", StringComparison.OrdinalIgnoreCase)) return PartialDistance;
        return KnownRows().Select(r => r.Key).FirstOrDefault(r => string.Equals(r, k, StringComparison.OrdinalIgnoreCase));
    }

    public static SectionTableModel Build(string name, double station, SectionProfile ground, SectionProfile design, IEnumerable<TableRowSpec> rows)
    {
        var offsets = Offsets(ground, design);
        var n = offsets.Count;
        var areas = Areas(ground, design);
        var result = new List<SectionTableRow>();
        var messages = new List<string>();
        foreach (var spec in rows ?? Enumerable.Empty<TableRowSpec>())
        {
            if (spec == null) continue;
            var key = Canonical(spec.Key);
            var d = Math.Max(0, Math.Min(6, spec.Decimals));
            string[] PerOffset(Func<double, string> cell) => offsets.Select(cell).ToArray();
            switch (key)
            {
                case GroundElevation:
                    result.Add(Cells(key, spec, d, PerOffset(o => Opt(ground?.ElevationAt(o), d))));
                    break;
                case DesignElevation:
                    result.Add(Cells(key, spec, d, PerOffset(o => Opt(design?.ElevationAt(o), d))));
                    break;
                case Offset:
                    result.Add(Cells(key, spec, d, PerOffset(o => ProfileTableBuilder.F(Math.Abs(o), d))));
                    break;
                case PartialDistance:
                    var gaps = new List<ProfileTableSpan>();
                    for (var i = 1; i < n; i++)
                        gaps.Add(new ProfileTableSpan(offsets[i - 1], offsets[i], ProfileTableBuilder.F(offsets[i] - offsets[i - 1], d)));
                    result.Add(Spans(key, spec, d, gaps));
                    break;
                case CutArea:
                case FillArea:
                    var whole = new List<ProfileTableSpan>();
                    if (n > 1) whole.Add(new ProfileTableSpan(offsets[0], offsets[n - 1], ProfileTableBuilder.F(key == CutArea ? areas.Cut : areas.Fill, d)));
                    result.Add(Spans(key, spec, d, whole));
                    break;
                default:
                    messages.Add($"Bảng trắc ngang: bỏ qua dòng \"{spec.Key}\" ({spec.Label}), preset có khoá chưa hỗ trợ.");
                    break;
            }
        }

        var g0 = ground?.ElevationAt(0);
        var d0 = design?.ElevationAt(0);
        return new SectionTableModel(name, station, offsets, result, messages, areas, g0.HasValue && d0.HasValue ? d0 - g0 : null);
    }

    /// <summary>One row per section in station order (CSV / Excel): Tên cọc, Lý trình, Diện tích đào, Diện tích đắp, Chênh cao tim.</summary>
    public static TableData Export(IEnumerable<SectionTableModel> sections, int stationDecimals = 2, int areaDecimals = 2, int elevationDecimals = 2)
    {
        var table = new TableData((string[])ExportHeaders.Clone());
        foreach (var s in (sections ?? Enumerable.Empty<SectionTableModel>()).Where(s => s != null).OrderBy(s => s.Station))
        {
            table.AddRow(
                s.Name,
                StationFormatter.Format(s.Station, stationDecimals, withKmPrefix: false),
                ProfileTableBuilder.F(s.Areas.Cut, areaDecimals),
                ProfileTableBuilder.F(s.Areas.Fill, areaDecimals),
                s.CentreDifference.HasValue ? ProfileTableBuilder.Signed(s.CentreDifference.Value, elevationDecimals) : "");
        }

        return table;
    }

    /// <summary>Union of both lines' vertex offsets, sorted, near-duplicates merged.</summary>
    public static List<double> Offsets(SectionProfile ground, SectionProfile design)
    {
        var all = new List<double>();
        foreach (var p in new[] { ground, design }.Where(p => p != null)) all.AddRange(p.Points.Select(v => v.Offset));
        all.Sort();
        var result = new List<double>();
        foreach (var o in all)
        {
            if (result.Count > 0 && o - result[result.Count - 1] <= OffsetTolerance) continue;
            result.Add(o);
        }

        return result;
    }

    /// <summary>
    /// Cut/fill between the lines over the offsets both cover. Pieces between consecutive breakpoints (vertices of
    /// either line and crossings) are linear in both lines; consecutive pieces with the same sign form one closed loop
    /// (ground forward, design back), whose area is taken by the shoelace formula.
    /// </summary>
    public static SectionCutFill Areas(SectionProfile ground, SectionProfile design)
    {
        if (ground == null || design == null || !ground.IsUsable || !design.IsUsable) return new SectionCutFill(0, 0, 0);
        var lo = Math.Max(ground.MinOffset, design.MinOffset);
        var hi = Math.Min(ground.MaxOffset, design.MaxOffset);
        if (hi - lo <= SectionProfile.Tolerance) return new SectionCutFill(0, 0, 0);

        var xs = new List<double> { lo, hi };
        xs.AddRange(ground.Points.Select(p => p.Offset).Concat(design.Points.Select(p => p.Offset)).Where(x => x > lo && x < hi));
        xs.Sort();

        double cut = 0, fill = 0;
        var loops = 0;
        var top = new List<(double X, double Z)>();
        var bottom = new List<(double X, double Z)>();
        var sign = 0;

        void Close()
        {
            if (sign != 0 && top.Count > 0)
            {
                var ring = new List<(double X, double Z)>(top);
                for (var i = bottom.Count - 1; i >= 0; i--) ring.Add(bottom[i]);
                var area = Math.Abs(Shoelace(ring));
                if (sign < 0) cut += area;
                else fill += area;
                loops++;
            }

            top.Clear();
            bottom.Clear();
            sign = 0;
        }

        void Piece(double a, double b, double ga, double gb, double da, double db)
        {
            var mid = (da - ga) + (db - gb);
            var s = Math.Abs(da - ga) + Math.Abs(db - gb) <= 1e-9 ? 0 : Math.Sign(mid);
            if (s != sign) Close();
            sign = s;
            if (s == 0) return;
            top.Add((a, ga));
            top.Add((b, gb));
            bottom.Add((a, da));
            bottom.Add((b, db));
        }

        for (var i = 1; i < xs.Count; i++)
        {
            double a = xs[i - 1], b = xs[i];
            if (b - a <= SectionProfile.Tolerance) continue;
            var g = ground.AcrossInterval(a, b);
            var d = design.AcrossInterval(a, b);
            if (g == null || d == null) continue;
            double ga = g.Value.AtFrom, gb = g.Value.AtTo, da = d.Value.AtFrom, db = d.Value.AtTo;
            double fa = da - ga, fb = db - gb;
            if (fa * fb < 0)
            {
                // The lines cross inside the piece: split there so each part has one sign.
                var t = fa / (fa - fb);
                var x = a + t * (b - a);
                var z = ga + t * (gb - ga);
                Piece(a, x, ga, z, da, z);
                Piece(x, b, z, gb, z, db);
            }
            else
            {
                Piece(a, b, ga, gb, da, db);
            }
        }

        Close();
        return new SectionCutFill(cut, fill, loops);
    }

    /// <summary>Signed area of a closed ring (last vertex joins the first).</summary>
    public static double Shoelace(IReadOnlyList<(double X, double Z)> ring)
    {
        double sum = 0;
        for (var i = 0; i < ring.Count; i++)
        {
            var p = ring[i];
            var q = ring[(i + 1) % ring.Count];
            sum += p.X * q.Z - q.X * p.Z;
        }

        return sum / 2;
    }

    private static SectionTableRow Cells(string key, TableRowSpec spec, int d, string[] cells) =>
        new SectionTableRow(key, spec.Label, d, SectionTableRowKind.PerOffset, cells, null);

    private static SectionTableRow Spans(string key, TableRowSpec spec, int d, List<ProfileTableSpan> spans) =>
        new SectionTableRow(key, spec.Label, d, SectionTableRowKind.Span, null, spans);

    private static string Opt(double? v, int decimals) => v.HasValue ? ProfileTableBuilder.F(v.Value, decimals) : "";
}
