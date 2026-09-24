using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace C3DTools.Core.Curves;

public enum CurveIssueCode { NoRuleForSpeed, RadiusTooSmall, RadiusBelowNormal, SpiralTooShort }

public sealed class CurveIssue
{
    public CurveIssue(CurveIssueCode code, string message)
    {
        Code = code;
        Message = message;
    }

    public CurveIssueCode Code { get; }
    public string Message { get; }
}

public sealed class CurveCheckResult
{
    public List<CurveIssue> Issues { get; } = new List<CurveIssue>();

    /// <summary>Null when the widening table has no row for this radius.</summary>
    public double? Widening { get; set; }
}

public static class CurveRuleChecker
{
    public static CurveCheckResult Check(CurveGroup curve, double designSpeed, CurveRules rules)
    {
        if (curve == null) throw new ArgumentNullException(nameof(curve));
        if (rules == null) throw new ArgumentNullException(nameof(rules));

        var result = new CurveCheckResult();
        var name = "Đ" + curve.Index.ToString(CultureInfo.InvariantCulture);
        var speedText = designSpeed.ToString("0", CultureInfo.InvariantCulture);

        var minRadius = rules.MinRadius.FirstOrDefault(r => r.DesignSpeed == designSpeed);
        if (minRadius == null)
            result.Issues.Add(new CurveIssue(CurveIssueCode.NoRuleForSpeed,
                $"Preset chưa có Rmin cho vận tốc {speedText} km/h."));
        else if (curve.Radius < minRadius.MinRadius)
            result.Issues.Add(new CurveIssue(CurveIssueCode.RadiusTooSmall,
                $"{name}: R = {M(curve.Radius)} nhỏ hơn Rmin giới hạn = {M(minRadius.MinRadius)} (V = {speedText} km/h)."));
        else if (curve.Radius < minRadius.NormalRadius)
            result.Issues.Add(new CurveIssue(CurveIssueCode.RadiusBelowNormal,
                $"{name}: R = {M(curve.Radius)} nhỏ hơn Rmin thông thường = {M(minRadius.NormalRadius)}."));

        var minSpiral = Find(rules.MinSpiral, curve.Radius, designSpeed);
        if (minSpiral != null)
        {
            var shortest = Math.Min(curve.SpiralIn, curve.SpiralOut);
            if (shortest < minSpiral.Value)
                result.Issues.Add(new CurveIssue(CurveIssueCode.SpiralTooShort,
                    $"{name}: L = {M(shortest)} nhỏ hơn Lmin = {M(minSpiral.Value)}."));
        }

        result.Widening = Find(rules.Widening, curve.Radius, designSpeed)?.Value;
        return result;
    }

    private static RadiusRangeRule Find(IEnumerable<RadiusRangeRule> table, double radius, double speed) =>
        table.FirstOrDefault(r => (r.DesignSpeed == 0 || r.DesignSpeed == speed)
                                  && radius > r.RadiusFrom && radius <= r.RadiusTo);

    private static string M(double v) => v.ToString("0.00", CultureInfo.InvariantCulture) + " m";
}
