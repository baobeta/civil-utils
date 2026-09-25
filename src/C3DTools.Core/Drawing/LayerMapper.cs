using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using C3DTools.Core.Presets;

namespace C3DTools.Core.Drawing;

/// <summary>A layer of the drawing and how many objects are on it (layouts; BlockCount: inside block definitions).</summary>
public sealed class LayerUsage
{
    public LayerUsage(string name, int count, int blockCount = 0)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Count = count;
        BlockCount = blockCount;
    }

    public string Name { get; }
    public int Count { get; }
    public int BlockCount { get; }
}

/// <summary>Objects on From move to To; To is created with Color (ACI) and Linetype when the drawing lacks it.</summary>
public sealed class LayerMove
{
    public LayerMove(string from, string to, short color, string linetype, int count)
    {
        From = from;
        To = to;
        Color = color;
        Linetype = linetype;
        Count = count;
    }

    public string From { get; }
    public string To { get; }
    public short Color { get; }
    public string Linetype { get; }
    public int Count { get; }
}

public sealed class LayerMapPlan
{
    public List<LayerMove> Moves { get; } = new List<LayerMove>();

    /// <summary>Layers no rule matches; they stay as they are.</summary>
    public List<LayerUsage> Unmatched { get; } = new List<LayerUsage>();
}

/// <summary>CTLAYER: applies the preset's LayerMap (first matching rule wins) to the drawing's layers.</summary>
public static class LayerMapper
{
    /// <summary>
    /// AutoCAD-style wildcards, case-insensitive: * any run, ? one character, "A*,B*" either pattern.
    /// Every other character is literal. An empty pattern matches nothing.
    /// </summary>
    public static bool Matches(string pattern, string name)
    {
        if (string.IsNullOrEmpty(pattern)) return false;
        name ??= "";
        foreach (var part in pattern.Split(','))
        {
            var p = part.Trim();
            if (p.Length == 0) continue;
            var regex = "^" + Regex.Escape(p).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            if (Regex.IsMatch(name, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)) return true;
        }

        return false;
    }

    /// <summary>The first usable rule (pattern and target set) that matches name, or null.</summary>
    public static LayerMapRule FirstMatch(IEnumerable<LayerMapRule> rules, string name) =>
        rules?.FirstOrDefault(r => r != null && !string.IsNullOrWhiteSpace(r.Layer) && Matches(r.Pattern, name));

    public static LayerMapPlan Plan(IEnumerable<LayerMapRule> rules, IEnumerable<LayerUsage> layers)
    {
        var ruleList = (rules ?? Enumerable.Empty<LayerMapRule>()).ToList();
        var plan = new LayerMapPlan();
        foreach (var layer in layers ?? Enumerable.Empty<LayerUsage>())
        {
            var rule = FirstMatch(ruleList, layer.Name);
            if (rule == null)
            {
                plan.Unmatched.Add(layer);
                continue;
            }

            var target = rule.Layer.Trim();
            if (string.Equals(target, layer.Name, StringComparison.OrdinalIgnoreCase)) continue;
            plan.Moves.Add(new LayerMove(layer.Name, target, rule.Color, rule.Linetype, layer.Count));
        }

        return plan;
    }
}
