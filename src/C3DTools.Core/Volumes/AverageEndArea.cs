using System;
using System.Collections.Generic;
using System.Globalization;

namespace C3DTools.Core.Volumes;

public sealed class SectionAreas
{
    public SectionAreas(double station, double cutArea, double fillArea)
    {
        Station = station;
        CutArea = cutArea;
        FillArea = fillArea;
    }

    public double Station { get; }
    public double CutArea { get; }
    public double FillArea { get; }
}

public sealed class VolumeRow
{
    public double Station { get; set; }
    public double CutArea { get; set; }
    public double FillArea { get; set; }
    public double Distance { get; set; }
    public double CutVolume { get; set; }
    public double FillVolume { get; set; }
    public double CumulativeCut { get; set; }
    public double CumulativeFill { get; set; }
}

/// <summary>Cut/fill volumes between consecutive sections by the average end area method.</summary>
public static class AverageEndArea
{
    public static IReadOnlyList<VolumeRow> Compute(IReadOnlyList<SectionAreas> sections)
    {
        if (sections == null) throw new ArgumentNullException(nameof(sections));

        var rows = new List<VolumeRow>(sections.Count);
        VolumeRow previous = null;

        foreach (var s in sections)
        {
            if (s.CutArea < 0 || s.FillArea < 0)
                throw new ArgumentException($"Diện tích âm tại lý trình {Inv(s.Station)}.", nameof(sections));
            if (previous != null && s.Station <= previous.Station)
                throw new ArgumentException(
                    $"Lý trình phải tăng dần: {Inv(s.Station)} đứng sau {Inv(previous.Station)}.", nameof(sections));

            var row = new VolumeRow { Station = s.Station, CutArea = s.CutArea, FillArea = s.FillArea };
            if (previous != null)
            {
                row.Distance = s.Station - previous.Station;
                row.CutVolume = (previous.CutArea + s.CutArea) / 2.0 * row.Distance;
                row.FillVolume = (previous.FillArea + s.FillArea) / 2.0 * row.Distance;
                row.CumulativeCut = previous.CumulativeCut + row.CutVolume;
                row.CumulativeFill = previous.CumulativeFill + row.FillVolume;
            }

            rows.Add(row);
            previous = row;
        }

        return rows;
    }

    private static string Inv(double value) => value.ToString(CultureInfo.InvariantCulture);
}
