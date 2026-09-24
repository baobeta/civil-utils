using System;
using System.Linq;
using C3DTools.Core.Points;
using Xunit;

namespace C3DTools.Core.Tests.Points;

public class PointFileParserTests
{
    private static PointFileResult Parse(PointFileOptions options, params string[] lines) =>
        PointFileParser.Parse(lines, options);

    private static PointFileResult Parse(params string[] lines) => Parse(new PointFileOptions(), lines);

    [Fact]
    public void Reads_valid_pnezd_line()
    {
        var result = Parse("1,1200000.123,550000.456,5.25,MOC");

        Assert.Empty(result.Issues);
        var p = Assert.Single(result.Points);
        Assert.Equal(1u, p.Number);
        Assert.Equal(1200000.123, p.Northing, 6);
        Assert.Equal(550000.456, p.Easting, 6);
        Assert.Equal(5.25, p.Elevation.Value, 6);
        Assert.Equal("MOC", p.Description);
        Assert.Equal(1, p.Line);
    }

    [Fact]
    public void Flags_duplicate_point_number_and_keeps_first()
    {
        var result = Parse("1,1200000,550000,5,A", "1,1200001,550001,5,B");

        var p = Assert.Single(result.Points);
        Assert.Equal("A", p.Description);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(PointIssueCode.DuplicateNumber, issue.Code);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal(2, issue.Line);
        Assert.Contains("dòng 1", issue.Message);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void Accepts_comma_decimals_with_warning_when_delimiter_is_not_comma()
    {
        var result = Parse(new PointFileOptions { Delimiter = ';' }, "5;1200000,5;550000,25;3,1;CAY");

        var p = Assert.Single(result.Points);
        Assert.Equal(1200000.5, p.Northing, 6);
        Assert.Equal(550000.25, p.Easting, 6);
        Assert.Equal(3.1, p.Elevation.Value, 6);
        Assert.Equal(3, result.Issues.Count(i => i.Code == PointIssueCode.CommaDecimal));
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Rejects_non_numeric_coordinate()
    {
        var result = Parse("7,abc,550000,5,X");

        Assert.Empty(result.Points);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(PointIssueCode.NotANumber, issue.Code);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
    }

    [Fact]
    public void Warns_when_northing_and_easting_look_swapped()
    {
        var result = Parse("8,550000,1200000,5,X");

        Assert.Single(result.Points);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(PointIssueCode.SwappedXY, issue.Code);
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void Rejects_line_with_too_few_columns()
    {
        var result = Parse("9,1200000");

        Assert.Empty(result.Points);
        Assert.Equal(PointIssueCode.WrongColumnCount, Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Keeps_delimiter_inside_trailing_description()
    {
        var result = Parse("10,1200000,550000,5,COT DIEN, BE TONG");

        Assert.Equal("COT DIEN, BE TONG", Assert.Single(result.Points).Description);
    }

    [Fact]
    public void Allows_missing_trailing_description()
    {
        var result = Parse("11,1200000,550000,5");

        Assert.Empty(result.Issues);
        Assert.Equal("", Assert.Single(result.Points).Description);
    }

    [Fact]
    public void Skips_blank_and_comment_lines_but_keeps_file_line_numbers()
    {
        var result = Parse("# so hieu,N,E,Z,mo ta", "", "12,1200000,550000,5,A", "x");

        Assert.Single(result.Points);
        Assert.Equal(4, Assert.Single(result.Issues).Line);
    }

    [Fact]
    public void Supports_other_column_orders()
    {
        var result = Parse(new PointFileOptions { Columns = "PENZ" }, "3,550000,1200000,4");

        var p = Assert.Single(result.Points);
        Assert.Equal(1200000, p.Northing, 6);
        Assert.Equal(550000, p.Easting, 6);
        Assert.Empty(result.Issues);
    }

    [Theory]
    [InlineData("PNX")]
    [InlineData("PNNE")]
    [InlineData("NEZ")]
    public void Rejects_invalid_column_spec(string columns)
    {
        Assert.Throws<ArgumentException>(() => Parse(new PointFileOptions { Columns = columns }, "1,2,3"));
    }

    [Fact]
    public void Parses_with_invariant_culture_on_vietnamese_windows()
    {
        TestCulture.Run("vi-VN", () =>
        {
            var result = Parse("1,1200000.123,550000.456,5.25,MOC");
            Assert.Empty(result.Issues);
            Assert.Equal(1200000.123, Assert.Single(result.Points).Northing, 6);
        });
    }
}
