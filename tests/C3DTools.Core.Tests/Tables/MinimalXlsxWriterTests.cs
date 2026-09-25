using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using C3DTools.Core.Tables;
using Xunit;

namespace C3DTools.Core.Tests.Tables;

public class MinimalXlsxWriterTests
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static TableData Sample()
    {
        var table = new TableData("Cọc", "Lý trình", "X", "Ghi chú");
        table.AddRow("Km0", "0+000.00", "12.5", "Đường cong");
        table.AddRow("H1", "1+234.56", "-3", "Tiêu chuẩn");
        return table;
    }

    private static ZipArchive Write(TableData table, string sheetName = "Bang")
    {
        var stream = new MemoryStream();
        MinimalXlsxWriter.Write(table, stream, sheetName);
        stream.Position = 0;
        return new ZipArchive(stream, ZipArchiveMode.Read);
    }

    private static XDocument Part(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name);
        Assert.NotNull(entry);
        using var s = entry.Open();
        return XDocument.Load(s);
    }

    private static XElement Cell(XDocument sheet, string reference) =>
        sheet.Descendants(Main + "c").Single(c => (string)c.Attribute("r") == reference);

    private static string InlineText(XElement cell) => (string)cell.Element(Main + "is")?.Element(Main + "t");

    [Fact]
    public void Zip_contains_the_six_parts()
    {
        using var zip = Write(Sample());

        var names = zip.Entries.Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[]
        {
            "[Content_Types].xml", "_rels/.rels", "xl/_rels/workbook.xml.rels", "xl/styles.xml", "xl/workbook.xml", "xl/worksheets/sheet1.xml",
        }, names);
    }

    [Fact]
    public void Sheet_has_one_row_per_table_row_plus_header()
    {
        using var zip = Write(Sample());

        var rows = Part(zip, "xl/worksheets/sheet1.xml").Descendants(Main + "row").ToList();
        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { "1", "2", "3" }, rows.Select(r => (string)r.Attribute("r")));
        Assert.All(rows, r => Assert.Equal(4, r.Elements(Main + "c").Count()));
    }

    [Fact]
    public void Numbers_become_numeric_cells()
    {
        using var zip = Write(Sample());
        var sheet = Part(zip, "xl/worksheets/sheet1.xml");

        var x = Cell(sheet, "C2");
        Assert.Equal("n", (string)x.Attribute("t"));
        Assert.Equal("12.5", (string)x.Element(Main + "v"));
        Assert.Equal("-3", (string)Cell(sheet, "C3").Element(Main + "v"));
    }

    [Fact]
    public void Station_text_stays_a_string()
    {
        using var zip = Write(Sample());
        var cell = Cell(Part(zip, "xl/worksheets/sheet1.xml"), "B3");

        Assert.Equal("inlineStr", (string)cell.Attribute("t"));
        Assert.Equal("1+234.56", InlineText(cell));
    }

    [Theory]
    [InlineData("12.5", true)]
    [InlineData("-0.25", true)]
    [InlineData("+7", false)]
    [InlineData("007", false)]
    [InlineData("-007", false)]
    [InlineData("0", true)]
    [InlineData("0.5", true)]
    [InlineData("-0.5", true)]
    [InlineData("123456789012345", true)]
    [InlineData("1234567890123456", false)]
    [InlineData("0.0000123456789012345", true)]
    [InlineData("1234567890.123456", false)]
    [InlineData("1,234.5", false)]
    [InlineData("12,5", false)]
    [InlineData("1+234.56", false)]
    [InlineData(" 12", false)]
    [InlineData("1E5", false)]
    [InlineData("NaN", false)]
    [InlineData("", false)]
    public void Only_plain_invariant_numbers_are_numeric(string text, bool numeric)
    {
        Assert.Equal(numeric, MinimalXlsxWriter.TryNumber(text, out _));
    }

    [Fact]
    public void Numbers_are_numeric_under_a_comma_culture()
    {
        TestCulture.Run("vi-VN", () =>
        {
            using var zip = Write(Sample());
            Assert.Equal("12.5", (string)Cell(Part(zip, "xl/worksheets/sheet1.xml"), "C2").Element(Main + "v"));
        });
    }

    [Fact]
    public void Vietnamese_text_survives_as_utf8()
    {
        using var zip = Write(Sample());
        var entry = zip.GetEntry("xl/worksheets/sheet1.xml");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        var xml = reader.ReadToEnd();

        Assert.Contains("Đường cong", xml);
        Assert.Contains("Tiêu chuẩn", xml);
        Assert.Equal("Cọc", InlineText(Cell(XDocument.Parse(xml), "A1")));
    }

    [Fact]
    public void Header_is_bold_and_frozen()
    {
        using var zip = Write(Sample());
        var sheet = Part(zip, "xl/worksheets/sheet1.xml");
        var styles = Part(zip, "xl/styles.xml");

        var pane = sheet.Descendants(Main + "pane").Single();
        Assert.Equal("1", (string)pane.Attribute("ySplit"));
        Assert.Equal("A2", (string)pane.Attribute("topLeftCell"));
        Assert.Equal("frozen", (string)pane.Attribute("state"));

        var headerStyle = int.Parse((string)Cell(sheet, "A1").Attribute("s"));
        var xf = styles.Descendants(Main + "cellXfs").Single().Elements(Main + "xf").ElementAt(headerStyle);
        var font = styles.Descendants(Main + "fonts").Single().Elements(Main + "font").ElementAt(int.Parse((string)xf.Attribute("fontId")));
        Assert.NotNull(font.Element(Main + "b"));
        var border = styles.Descendants(Main + "borders").Single().Elements(Main + "border").ElementAt(int.Parse((string)xf.Attribute("borderId")));
        Assert.Equal("thin", (string)border.Element(Main + "left").Attribute("style"));
    }

    [Fact]
    public void Column_widths_follow_the_longest_text_within_limits()
    {
        var table = new TableData("A", "B", "C");
        table.AddRow("x", "1234567890123456789", new string('y', 200));
        using var zip = Write(table);

        var widths = Part(zip, "xl/worksheets/sheet1.xml").Descendants(Main + "col")
            .Select(c => double.Parse((string)c.Attribute("width"), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(new double[] { 8, 21, 60 }, widths);
    }

    [Theory]
    [InlineData("Bang", "Bang")]
    [InlineData("Yếu tố cong: [T1]/2", "Yếu tố cong_ _T1__2")]
    [InlineData("", "Sheet1")]
    [InlineData(null, "Sheet1")]
    [InlineData("'x'", "x")]
    [InlineData("1234567890123456789012345678901234567890", "1234567890123456789012345678901")]
    public void Sheet_name_is_sanitised(string name, string expected)
    {
        Assert.Equal(expected, MinimalXlsxWriter.SheetName(name));
    }

    [Fact]
    public void Workbook_uses_the_sanitised_sheet_name()
    {
        using var zip = Write(Sample(), "Tuyến/A");

        var sheet = Part(zip, "xl/workbook.xml").Descendants(Main + "sheet").Single();
        Assert.Equal("Tuyến_A", (string)sheet.Attribute("name"));
    }

    [Fact]
    public void Characters_invalid_in_xml_are_dropped()
    {
        var table = new TableData("A");
        table.AddRow("a\u0001b");
        using var zip = Write(table);

        Assert.Equal("ab", InlineText(Cell(Part(zip, "xl/worksheets/sheet1.xml"), "A2")));
    }

    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(701, "ZZ")]
    [InlineData(702, "AAA")]
    public void Column_letters(int index, string letters)
    {
        Assert.Equal(letters, MinimalXlsxWriter.ColumnName(index));
    }

    [Fact]
    public void TableExport_writes_the_xlsx_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "c3dtools-xlsx-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            TableExport.WriteXlsx(Sample(), path, "Bang");

            using var zip = ZipFile.OpenRead(path);
            Assert.NotNull(zip.GetEntry("xl/worksheets/sheet1.xml"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [SofficeFact]
    public void LibreOffice_reads_the_same_cells()
    {
        var dir = Path.Combine(Path.GetTempPath(), "c3dtools-soffice-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var xlsx = Path.Combine(dir, "bang.xlsx");
            TableExport.WriteXlsx(Sample(), xlsx, "Bang");
            var info = new ProcessStartInfo("soffice",
                $"--headless -env:UserInstallation=file://{dir.Replace('\\', '/')}/profile --convert-to \"csv:Text - txt - csv (StarCalc):44,34,76\" --outdir \"{dir}\" \"{xlsx}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using (var process = Process.Start(info))
            {
                Assert.True(process.WaitForExit(120000), "soffice timed out");
            }

            var lines = File.ReadAllLines(Path.Combine(dir, "bang.csv"), Encoding.UTF8);
            Assert.Equal(new[]
            {
                "Cọc,Lý trình,X,Ghi chú",
                "Km0,0+000.00,12.5,Đường cong",
                "H1,1+234.56,-3,Tiêu chuẩn",
            }, lines.Select(l => l.TrimStart('﻿')));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}

/// <summary>A Fact that is skipped when LibreOffice (soffice) is not on PATH, e.g. on a developer Mac.</summary>
public sealed class SofficeFactAttribute : FactAttribute
{
    public SofficeFactAttribute()
    {
        if (!OnPath("soffice")) Skip = "soffice không có trên PATH";
    }

    private static bool OnPath(string program)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var names = Environment.OSVersion.Platform == PlatformID.Win32NT ? new[] { program + ".exe", program + ".com" } : new[] { program };
        return path.Split(Path.PathSeparator).Where(d => d.Length > 0).Any(d => names.Any(n => File.Exists(Path.Combine(d, n))));
    }
}
