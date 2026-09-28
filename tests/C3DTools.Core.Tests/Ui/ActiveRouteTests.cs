using C3DTools.Core.Ui;
using Xunit;

namespace C3DTools.Core.Tests.Ui;

public class ActiveRouteTests
{
    private static readonly string[] Three = { "1A", "2B", "3C" };

    [Fact]
    public void Preselected_wins() =>
        Assert.Equal(new RouteChoice("2B", RouteChoiceReason.Preselected), ActiveRoute.Resolve("2B", "1A", Three));

    [Fact]
    public void Then_the_stored_route_when_it_still_exists() =>
        Assert.Equal(new RouteChoice("1A", RouteChoiceReason.Active), ActiveRoute.Resolve(null, "1a", Three));

    [Fact]
    public void A_stored_route_that_was_erased_is_ignored() =>
        Assert.Equal(RouteChoice.None, ActiveRoute.Resolve(null, "9Z", Three));

    [Fact]
    public void A_single_alignment_needs_no_choice() =>
        Assert.Equal(new RouteChoice("1A", RouteChoiceReason.OnlyOne), ActiveRoute.Resolve(null, null, new[] { "1A" }));

    [Fact]
    public void Preselected_must_be_an_alignment_of_the_drawing() =>
        Assert.Equal(new RouteChoice("1A", RouteChoiceReason.Active), ActiveRoute.Resolve("77", "1A", Three));

    [Fact]
    public void Nothing_to_choose_from()
    {
        Assert.Equal(RouteChoice.None, ActiveRoute.Resolve(null, null, new string[0]));
        Assert.Equal(RouteChoice.None, ActiveRoute.Resolve(null, null, null));
        Assert.False(RouteChoice.None.Found);
    }

    [Theory]
    [InlineData(RouteChoiceReason.Active, "Tuyến hiện hành: T1")]
    [InlineData(RouteChoiceReason.OnlyOne, "Bản vẽ có một tuyến: T1")]
    [InlineData(RouteChoiceReason.Preselected, null)]
    [InlineData(RouteChoiceReason.None, null)]
    public void Describe(RouteChoiceReason reason, string text) => Assert.Equal(text, ActiveRoute.Describe(reason, "T1"));

    [Fact]
    public void Handle_is_spelled_as_the_drawing_spells_it()
    {
        Assert.Equal("1A", ActiveRoute.Resolve(null, "1a", Three).Handle);
        Assert.Equal("2B", ActiveRoute.Resolve("2b", null, Three).Handle);
    }

    [Fact]
    public void RouteChoice_equality_and_hash_code()
    {
        var a = new RouteChoice("1a", RouteChoiceReason.Active);
        var b = new RouteChoice("1A", RouteChoiceReason.Active);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        var c = new RouteChoice("1A", RouteChoiceReason.OnlyOne);
        Assert.NotEqual(a, c);

        Assert.Equal(RouteChoice.None, default(RouteChoice));
    }
}
