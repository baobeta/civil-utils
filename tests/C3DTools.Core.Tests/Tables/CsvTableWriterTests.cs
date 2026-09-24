using System;
using System.IO;
using System.Text;
using C3DTools.Core.Tables;
using Xunit;

namespace C3DTools.Core.Tests.Tables;

public class CsvTableWriterTests
{
    private static byte[] Write(TableData table)
    {
        using var stream = new MemoryStream();
        CsvTableWriter.Write(table, stream);
        return stream.ToArray();
    }

    [Fact]
    public void Writes_utf8_with_bom_and_crlf()
    {
        var table = new TableData("Cọc", "Cao độ");
        table.AddRow("Km0", "1.234");

        var bytes = Write(table);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal("Cọc,Cao độ\r\nKm0,1.234\r\n", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }

    [Fact]
    public void Quotes_cells_with_delimiter_quote_or_newline()
    {
        var table = new TableData("A", "B", "C");
        table.AddRow("2,5", "say \"hi\"", "x\ny");

        var text = Encoding.UTF8.GetString(Write(table)[3..]);

        Assert.Equal("A,B,C\r\n\"2,5\",\"say \"\"hi\"\"\",\"x\ny\"\r\n", text);
    }

    [Fact]
    public void Rejects_row_with_wrong_cell_count()
    {
        var table = new TableData("A", "B");
        Assert.Throws<ArgumentException>(() => table.AddRow("only one"));
    }

    [Fact]
    public void Leaves_stream_open()
    {
        using var stream = new MemoryStream();
        CsvTableWriter.Write(new TableData("A"), stream);
        Assert.True(stream.CanWrite);
    }

    [Fact]
    public void NumberFormat_uses_dot_on_vietnamese_windows()
    {
        TestCulture.Run("vi-VN", () => Assert.Equal("1.50", NumberFormat.Fixed(1.5, 2)));
    }
}
