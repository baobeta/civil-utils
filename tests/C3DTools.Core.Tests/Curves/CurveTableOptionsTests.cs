using System.Collections.Generic;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class CurveTableOptionsTests
{
    private static List<string> Watch(CurveTableOptions o)
    {
        var names = new List<string>();
        o.PropertyChanged += (s, e) => names.Add(e.PropertyName);
        return names;
    }

    [Fact]
    public void Defaults_table_and_csv_without_a_route()
    {
        var o = new CurveTableOptions();

        Assert.True(o.WriteTable);
        Assert.True(o.WriteCsv);
        Assert.False(o.WriteXlsx);
        Assert.False(o.CanApply);
        Assert.Equal("Chưa chọn tuyến", o.SummaryText);
    }

    [Fact]
    public void A_route_with_curves_can_be_applied()
    {
        var o = new CurveTableOptions();
        var names = Watch(o);

        o.CurveCount = 3;

        Assert.True(o.CanApply);
        Assert.Equal("3 đường cong", o.SummaryText);
        Assert.Equal(new[] { "CurveCount", "CanApply", "SummaryText" }, names);
    }

    [Fact]
    public void A_route_without_curves_cannot()
    {
        var o = new CurveTableOptions { CurveCount = 0 };

        Assert.False(o.CanApply);
        Assert.Equal("Tuyến không có đường cong nào", o.SummaryText);
    }

    [Fact]
    public void Needs_at_least_one_output()
    {
        var o = new CurveTableOptions { CurveCount = 2 };
        var names = Watch(o);

        o.WriteTable = false;
        o.WriteCsv = false;
        Assert.False(o.CanApply);
        o.WriteXlsx = true;
        Assert.True(o.CanApply);

        Assert.Equal(new[] { "WriteTable", "CanApply", "WriteCsv", "CanApply", "WriteXlsx", "CanApply" }, names);
    }
}
