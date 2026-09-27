using C3DTools.Core.Curves;
using C3DTools.Core.Stations;
using Xunit;

namespace C3DTools.Core.Tests.Stations;

public class StakeLabelLayoutTests
{
    private static readonly PlanPoint Origin = new PlanPoint(0, 0);
    private static readonly PlanPoint East = new PlanPoint(1, 0);

    [Fact]
    public void Names_alternate_left_and_right_of_the_route()
    {
        var first = StakeLabelLayout.Build(0, Origin, East, 20, "C1", 2, new StakeLabelOptions());
        var second = StakeLabelLayout.Build(1, new PlanPoint(20, 0), East, 40, "C2", 2, new StakeLabelOptions());
        var third = StakeLabelLayout.Build(2, new PlanPoint(40, 0), East, 60, "C3", 2, new StakeLabelOptions());

        Assert.True(first.NameTextPoint.Y > 0);    // left of a route heading east is +Y
        Assert.True(second.NameTextPoint.Y < 0);
        Assert.True(third.NameTextPoint.Y > 0);
        Assert.Equal(-first.NameTextPoint.Y, second.NameTextPoint.Y, 9);
        Assert.Equal("C2", second.NameText);
        Assert.True(first.TickEnd.Y > 0 && second.TickEnd.Y < 0);
    }

    [Fact]
    public void Without_alternating_every_name_is_on_the_left()
    {
        var options = new StakeLabelOptions { AlternateSides = false };

        Assert.True(StakeLabelLayout.Build(1, Origin, East, 40, "C2", 2, options).NameTextPoint.Y > 0);
    }

    [Fact]
    public void Station_is_written_only_when_asked()
    {
        Assert.Equal("", StakeLabelLayout.Build(0, Origin, East, 20, "C1", 2, new StakeLabelOptions()).StationText);
        Assert.Equal("0+020.00", StakeLabelLayout.Build(0, Origin, East, 20, "C1", 2, new StakeLabelOptions { WithStation = true }).StationText);
    }

    [Fact]
    public void Text_is_readable_on_both_sides()
    {
        foreach (var index in new[] { 0, 1 })
        {
            var rotation = StakeLabelLayout.Build(index, Origin, East, 20, "C1", 2, null).Rotation;
            Assert.InRange(rotation, -System.Math.PI / 2 - 1e-9, System.Math.PI / 2 + 1e-9);
        }
    }
}
