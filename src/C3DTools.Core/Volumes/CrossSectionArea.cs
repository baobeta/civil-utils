using System;
using System.Collections.Generic;

namespace C3DTools.Core.Volumes;

public sealed class CrossSectionAreas
{
    public CrossSectionAreas(double cutArea, double fillArea)
    {
        CutArea = cutArea;
        FillArea = fillArea;
    }

    public double CutArea { get; }
    public double FillArea { get; }
}

/// <summary>
/// Computes cut/fill areas between two sampled ground profiles.
/// Positive base-minus-design elevation is cut; negative elevation is fill.
/// </summary>
public static class CrossSectionArea
{
    public static CrossSectionAreas Compute(
        IReadOnlyList<double> baseElevations,
        IReadOnlyList<double> designElevations,
        double spacing)
    {
        if (baseElevations == null) throw new ArgumentNullException(nameof(baseElevations));
        if (designElevations == null) throw new ArgumentNullException(nameof(designElevations));
        if (baseElevations.Count != designElevations.Count)
            throw new ArgumentException("Hai profile phải có cùng số điểm cao.", nameof(designElevations));
        if (baseElevations.Count < 2)
            throw new ArgumentException("Cần ít nhất hai điểm cao để tính diện tích.", nameof(baseElevations));
        if (!(spacing > 0) || double.IsNaN(spacing) || double.IsInfinity(spacing))
            throw new ArgumentOutOfRangeException(nameof(spacing), "Khoảng cách lấy mẫu phải lớn hơn 0.");

        for (var i = 0; i < baseElevations.Count; i++)
        {
            if (double.IsNaN(baseElevations[i]) || double.IsInfinity(baseElevations[i]) ||
                double.IsNaN(designElevations[i]) || double.IsInfinity(designElevations[i]))
                throw new ArgumentException($"Cao độ tại điểm {i} không hợp lệ.", nameof(baseElevations));
        }

        var cut = 0.0;
        var fill = 0.0;
        for (var i = 1; i < baseElevations.Count; i++)
        {
            var d0 = baseElevations[i - 1] - designElevations[i - 1];
            var d1 = baseElevations[i] - designElevations[i];
            cut += PositiveTriangleIntegral(d0, d1, spacing);
            fill += PositiveTriangleIntegral(-d0, -d1, spacing);
        }

        return new CrossSectionAreas(cut, fill);
    }

    private static double PositiveTriangleIntegral(double start, double end, double width)
    {
        if (start <= 0 && end <= 0) return 0;
        if (start >= 0 && end >= 0) return (start + end) / 2.0 * width;
        if (start > 0) return start * start / (2.0 * (start - end)) * width;
        return end * end / (2.0 * (end - start)) * width;
    }
}
