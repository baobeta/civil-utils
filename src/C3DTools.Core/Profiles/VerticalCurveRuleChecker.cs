using System;
using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Presets;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Profiles;

public enum VerticalCurveIssueCode { NoRuleForSpeed, RadiusTooSmall, LengthTooShort, GradeTooSteep }

public sealed class VerticalCurveIssue
{
    public VerticalCurveIssue(VerticalCurveIssueCode code, string message)
    {
        Code = code;
        Message = message;
    }

    public VerticalCurveIssueCode Code { get; }
    public string Message { get; }
}

/// <summary>
/// Checks a PVI against the preset's VerticalRules for the design speed: Rmin lồi/lõm, Lmin, imax (percent) of i1 and i2.
/// No built-in values: an empty table is not checked; a table without a row for this speed gives NoRuleForSpeed.
/// A PVI without a curve is checked for grades only.
/// </summary>
public static class VerticalCurveRuleChecker
{
    public static List<VerticalCurveIssue> Check(VerticalCurve curve, double designSpeed, VerticalRules rules)
    {
        if (curve == null) throw new ArgumentNullException(nameof(curve));
        var issues = new List<VerticalCurveIssue>();
        if (rules == null) return issues;

        var speedText = NumberFormat.Trimmed(designSpeed, 0);
        var missing = new List<string>();
        var e = curve.Elements;

        if (e != null)
        {
            var radiusTable = curve.IsCrest ? rules.MinRadiusCrest : rules.MinRadiusSag;
            var radiusName = curve.IsCrest ? "Rmin lồi" : "Rmin lõm";
            var minRadius = Find(radiusTable, designSpeed, radiusName, missing);
            if (minRadius.HasValue && e.A > 0 && e.R < minRadius.Value)
                issues.Add(new VerticalCurveIssue(VerticalCurveIssueCode.RadiusTooSmall,
                    $"{curve.Name}: R = {M(e.R)} nhỏ hơn {radiusName} = {M(minRadius.Value)} (V = {speedText} km/h)."));

            var minLength = Find(rules.MinLength, designSpeed, "Lmin", missing);
            if (minLength.HasValue && e.L < minLength.Value)
                issues.Add(new VerticalCurveIssue(VerticalCurveIssueCode.LengthTooShort,
                    $"{curve.Name}: L = {M(e.L)} nhỏ hơn Lmin = {M(minLength.Value)}."));
        }

        var maxGrade = Find(rules.MaxGrade, designSpeed, "imax", missing);
        if (maxGrade.HasValue)
        {
            foreach (var (label, grade) in new[] { ("i1", curve.GradeIn), ("i2", curve.GradeOut) })
            {
                if (Math.Abs(grade) * 100 > maxGrade.Value + 1e-9)
                    issues.Add(new VerticalCurveIssue(VerticalCurveIssueCode.GradeTooSteep,
                        $"{curve.Name}: {label} = {VerticalCurveBoxText.Percent(grade)} dốc hơn imax = {NumberFormat.Fixed(maxGrade.Value, 2)}%."));
            }
        }

        if (missing.Count > 0)
            issues.Insert(0, new VerticalCurveIssue(VerticalCurveIssueCode.NoRuleForSpeed,
                $"Preset chưa có {string.Join(", ", missing)} cho vận tốc {speedText} km/h."));
        return issues;
    }

    /// <summary>The value for this speed; null when the table is empty (not checked) or has no row for it (noted in missing).</summary>
    internal static double? Find(List<SpeedValueRule> table, double speed, string name, List<string> missing)
    {
        if (table == null || table.Count == 0) return null;
        var rule = table.FirstOrDefault(r => r != null && r.DesignSpeed == speed);
        if (rule == null) missing?.Add(name);
        return rule?.Value;
    }

    private static string M(double v) => NumberFormat.Fixed(v, 2) + " m";
}
