using System;
using System.Collections.Generic;
using System.Globalization;

namespace C3DTools.Core.Drainage;

public sealed class PipeData
{
    public string Name { get; set; } = "";
    public double Length { get; set; }
    public double StartInvert { get; set; }
    public double EndInvert { get; set; }
    public double InnerDiameter { get; set; }
    public double WallThickness { get; set; }
    public double StartGround { get; set; }
    public double EndGround { get; set; }
}

/// <summary>Project thresholds. Values come from the preset; there are deliberately no defaults.</summary>
public sealed class PipeRules
{
    public PipeRules(double minSlope, double minCover)
    {
        MinSlope = minSlope;
        MinCover = minCover;
    }

    /// <summary>Minimum slope as a fraction (0.003 = 0.3%).</summary>
    public double MinSlope { get; }

    /// <summary>Minimum cover in metres, ground to pipe outer top.</summary>
    public double MinCover { get; }
}

public enum PipeIssueCode { InvalidLength, SlopeTooLow, CoverTooLowStart, CoverTooLowEnd }

public sealed class PipeIssue
{
    public PipeIssue(PipeIssueCode code, string message)
    {
        Code = code;
        Message = message;
    }

    public PipeIssueCode Code { get; }
    public string Message { get; }
}

public sealed class PipeCheckResult
{
    public PipeCheckResult(string name, double slope, double startCover, double endCover, IReadOnlyList<PipeIssue> issues)
    {
        Name = name;
        Slope = slope;
        StartCover = startCover;
        EndCover = endCover;
        Issues = issues;
    }

    public string Name { get; }
    public double Slope { get; }
    public double StartCover { get; }
    public double EndCover { get; }
    public IReadOnlyList<PipeIssue> Issues { get; }
}

public static class PipeChecker
{
    public static PipeCheckResult Check(PipeData pipe, PipeRules rules)
    {
        if (pipe == null) throw new ArgumentNullException(nameof(pipe));
        if (rules == null) throw new ArgumentNullException(nameof(rules));

        var issues = new List<PipeIssue>();
        if (!(pipe.Length > 0))
        {
            issues.Add(new PipeIssue(PipeIssueCode.InvalidLength, $"Cống {pipe.Name}: chiều dài không hợp lệ."));
            return new PipeCheckResult(pipe.Name, double.NaN, double.NaN, double.NaN, issues);
        }

        var slope = (pipe.StartInvert - pipe.EndInvert) / pipe.Length;
        var outerHeight = pipe.InnerDiameter + pipe.WallThickness;
        var startCover = pipe.StartGround - (pipe.StartInvert + outerHeight);
        var endCover = pipe.EndGround - (pipe.EndInvert + outerHeight);

        if (slope < rules.MinSlope)
            issues.Add(new PipeIssue(PipeIssueCode.SlopeTooLow,
                $"Cống {pipe.Name}: độ dốc {Percent(slope)} nhỏ hơn tối thiểu {Percent(rules.MinSlope)}."));
        if (startCover < rules.MinCover)
            issues.Add(new PipeIssue(PipeIssueCode.CoverTooLowStart,
                $"Cống {pipe.Name}: chiều sâu chôn đầu cống {Metres(startCover)} nhỏ hơn {Metres(rules.MinCover)}."));
        if (endCover < rules.MinCover)
            issues.Add(new PipeIssue(PipeIssueCode.CoverTooLowEnd,
                $"Cống {pipe.Name}: chiều sâu chôn cuối cống {Metres(endCover)} nhỏ hơn {Metres(rules.MinCover)}."));

        return new PipeCheckResult(pipe.Name, slope, startCover, endCover, issues);
    }

    private static string Percent(double fraction) =>
        (fraction * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%";

    private static string Metres(double value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture) + " m";
}
