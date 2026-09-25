using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using C3DTools.Core.Presets;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Stations;

/// <summary>A stake located on the alignment: drawing X, Y and, when a surface gave one, Z.</summary>
public sealed class StakePoint
{
    public StakePoint(string name, double station, double x, double y, double? z)
    {
        Name = name ?? "";
        Station = station;
        X = x;
        Y = y;
        Z = z;
    }

    public string Name { get; }
    public double Station { get; }
    public double X { get; }
    public double Y { get; }
    public double? Z { get; }
}

/// <summary>CTTOADO: STT, Tên cọc, Lý trình (k+mmm.mm), X, Y (axis named in the header, see NorthingAsX) and Z (only when the preset wants it and some stake has one).</summary>
public static class StakeCoordinateTable
{
    public static TableData Build(IEnumerable<StakePoint> stakes, StakeTableOptions options, int stationDecimals = 2)
    {
        var list = (stakes ?? Enumerable.Empty<StakePoint>()).Where(s => s != null).ToList();
        options ??= new StakeTableOptions();
        var withZ = options.IncludeZ && list.Any(s => s.Z.HasValue);
        var northingAsX = options.NorthingAsX;
        string xHeader = XHeader(northingAsX), yHeader = YHeader(northingAsX);
        var table = withZ
            ? new TableData("STT", "Tên cọc", "Lý trình", xHeader, yHeader, "Z")
            : new TableData("STT", "Tên cọc", "Lý trình", xHeader, yHeader);
        var xDecimals = Clamp(options.XDecimals);
        var yDecimals = Clamp(options.YDecimals);
        var zDecimals = Clamp(options.ZDecimals);

        for (var i = 0; i < list.Count; i++)
        {
            var s = list[i];
            var number = (i + 1).ToString(CultureInfo.InvariantCulture);
            var station = StationFormatter.Format(s.Station, Clamp(stationDecimals), withKmPrefix: false);
            var easting = NumberFormat.Fixed(s.X, xDecimals);
            var northing = NumberFormat.Fixed(s.Y, yDecimals);
            var x = northingAsX ? northing : easting;
            var y = northingAsX ? easting : northing;
            if (withZ) table.AddRow(number, s.Name, station, x, y, s.Z.HasValue ? NumberFormat.Fixed(s.Z.Value, zDecimals) : "");
            else table.AddRow(number, s.Name, station, x, y);
        }

        return table;
    }

    /// <summary>Header of the X column: "X (Bắc)" when X is the northing, else "X (Đông)".</summary>
    public static string XHeader(bool northingAsX) => northingAsX ? "X (Bắc)" : "X (Đông)";

    public static string YHeader(bool northingAsX) => northingAsX ? "Y (Đông)" : "Y (Bắc)";

    private static int Clamp(int decimals) => Math.Max(0, Math.Min(6, decimals));
}
