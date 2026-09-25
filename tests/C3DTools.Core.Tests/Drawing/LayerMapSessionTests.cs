using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Drawing;
using C3DTools.Core.Presets;
using Xunit;

namespace C3DTools.Core.Tests.Drawing;

public class LayerMapSessionTests
{
    private static LayerMapRule Rule(string pattern, string layer, short color = 7, string linetype = "Continuous") =>
        new LayerMapRule { Pattern = pattern, Layer = layer, Color = color, Linetype = linetype };

    private static LayerMapSession Session(IEnumerable<LayerMapRule> rules, params LayerUsage[] layers)
    {
        var s = new LayerMapSession(rules);
        s.Load(layers);
        return s;
    }

    [Fact]
    public void Rows_start_from_the_preset_rules()
    {
        var s = Session(new[] { Rule("*COC*", "TK_COC", 1, "DASHED") },
            new LayerUsage("KS_COC", 4, 2), new LayerUsage("0", 9));

        var coc = s.Rows[0];
        Assert.Equal("KS_COC", coc.Name);
        Assert.Equal("TK_COC", coc.Target);
        Assert.Equal("1", coc.ColorText);
        Assert.Equal("DASHED", coc.Linetype);
        Assert.Equal(4, coc.ObjectCount);
        Assert.True(coc.WillMove);
        Assert.Equal("", s.Rows[1].Target);
        Assert.False(s.Rows[1].WillMove);
        Assert.Equal(new[] { "TK_COC" }, s.TargetNames);
        Assert.True(s.CanApply);
        Assert.Equal("1 layer sẽ chuyển (4 đối tượng)", s.SummaryText);
    }

    [Fact]
    public void Block_counts_are_included_only_when_applying_to_blocks()
    {
        var s = Session(new[] { Rule("A", "B") }, new LayerUsage("A", 4, 2));
        var changed = new List<string>();
        s.Rows[0].PropertyChanged += (o, e) => changed.Add(e.PropertyName);

        s.ApplyToBlocks = true;

        Assert.Equal(6, s.Rows[0].ObjectCount);
        Assert.Contains(nameof(LayerMapRow.ObjectCount), changed);
        Assert.Equal("1 layer sẽ chuyển (6 đối tượng)", s.SummaryText);
        Assert.Equal(6, s.Moves().Single().Count);
    }

    [Fact]
    public void Choosing_a_preset_target_takes_its_colour_and_linetype()
    {
        var s = Session(new[] { Rule("X*", "TK_TIM", 1, "CENTER") }, new LayerUsage("TIM", 3));

        s.Rows[0].Target = "tk_tim";

        Assert.Equal("1", s.Rows[0].ColorText);
        Assert.Equal("CENTER", s.Rows[0].Linetype);
        var move = s.Moves().Single();
        Assert.Equal("tk_tim", move.To);
        Assert.Equal(1, move.Color);
    }

    [Fact]
    public void Free_text_target_keeps_the_row_colour()
    {
        var s = Session(new LayerMapRule[0], new LayerUsage("TIM", 3));
        s.Rows[0].ColorText = "3";

        s.Rows[0].Target = "  TK_MOI  ";

        var move = s.Moves().Single();
        Assert.Equal("TK_MOI", move.To);
        Assert.Equal(3, move.Color);
        Assert.Equal("Continuous", move.Linetype);
    }

    [Theory]
    [InlineData("A<B")]
    [InlineData("A|B")]
    [InlineData("A*")]
    [InlineData("A:B")]
    [InlineData("A=B")]
    public void Invalid_target_names_mark_the_row_and_block_apply(string target)
    {
        var s = Session(new[] { Rule("A", "B") }, new LayerUsage("A", 1));

        s.Rows[0].Target = target;

        Assert.False(s.Rows[0].IsTargetValid);
        Assert.False(s.Rows[0].IsValid);
        Assert.False(s.CanApply);
        Assert.Equal("Tên layer hoặc màu không hợp lệ (ô tô đỏ)", s.SummaryText);
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("256", false)]
    [InlineData("abc", false)]
    [InlineData("2,5", false)]
    [InlineData(" 12 ", true)]
    [InlineData("255", true)]
    public void Colour_must_be_an_aci_index(string text, bool valid)
    {
        var s = Session(new[] { Rule("A", "B") }, new LayerUsage("A", 1));

        s.Rows[0].ColorText = text;

        Assert.Equal(valid, s.Rows[0].IsColorValid);
        Assert.Equal(valid, s.CanApply);
    }

    [Fact]
    public void Nothing_to_move_blocks_apply_unless_purging()
    {
        var s = Session(new LayerMapRule[0], new LayerUsage("A", 1));
        Assert.False(s.CanApply);
        Assert.Equal("Không có layer nào cần chuyển", s.SummaryText);

        s.PurgeEmpty = true;

        Assert.True(s.CanApply);
        Assert.Equal("Không có layer nào cần chuyển; xoá layer rỗng", s.SummaryText);
    }

    [Fact]
    public void Target_equal_to_the_layer_ignoring_case_is_not_a_move()
    {
        var s = Session(new LayerMapRule[0], new LayerUsage("Tim", 1));

        s.Rows[0].Target = "TIM";

        Assert.False(s.Rows[0].WillMove);
        Assert.Empty(s.Moves());
    }

    [Fact]
    public void Select_finds_the_row_ignoring_case()
    {
        var s = Session(new LayerMapRule[0], new LayerUsage("A", 1), new LayerUsage("Bb", 1));

        Assert.True(s.Select("bB"));
        Assert.Same(s.Rows[1], s.SelectedRow);
        Assert.False(s.Select("C"));
        Assert.Same(s.Rows[1], s.SelectedRow);
    }

    [Fact]
    public void MergedRules_puts_edited_rows_first_and_keeps_preset_rules()
    {
        var s = Session(new[] { Rule("*COC*", "TK_COC", 1), Rule("DIM*", "TK_DIM", 2) },
            new LayerUsage("KS_COC", 4), new LayerUsage("KS_TEXT", 1), new LayerUsage("DIM1", 1), new LayerUsage("DIM2", 1));
        s.Rows[1].Target = "TK_CHU";   // unmatched → new exact rule
        s.Rows[1].ColorText = "4";
        s.Rows[2].Target = "";         // cleared: keep DIM1 as it is
        // KS_COC and DIM2 still follow the preset: no extra rule

        var rules = s.MergedRules();

        Assert.Equal(new[] { "KS_TEXT", "DIM1", "*COC*", "DIM*" }, rules.Select(r => r.Pattern));
        Assert.Equal("TK_CHU", rules[0].Layer);
        Assert.Equal(4, rules[0].Color);
        Assert.Equal("DIM1", rules[1].Layer);
        Assert.Equal(1, rules[2].Color);
    }

    [Fact]
    public void MergedRules_replaces_an_existing_exact_rule_for_the_same_layer()
    {
        var s = Session(new[] { Rule("KS_TEXT", "OLD"), Rule("*", "TK_KHAC") }, new LayerUsage("KS_TEXT", 1));
        s.Rows[0].Target = "NEW";

        var rules = s.MergedRules();

        Assert.Equal(new[] { "KS_TEXT", "*" }, rules.Select(r => r.Pattern));
        Assert.Equal("NEW", rules[0].Layer);
    }

    [Fact]
    public void UseRules_makes_saved_rows_follow_the_preset()
    {
        var s = Session(new LayerMapRule[0], new LayerUsage("A", 1));
        s.Rows[0].Target = "B";
        var merged = s.MergedRules();

        s.UseRules(merged);

        Assert.Equal(merged.Select(r => r.Pattern), s.MergedRules().Select(r => r.Pattern));
        Assert.Equal(new[] { "B" }, s.TargetNames);
        Assert.Equal("B", s.Rows[0].Target);
    }

    [Fact]
    public void Empty_and_missing_linetypes_count_as_continuous_when_merging()
    {
        var s = Session(new[] { Rule("A*", "B", 7, null), Rule("C*", "D", 7, "") }, new LayerUsage("A1", 1), new LayerUsage("C1", 1));
        Assert.Equal("Continuous", s.Rows[0].Linetype);

        s.Rows[1].Linetype = "continuous";

        Assert.Equal(new[] { "A*", "C*" }, s.MergedRules().Select(r => r.Pattern));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(256)]
    [InlineData(-1)]
    public void Preset_colours_outside_the_aci_range_become_7_with_a_warning(short color)
    {
        var s = Session(new[] { Rule("A*", "B", color) }, new LayerUsage("A1", 1));

        Assert.Equal("7", s.Rows[0].ColorText);
        Assert.True(s.Rows[0].IsValid);
        Assert.True(s.CanApply);
        Assert.Contains("không hợp lệ", Assert.Single(s.Warnings));
        Assert.Equal(7, s.MergedRules().Single().Color);
    }
}
