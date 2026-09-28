using System;
using System.Collections.Generic;
using System.Linq;

namespace C3DTools.Core.Ui;

public enum RouteChoiceReason { None, Preselected, Active, OnlyOne }

/// <summary>The alignment (by handle) a command works on and why; Handle is null when the user has to pick.</summary>
public readonly struct RouteChoice : IEquatable<RouteChoice>
{
    public static readonly RouteChoice None = new RouteChoice(null, RouteChoiceReason.None);

    public RouteChoice(string handle, RouteChoiceReason reason)
    {
        Handle = handle;
        Reason = reason;
    }

    public string Handle { get; }
    public RouteChoiceReason Reason { get; }
    public bool Found => Handle != null;

    public bool Equals(RouteChoice other) =>
        string.Equals(Handle, other.Handle, StringComparison.OrdinalIgnoreCase) && Reason == other.Reason;

    public override bool Equals(object obj) => obj is RouteChoice other && Equals(other);
    public override int GetHashCode() => (Handle ?? "").ToUpperInvariant().GetHashCode() ^ (int)Reason;
    public override string ToString() => Reason + ":" + Handle;
}

/// <summary>"Tuyến hiện hành": commands stop asking for the alignment once the drawing has one in use.</summary>
public static class ActiveRoute
{
    /// <summary>
    /// In order: the alignment selected before the command, the route remembered in the drawing if it still exists,
    /// the drawing's only alignment. Otherwise None and the command asks.
    /// </summary>
    /// <param name="preselected">Handle of the alignment selected before the command, or null.</param>
    /// <param name="stored">Handle remembered in the drawing, or null.</param>
    /// <param name="available">Handles of the drawing's alignments.</param>
    public static RouteChoice Resolve(string preselected, string stored, IEnumerable<string> available)
    {
        var all = (available ?? Enumerable.Empty<string>()).Where(h => !string.IsNullOrEmpty(h)).ToList();
        string Find(string h) => h == null ? null : all.FirstOrDefault(a => string.Equals(a, h, StringComparison.OrdinalIgnoreCase));

        var picked = Find(preselected);
        if (picked != null) return new RouteChoice(picked, RouteChoiceReason.Preselected);
        var active = Find(stored);
        if (active != null) return new RouteChoice(active, RouteChoiceReason.Active);
        return all.Count == 1 ? new RouteChoice(all[0], RouteChoiceReason.OnlyOne) : RouteChoice.None;
    }

    /// <summary>What the command line says about the choice; null when there is nothing to say.</summary>
    public static string Describe(RouteChoiceReason reason, string name) => reason switch
    {
        RouteChoiceReason.Active => "Tuyến hiện hành: " + name,
        RouteChoiceReason.OnlyOne => "Bản vẽ có một tuyến: " + name,
        _ => null,
    };
}
