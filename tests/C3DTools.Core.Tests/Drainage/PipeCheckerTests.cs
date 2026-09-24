using System.Linq;
using C3DTools.Core.Drainage;
using Xunit;

namespace C3DTools.Core.Tests.Drainage;

public class PipeCheckerTests
{
    private static readonly PipeRules Rules = new PipeRules(minSlope: 0.003, minCover: 0.7);

    private static PipeData Pipe() => new PipeData
    {
        Name = "C1",
        Length = 40,
        StartInvert = 10.0,
        EndInvert = 9.8,
        InnerDiameter = 0.6,
        WallThickness = 0.08,
        StartGround = 11.5,
        EndGround = 11.4,
    };

    [Fact]
    public void Good_pipe_has_no_issues()
    {
        var r = PipeChecker.Check(Pipe(), Rules);

        Assert.Empty(r.Issues);
        Assert.Equal(0.005, r.Slope, 9);
        Assert.Equal(0.82, r.StartCover, 9);  // 11.5 - (10.0 + 0.6 + 0.08)
        Assert.Equal(0.92, r.EndCover, 9);    // 11.4 - (9.8 + 0.6 + 0.08)
    }

    [Fact]
    public void Flags_slope_below_minimum()
    {
        var p = Pipe();
        p.EndInvert = 9.94;  // slope 0.0015

        var issue = Assert.Single(PipeChecker.Check(p, Rules).Issues);
        Assert.Equal(PipeIssueCode.SlopeTooLow, issue.Code);
        Assert.Contains("0.15%", issue.Message);
    }

    [Fact]
    public void Flags_adverse_slope()
    {
        var p = Pipe();
        p.EndInvert = 10.1;

        var r = PipeChecker.Check(p, Rules);
        Assert.True(r.Slope < 0);
        Assert.Contains(r.Issues, i => i.Code == PipeIssueCode.SlopeTooLow);
    }

    [Fact]
    public void Flags_low_cover_at_end()
    {
        var p = Pipe();
        p.EndGround = 10.9;  // cover 0.42

        var r = PipeChecker.Check(p, Rules);
        Assert.Equal(PipeIssueCode.CoverTooLowEnd, Assert.Single(r.Issues).Code);
    }

    [Fact]
    public void Zero_length_is_reported_without_other_checks()
    {
        var p = Pipe();
        p.Length = 0;

        var r = PipeChecker.Check(p, Rules);
        Assert.Equal(PipeIssueCode.InvalidLength, Assert.Single(r.Issues).Code);
        Assert.True(double.IsNaN(r.Slope));
    }

    [Fact]
    public void Messages_use_dot_decimal_on_vietnamese_windows()
    {
        TestCulture.Run("vi-VN", () =>
        {
            var p = Pipe();
            p.EndInvert = 9.94;
            Assert.Contains("0.15%", PipeChecker.Check(p, Rules).Issues.Single().Message);
        });
    }
}
