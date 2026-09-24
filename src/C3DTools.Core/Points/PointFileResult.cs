using System.Collections.Generic;
using System.Linq;

namespace C3DTools.Core.Points;

public enum IssueSeverity { Warning, Error }

public enum PointIssueCode { WrongColumnCount, NotANumber, DuplicateNumber, CommaDecimal, SwappedXY }

public sealed class PointIssue
{
    public PointIssue(int line, IssueSeverity severity, PointIssueCode code, string message)
    {
        Line = line;
        Severity = severity;
        Code = code;
        Message = message;
    }

    public int Line { get; }
    public IssueSeverity Severity { get; }
    public PointIssueCode Code { get; }
    public string Message { get; }
}

public sealed class SurveyPoint
{
    public uint Number { get; set; }
    public double Northing { get; set; }
    public double Easting { get; set; }
    public double? Elevation { get; set; }
    public string Description { get; set; } = "";
    public int Line { get; set; }
}

public sealed class PointFileResult
{
    public PointFileResult(IReadOnlyList<SurveyPoint> points, IReadOnlyList<PointIssue> issues)
    {
        Points = points;
        Issues = issues;
    }

    public IReadOnlyList<SurveyPoint> Points { get; }
    public IReadOnlyList<PointIssue> Issues { get; }
    public bool HasErrors => Issues.Any(i => i.Severity == IssueSeverity.Error);
}
