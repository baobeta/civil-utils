using System;
using C3DTools.Core.Curves;

namespace C3DTools.Core.Stations;

/// <summary>How the stake names are drawn on the plan ("Ghi tên cọc lên bình đồ").</summary>
public sealed class StakeLabelOptions
{
    /// <summary>"Tên cọc xen kẽ trái phải": every second stake has its name on the right and its station on the left.</summary>
    public bool AlternateSides { get; set; }

    /// <summary>"Ghi lý trình ở đầu kia": the station at the other end of the cross-section line.</summary>
    public bool WithStation { get; set; } = true;

    /// <summary>"Chỉ tại cọc Km": the station is written at Km stakes only, not at C, H or curve stakes.</summary>
    public bool StationOnlyAtKm { get; set; } = true;

    public int StationDecimals { get; set; } = 2;

    /// <summary>The choices of "Lý trình": index 0 none, 1 Km stakes only, 2 every stake.</summary>
    public static System.Collections.Generic.IReadOnlyList<string> StationModes { get; } =
        new[] { "Không ghi lý trình", "Lý trình chỉ tại cọc Km", "Lý trình tại mọi cọc" };

    public bool WritesStation(StakeRole role) => WithStation && (!StationOnlyAtKm || role == StakeRole.Km);
}

/// <summary>The two texts of a stake drawn at the ends of its cross-section line, parallel to the route.</summary>
public sealed class StakeEndLabels
{
    public string NameText { get; set; }
    public PlanPoint NamePoint { get; set; }

    /// <summary>Empty when the station is not written.</summary>
    public string StationText { get; set; }
    public PlanPoint StationPoint { get; set; }

    /// <summary>Text rotation, radians: along the route, never upside down.</summary>
    public double Rotation { get; set; }
}

public static class StakeLabelLayout
{
    /// <summary>Clear distance between the end of the line and the text, in text heights.</summary>
    public const double Gap = 1.0;

    /// <summary>
    /// The stake name just beyond the left end of its cross-section line and the station just beyond the right end
    /// (left and right of the direction of travel), both middle-centred and written along the route.
    /// </summary>
    /// <param name="index">0-based position among the labelled stakes; odd ones swap ends when alternating.</param>
    /// <param name="endA">One end of the cross-section line.</param>
    /// <param name="endB">The other end; which one is left is worked out from the direction.</param>
    public static StakeEndLabels AtEnds(int index, PlanPoint endA, PlanPoint endB, PlanPoint direction, double station, string name,
        double textHeight, StakeLabelOptions options, StakeRole role = StakeRole.Km)
    {
        if (!(textHeight > 0)) throw new ArgumentOutOfRangeException(nameof(textHeight));
        options ??= new StakeLabelOptions();
        double ax = endA.X - endB.X, ay = endA.Y - endB.Y;
        var length = Math.Sqrt(ax * ax + ay * ay);
        if (length < 1e-9) throw new ArgumentException("Trắc ngang không có chiều dài.", nameof(endA));

        // endA is on the left of the direction of travel when direction × (endA − endB) > 0.
        var aIsLeft = direction.X * ay - direction.Y * ax > 0;
        var left = aIsLeft ? endA : endB;
        var right = aIsLeft ? endB : endA;
        var ux = (left.X - right.X) / length;   // unit vector from the right end to the left end
        var uy = (left.Y - right.Y) / length;
        var reach = (Gap + 0.5) * textHeight;   // middle-centred text: half its height plus the gap
        var beyondLeft = new PlanPoint(left.X + ux * reach, left.Y + uy * reach);
        var beyondRight = new PlanPoint(right.X - ux * reach, right.Y - uy * reach);
        var swap = options.AlternateSides && index % 2 == 1;

        return new StakeEndLabels
        {
            NameText = name ?? "",
            NamePoint = swap ? beyondRight : beyondLeft,
            StationText = options.WritesStation(role)
                ? StationFormatter.Format(station, Math.Max(0, Math.Min(6, options.StationDecimals)), withKmPrefix: false)
                : "",
            StationPoint = swap ? beyondLeft : beyondRight,
            // Perpendicular to the cross-section line, so the text stays square to it on curves and skewed lines too.
            Rotation = TextAngle.Readable(Math.Atan2(-ux, uy)),
        };
    }
}
