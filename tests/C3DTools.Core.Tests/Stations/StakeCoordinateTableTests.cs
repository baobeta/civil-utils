using C3DTools.Core.Presets;
using C3DTools.Core.Stations;
using Xunit;

namespace C3DTools.Core.Tests.Stations;

public class StakeCoordinateTableTests
{
    [Fact]
    public void Formats_rows_with_preset_decimals_and_station_text()
    {
        var table = StakeCoordinateTable.Build(new[]
        {
            new StakePoint("Km0", 0, 587123.45678, 1234567.8912, 12.346),
            new StakePoint("TĐ1", 1234.5, 587200.1, 1234600, null),
        }, new StakeTableOptions());

        Assert.Equal(new[] { "STT", "Tên cọc", "Lý trình", "X", "Y", "Z" }, table.Headers);
        Assert.Equal(new[] { "1", "Km0", "0+000.00", "587123.457", "1234567.891", "12.35" }, table.Rows[0]);
        Assert.Equal(new[] { "2", "TĐ1", "1+234.50", "587200.100", "1234600.000", "" }, table.Rows[1]);
    }

    [Fact]
    public void Z_column_is_omitted_when_no_stake_has_z()
    {
        var table = StakeCoordinateTable.Build(new[] { new StakePoint("C1", 20, 1, 2, null) }, new StakeTableOptions());

        Assert.Equal(new[] { "STT", "Tên cọc", "Lý trình", "X", "Y" }, table.Headers);
        Assert.Equal(new[] { "1", "C1", "0+020.00", "1.000", "2.000" }, table.Rows[0]);
    }

    [Fact]
    public void Z_column_is_omitted_when_the_preset_says_so()
    {
        var table = StakeCoordinateTable.Build(new[] { new StakePoint("C1", 20, 1, 2, 3) }, new StakeTableOptions { IncludeZ = false });

        Assert.Equal(5, table.Headers.Count);
    }

    [Fact]
    public void Uses_custom_decimals_and_invariant_culture()
    {
        TestCulture.Run("vi-VN", () =>
        {
            var options = new StakeTableOptions { XDecimals = 2, YDecimals = 1, ZDecimals = 3 };
            var table = StakeCoordinateTable.Build(new[] { new StakePoint("H1", 100.123, 10.006, 20.26, 1.23456) }, options, stationDecimals: 3);

            Assert.Equal(new[] { "1", "H1", "0+100.123", "10.01", "20.3", "1.235" }, table.Rows[0]);
        });
    }

    [Fact]
    public void Empty_list_gives_headers_only()
    {
        var table = StakeCoordinateTable.Build(new StakePoint[0], null);

        Assert.Equal(5, table.Headers.Count);
        Assert.Empty(table.Rows);
    }
}
