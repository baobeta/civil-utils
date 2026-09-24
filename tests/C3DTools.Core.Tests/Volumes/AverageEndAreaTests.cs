using System;
using C3DTools.Core.Volumes;
using Xunit;

namespace C3DTools.Core.Tests.Volumes;

public class AverageEndAreaTests
{
    [Fact]
    public void First_section_has_zero_volume()
    {
        var rows = AverageEndArea.Compute(new[] { new SectionAreas(0, 10, 2) });

        var row = Assert.Single(rows);
        Assert.Equal(0, row.Distance);
        Assert.Equal(0, row.CutVolume);
        Assert.Equal(0, row.FillVolume);
    }

    [Fact]
    public void Volume_is_mean_area_times_distance()
    {
        var rows = AverageEndArea.Compute(new[]
        {
            new SectionAreas(0, 10, 0),
            new SectionAreas(20, 20, 4),
        });

        Assert.Equal(20, rows[1].Distance, 9);
        Assert.Equal(300, rows[1].CutVolume, 9);  // (10 + 20) / 2 * 20
        Assert.Equal(40, rows[1].FillVolume, 9);  // (0 + 4) / 2 * 20
    }

    [Fact]
    public void Accumulates_volumes()
    {
        var rows = AverageEndArea.Compute(new[]
        {
            new SectionAreas(0, 10, 0),
            new SectionAreas(20, 20, 4),
            new SectionAreas(50, 0, 6),
        });

        Assert.Equal(300 + 300, rows[2].CumulativeCut, 9);  // (20 + 0) / 2 * 30 = 300
        Assert.Equal(40 + 150, rows[2].CumulativeFill, 9);  // (4 + 6) / 2 * 30 = 150
    }

    [Fact]
    public void Empty_input_gives_empty_output()
    {
        Assert.Empty(AverageEndArea.Compute(Array.Empty<SectionAreas>()));
    }

    [Fact]
    public void Rejects_stations_that_do_not_increase()
    {
        var ex = Assert.Throws<ArgumentException>(() => AverageEndArea.Compute(new[]
        {
            new SectionAreas(20, 1, 1),
            new SectionAreas(20, 1, 1),
        }));
        Assert.Contains("tăng dần", ex.Message);
    }

    [Fact]
    public void Rejects_negative_area()
    {
        Assert.Throws<ArgumentException>(() => AverageEndArea.Compute(new[] { new SectionAreas(0, -1, 0) }));
    }
}
