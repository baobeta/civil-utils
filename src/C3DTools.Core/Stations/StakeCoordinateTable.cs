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

/// <summary>CTTOADO: STT, Tên cọc, Lý trình (k+mmm.mm), X, Y and Z (only when the preset wants it and some stake has one).</summary>
public static class StakeCoordinateTable
{
    public static TableData Build(IEnumerable<StakePoint> stakes, StakeTableOptions options, int stationDecimals = 2)
    {
        var list = (stakes ?? Enumerable.Empty<StakePoint>()).Where(s => s != null).ToList();
        options ??= new StakeTableOptions();
        var withZ = options.IncludeZ && list.Any(s => s.Z.HasValue);
        var table = withZ
            ? new TableData("STT", "Tên cọc", "Lý trình", "X", "Y", "Z")
            : new TableData("STT", "Tên cọc", "Lý trình", "X", "Y");
        var xDecimals = Clamp(options.XDecimals);
        var yDecimals = Clamp(options.YDecimals);
        var zDecimals = Clamp(options.ZDecimals);

        for (var i = 0; i < list.Count; i++)
        {
            var s = list[i];
            var number = (i + 1).ToString(CultureInfo.InvariantCulture);
            var station = StationFormatter.Format(s.Station, Clamp(stationDecimals), withKmPrefix: false);
            var x = NumberFormat.Fixed(s.X, xDecimals);
            var y = NumberFormat.Fixed(s.Y, yDecimals);
            if (withZ) table.AddRow(number, s.Name, station, x, y, s.Z.HasValue ? NumberFormat.Fixed(s.Z.Value, zDecimals) : "");
            else table.AddRow(number, s.Name, station, x, y);
        }

        return table;
    }

    private static int Clamp(int decimals) => Math.Max(0, Math.Min(6, decimals));
}
