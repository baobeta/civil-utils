using System.Linq;
using C3DTools.Core.Drawing;
using C3DTools.Core.Presets;
using Xunit;

namespace C3DTools.Core.Tests.Drawing;

public class LayerMapperTests
{
    private static LayerMapRule Rule(string pattern, string layer, short color = 7, string linetype = "Continuous") =>
        new LayerMapRule { Pattern = pattern, Layer = layer, Color = color, Linetype = linetype };

    [Theory]
    [InlineData("*COC*", "TD_COC_CHI_TIET", true)]
    [InlineData("*coc*", "td_Coc", true)]
    [InlineData("COC?", "COC1", true)]
    [InlineData("COC?", "COC12", false)]
    [InlineData("COC", "COC1", false)]
    [InlineData("*", "", true)]
    [InlineData("A*,B*", "B-TEXT", true)]
    [InlineData("A*,B*", "C-TEXT", false)]
    [InlineData("TIM.TUYEN", "TIM.TUYEN", true)]
    [InlineData("TIM.TUYEN", "TIMXTUYEN", false)]
    [InlineData("ĐƯỜNG*", "đường đồng mức", true)]
    [InlineData("", "ANY", false)]
    public void Matches_wildcards_case_insensitively(string pattern, string name, bool expected) =>
        Assert.Equal(expected, LayerMapper.Matches(pattern, name));

    [Fact]
    public void First_matching_rule_wins_in_rule_order()
    {
        var rules = new[] { Rule("*COC*", "TK_COC", 1), Rule("*", "TK_KHAC", 8) };

        var plan = LayerMapper.Plan(rules, new[] { new LayerUsage("KS_COC", 5), new LayerUsage("TEXT", 2) });

        Assert.Equal(2, plan.Moves.Count);
        var coc = plan.Moves[0];
        Assert.Equal("KS_COC", coc.From);
        Assert.Equal("TK_COC", coc.To);
        Assert.Equal(1, coc.Color);
        Assert.Equal(5, coc.Count);
        Assert.Equal("TK_KHAC", plan.Moves[1].To);
        Assert.Equal(8, plan.Moves[1].Color);
        Assert.Empty(plan.Unmatched);
    }

    [Fact]
    public void Unmatched_layers_are_listed_and_not_moved()
    {
        var rules = new[] { Rule("DIM*", "TK_KICH_THUOC", 2, "DASHED") };

        var plan = LayerMapper.Plan(rules, new[] { new LayerUsage("DIM1", 3), new LayerUsage("0", 10), new LayerUsage("HATCH", 0) });

        var move = Assert.Single(plan.Moves);
        Assert.Equal("DASHED", move.Linetype);
        Assert.Equal(new[] { "0", "HATCH" }, plan.Unmatched.Select(u => u.Name));
    }

    [Fact]
    public void A_layer_already_on_its_target_is_neither_moved_nor_unmatched()
    {
        var plan = LayerMapper.Plan(new[] { Rule("TK_*", "tk_coc") }, new[] { new LayerUsage("TK_COC", 4) });

        Assert.Empty(plan.Moves);
        Assert.Empty(plan.Unmatched);
    }

    [Fact]
    public void Rules_without_a_pattern_or_target_are_ignored()
    {
        var rules = new[] { Rule("", "X"), Rule("*", " "), null, Rule("*", "Y") };

        var move = Assert.Single(LayerMapper.Plan(rules, new[] { new LayerUsage("A", 1) }).Moves);
        Assert.Equal("Y", move.To);
    }

    [Fact]
    public void FirstMatch_returns_null_without_rules()
    {
        Assert.Null(LayerMapper.FirstMatch(null, "A"));
        Assert.Null(LayerMapper.FirstMatch(new[] { Rule("B*", "X") }, "A"));
        Assert.Equal("X", LayerMapper.FirstMatch(new[] { Rule("A*", "X") }, "A1").Layer);
    }
}
