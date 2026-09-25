using System;
using C3DTools.Core.Volumes;
using Xunit;

namespace C3DTools.Core.Tests.Volumes;

public class CrossSectionAreaTests
{
    [Fact]
    public void Uniform_cut_profile_returns_rectangle_area()
    {
        var result = CrossSectionArea.Compute(new[] { 2.0, 2.0 }, new[] { 1.0, 1.0 }, 5.0);

        Assert.Equal(5.0, result.CutArea, 9);
        Assert.Equal(0.0, result.FillArea, 9);
    }

    [Fact]
    public void Uniform_fill_profile_returns_rectangle_area()
    {
        var result = CrossSectionArea.Compute(new[] { 1.0, 1.0 }, new[] { 2.0, 2.0 }, 5.0);

        Assert.Equal(0.0, result.CutArea, 9);
        Assert.Equal(5.0, result.FillArea, 9);
    }

    [Fact]
    public void Crossing_profile_splits_cut_and_fill_triangles()
    {
        var result = CrossSectionArea.Compute(new[] { 1.0, -1.0 }, new[] { 0.0, 0.0 }, 4.0);

        Assert.Equal(1.0, result.CutArea, 9);
        Assert.Equal(1.0, result.FillArea, 9);
    }

    [Fact]
    public void Uses_trapezoid_for_mixed_but_same_sign_ends()
    {
        var result = CrossSectionArea.Compute(new[] { 1.0, 3.0, 2.0 }, new[] { 0.0, 0.0, 0.0 }, 2.0);

        Assert.Equal(9.0, result.CutArea, 9);
        Assert.Equal(0.0, result.FillArea, 9);
    }

    [Fact]
    public void Rejects_different_sample_counts()
    {
        Assert.Throws<ArgumentException>(() =>
            CrossSectionArea.Compute(new[] { 1.0, 2.0 }, new[] { 1.0 }, 1.0));
    }

    [Fact]
    public void Rejects_non_positive_spacing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CrossSectionArea.Compute(new[] { 1.0, 2.0 }, new[] { 1.0, 2.0 }, 0));
    }

    [Fact]
    public void Rejects_non_finite_elevation()
    {
        Assert.Throws<ArgumentException>(() =>
            CrossSectionArea.Compute(new[] { 1.0, double.NaN }, new[] { 1.0, 2.0 }, 1.0));
    }
}
