using System;
using System.IO;
using System.Text;
using C3DTools.Core.Tables;
using Xunit;

namespace C3DTools.Core.Tests.Tables;

public class TableExportTests
{
    [Fact]
    public void Suggests_a_path_next_to_the_drawing()
    {
        var drawing = Path.Combine("Du an", "Tuyen A.dwg");

        Assert.Equal(Path.Combine("Du an", "Tuyen A_TOADO.csv"), TableExport.SuggestPath(drawing, "TOADO", "csv"));
    }

    [Fact]
    public void Suggested_extension_may_start_with_a_dot()
    {
        Assert.Equal(Path.Combine("d", "t_BANG.xlsx"), TableExport.SuggestPath(Path.Combine("d", "t.dwg"), "BANG", ".xlsx"));
    }

    [Fact]
    public void Suggests_a_bare_name_without_a_folder()
    {
        Assert.Equal("t_BANG.csv", TableExport.SuggestPath("t.dwg", "BANG", "csv"));
    }

    [Fact]
    public void Writes_csv_like_the_csv_writer()
    {
        var table = new TableData("Cọc", "X");
        table.AddRow("Km0", "1.5");
        var path = Path.Combine(Path.GetTempPath(), "c3dtools-export-" + Guid.NewGuid().ToString("N") + ".csv");
        try
        {
            TableExport.WriteCsv(table, path);

            var bytes = File.ReadAllBytes(path);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
            Assert.Equal("Cọc,X\r\nKm0,1.5\r\n", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Xlsx_is_not_available_yet()
    {
        var ex = Assert.Throws<NotSupportedException>(() => TableExport.WriteXlsx(new TableData("A"), "x.xlsx", "Bang"));
        Assert.Equal("Xuất Excel sẽ có ở bước sau", ex.Message);
    }
}
