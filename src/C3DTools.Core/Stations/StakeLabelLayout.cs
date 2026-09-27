using System;
using C3DTools.Core.Curves;

namespace C3DTools.Core.Stations;

/// <summary>How the stake names are drawn on the plan ("Ghi tên cọc lên bình đồ").</summary>
public sealed class StakeLabelOptions
{
    /// <summary>"Tên cọc xen kẽ trái phải": stake 1 on the left of the route, stake 2 on the right, and so on.</summary>
    public bool AlternateSides { get; set; } = true;

    /// <summary>"Ghi kèm lý trình": the station next to the name. Off by default: alignment labels usually show it.</summary>
    public bool WithStation { get; set; }
}

/// <summary>Where one stake label goes: a short tick across the route and the texts beside it, on one side of the route.</summary>
public static class StakeLabelLayout
{
    /// <param name="index">0-based position among the stakes that get a label; decides the side when alternating.</param>
    /// <param name="point">The stake on the centreline.</param>
    /// <param name="direction">Unit direction of travel there.</param>
    public static StakeDrawing Build(int index, PlanPoint point, PlanPoint direction, double station, string name, double textHeight, StakeLabelOptions options)
    {
        if (!(textHeight > 0)) throw new ArgumentOutOfRangeException(nameof(textHeight));
        options ??= new StakeLabelOptions();
        var side = options.AlternateSides && index % 2 == 1 ? -1 : 1;   // +1 = left of the direction of travel
        var layout = RouteStakes.Layout(new Stake { Kind = StakeKind.Start, Station = station, Point = point, Direction = direction, Side = side }, textHeight);
        layout.NameText = name ?? "";
        if (!options.WithStation) layout.StationText = "";
        return layout;
    }
}
