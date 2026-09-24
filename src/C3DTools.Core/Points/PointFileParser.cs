using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace C3DTools.Core.Points;

/// <summary>Validates survey point files before they are imported as COGO points (C-02).</summary>
public static class PointFileParser
{
    public static PointFileResult Parse(IEnumerable<string> lines, PointFileOptions options)
    {
        if (lines == null) throw new ArgumentNullException(nameof(lines));
        if (options == null) throw new ArgumentNullException(nameof(options));
        var columns = ValidateColumns(options.Columns);

        var points = new List<SurveyPoint>();
        var issues = new List<PointIssue>();
        var firstLineByNumber = new Dictionary<uint, int>();
        var lineNo = 0;

        foreach (var raw in lines)
        {
            lineNo++;
            var line = raw?.Trim() ?? "";
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

            var fields = SplitFields(line, options.Delimiter, columns);
            if (fields == null)
            {
                issues.Add(new PointIssue(lineNo, IssueSeverity.Error, PointIssueCode.WrongColumnCount,
                    $"Dòng {lineNo}: cần {columns.Length} cột theo định dạng {options.Columns}."));
                continue;
            }

            uint? number = null;
            double? n = null, e = null, z = null;
            var description = "";
            var failed = false;

            for (var i = 0; i < columns.Length; i++)
            {
                var field = fields[i];
                switch (columns[i])
                {
                    case 'P':
                        if (uint.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)) number = parsed;
                        else issues.Add(NotANumber(lineNo, "số điểm", field));
                        failed |= number == null;
                        break;
                    case 'N':
                        n = ReadNumber(field, "N", lineNo, options.Delimiter, issues);
                        failed |= n == null;
                        break;
                    case 'E':
                        e = ReadNumber(field, "E", lineNo, options.Delimiter, issues);
                        failed |= e == null;
                        break;
                    case 'Z':
                        z = ReadNumber(field, "Z", lineNo, options.Delimiter, issues);
                        failed |= z == null;
                        break;
                    case 'D':
                        description = field;
                        break;
                }
            }

            if (failed) continue;

            if (firstLineByNumber.TryGetValue(number.Value, out var firstLine))
            {
                issues.Add(new PointIssue(lineNo, IssueSeverity.Error, PointIssueCode.DuplicateNumber,
                    $"Dòng {lineNo}: số điểm {number.Value} trùng với dòng {firstLine}."));
                continue;
            }
            firstLineByNumber[number.Value] = lineNo;

            if (InRange(e.Value, options.MinNorthing, options.MaxNorthing) &&
                InRange(n.Value, options.MinEasting, options.MaxEasting))
            {
                issues.Add(new PointIssue(lineNo, IssueSeverity.Warning, PointIssueCode.SwappedXY,
                    $"Dòng {lineNo}: N={Inv(n.Value)}, E={Inv(e.Value)} có vẻ bị đảo X/Y. Kiểm tra thứ tự cột."));
            }

            points.Add(new SurveyPoint
            {
                Number = number.Value,
                Northing = n.Value,
                Easting = e.Value,
                Elevation = z,
                Description = description,
                Line = lineNo,
            });
        }

        return new PointFileResult(points, issues);
    }

    private static char[] ValidateColumns(string spec)
    {
        var cols = (spec ?? "").ToUpperInvariant().ToCharArray();
        var valid = cols.All(c => "PNEZD".IndexOf(c) >= 0)
                    && cols.Distinct().Count() == cols.Length
                    && cols.Contains('P') && cols.Contains('N') && cols.Contains('E');
        if (!valid)
        {
            throw new ArgumentException(
                $"Định dạng cột không hợp lệ: '{spec}'. Dùng các ký tự P, N, E, Z, D; bắt buộc có P, N, E.",
                nameof(spec));
        }
        return cols;
    }

    private static string[] SplitFields(string line, char delimiter, char[] columns)
    {
        var endsWithDescription = columns[columns.Length - 1] == 'D';
        // A trailing description may itself contain the delimiter, so cap the split count.
        var parts = endsWithDescription
            ? line.Split(new[] { delimiter }, columns.Length)
            : line.Split(delimiter);

        if (endsWithDescription && parts.Length == columns.Length - 1)
            parts = parts.Concat(new[] { "" }).ToArray();

        return parts.Length == columns.Length ? parts.Select(p => p.Trim()).ToArray() : null;
    }

    private static double? ReadNumber(string field, string column, int lineNo, char delimiter, List<PointIssue> issues)
    {
        if (double.TryParse(field, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return value;

        if (delimiter != ',' && field.Contains(",") &&
            double.TryParse(field.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            issues.Add(new PointIssue(lineNo, IssueSeverity.Warning, PointIssueCode.CommaDecimal,
                $"Dòng {lineNo}: cột {column} dùng dấu phẩy thập phân ('{field}'), đã đọc là {Inv(value)}."));
            return value;
        }

        issues.Add(NotANumber(lineNo, "cột " + column, field));
        return null;
    }

    private static PointIssue NotANumber(int lineNo, string what, string field) =>
        new PointIssue(lineNo, IssueSeverity.Error, PointIssueCode.NotANumber,
            $"Dòng {lineNo}: {what} không phải số ('{field}').");

    private static bool InRange(double value, double min, double max) => value >= min && value <= max;

    private static string Inv(double value) => value.ToString(CultureInfo.InvariantCulture);
}
