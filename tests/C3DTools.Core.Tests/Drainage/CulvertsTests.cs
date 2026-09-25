using C3DTools.Core.Drainage;
using Xunit;

namespace C3DTools.Core.Tests.Drainage;

public class CulvertsTests
{
    [Theory]
    [InlineData("Circular", "", "Cống tròn")]
    [InlineData("Rectangular", "", "Cống hộp")]
    [InlineData("Arched", "", "Cống vòm")]
    [InlineData("Elliptical", "", "Cống elip")]
    [InlineData("HorizontalElliptical", "", "Cống elip")]
    [InlineData("EggShaped", "", "Cống trứng")]
    [InlineData("Undefined", "Concrete Box Culvert", "Cống hộp")]
    [InlineData("CustomShape", "Cống hộp BTCT", "Cống hộp")]
    [InlineData("CustomShape", "Concrete Pipe", "Cống tròn")]
    [InlineData("", "", "Cống")]
    public void Kind_from_shape_then_description(string shape, string description, string expected)
    {
        Assert.Equal(expected, Culverts.Kind(shape, description));
    }

    [Theory]
    [InlineData("Cống tròn", 1.0, 1.0, "D1000")]
    [InlineData("Cống tròn", 0.75, 0.75, "D750")]
    [InlineData("Cống hộp", 2.0, 2.0, "B×H 2.0×2.0")]
    [InlineData("Cống hộp", 1.25, 1.5, "B×H 1.25×1.5")]
    [InlineData("Cống vòm", 3, 2.4, "B×H 3.0×2.4")]
    public void Size_text(string kind, double width, double height, string expected)
    {
        Assert.Equal(expected, Culverts.SizeText(kind, width, height));
    }

    [Fact]
    public void Size_text_is_culture_invariant()
    {
        TestCulture.Run("vi-VN", () => Assert.Equal("B×H 1.25×1.5", Culverts.SizeText(Culverts.Box, 1.25, 1.5)));
    }

    [Fact]
    public void Size_text_without_height_uses_width()
    {
        Assert.Equal("B×H 2.0×2.0", Culverts.SizeText(Culverts.Box, 2.0, 0));
    }

    [Theory]
    [InlineData(0, 1, 1, 0, 0)]        // perpendicular crossing
    [InlineData(0, -1, 1, 0, 0)]       // flowing the other way: still square
    [InlineData(1, 1, 1, 0, 45)]
    [InlineData(-1, 1, 1, 0, 45)]
    [InlineData(1, 0, 1, 0, 90)]       // along the alignment
    [InlineData(0.2588190451, 0.9659258263, 1, 0, 15)]
    public void Skew_is_deviation_from_square(double pdx, double pdy, double tdx, double tdy, double expected)
    {
        Assert.Equal(expected, Culverts.SkewDegrees(pdx, pdy, tdx, tdy), 6);
    }

    [Fact]
    public void Skew_of_zero_vector_is_nan()
    {
        Assert.True(double.IsNaN(Culverts.SkewDegrees(0, 0, 1, 0)));
    }

    [Fact]
    public void Invert_from_endpoint_z()
    {
        Assert.Equal(10.0, Culverts.Invert(10.0, 1.0, endpointIsCentreline: false), 9);
        Assert.Equal(9.5, Culverts.Invert(10.0, 1.0, endpointIsCentreline: true), 9);
    }
}
