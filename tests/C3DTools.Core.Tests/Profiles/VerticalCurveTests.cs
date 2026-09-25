using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Presets;
using C3DTools.Core.Profiles;
using Xunit;

namespace C3DTools.Core.Tests.Profiles;

public class VerticalCurveTests
{
    // Hand-computed fixture: i1 = +3 %, i2 = −2 %, L = 100 → A = 5 %, R = 100 / 0.05 = 2000, T = 50, E = 0.05·100/8 = 0.625.
    private static ProfileSegment Crest() => ProfileSegment.Parabola(1000, 12.35, 0.03, -0.02, 100);

    [Fact]
    public void Crest_parabola_fixture()
    {
        var e = VerticalCurveElements.Compute(0.03, -0.02, 100, ProfileSegmentKind.ParabolaSymmetric);

        Assert.Equal(5, e.A, 9);
        Assert.Equal(100, e.L, 9);
        Assert.Equal(100, e.K, 9);
        Assert.Equal(2000, e.R, 6);
        Assert.Equal(50, e.T, 9);
        Assert.Equal(0.625, e.E, 9);
        Assert.True(e.IsCrest);
        Assert.Equal(60, e.HighLowOffset.Value, 9);   // 0.03 / 0.05 · 100
    }

    [Fact]
    public void External_in_percent_and_fraction_readings_agree()
    {
        var e = VerticalCurveElements.Compute(0.03, -0.02, 100, ProfileSegmentKind.ParabolaSymmetric);

        Assert.Equal(e.A * e.L / 800, e.E, 12);        // A in percent
        Assert.Equal(e.A / 100 * e.L / 8, e.E, 12);    // A as fraction
        Assert.Equal(e.T * e.T / (2 * e.R), e.E, 9);   // T²/2R
    }

    [Fact]
    public void Sag_parabola()
    {
        // i1 = −1.5 %, i2 = +2.5 %: A = 4 %, L = 80 → R = 2000, E = 0.04·80/8 = 0.4, low point 30 m after TĐ.
        var c = VerticalCurve.FromSegment(ProfileSegment.Parabola(500, 20, -0.015, 0.025, 80), 2);

        Assert.False(c.IsCrest);
        Assert.Equal(4, c.Elements.A, 9);
        Assert.Equal(2000, c.Elements.R, 6);
        Assert.Equal(0.4, c.Elements.E, 9);
        Assert.Equal(460, c.StartStation, 9);
        Assert.Equal(540, c.EndStation, 9);
        Assert.Equal(20.6, c.StartElevation, 9);
        Assert.Equal(21, c.EndElevation, 9);
        Assert.Equal(20.4, c.CurveElevationAtPvi, 9);
        Assert.Equal(490, c.HighLowStation.Value, 9);
        Assert.Equal(20.6 - 0.015 * 30 / 2, c.HighLowElevation.Value, 9);
    }

    [Fact]
    public void Crest_curve_stations_and_elevations()
    {
        var c = VerticalCurve.FromSegment(Crest(), 1);

        Assert.Equal("Đ1", c.Name);
        Assert.Equal(950, c.StartStation, 9);
        Assert.Equal(1050, c.EndStation, 9);
        Assert.Equal(12.35 - 1.5, c.StartElevation, 9);
        Assert.Equal(12.35 - 1.0, c.EndElevation, 9);
        Assert.Equal(12.35 - 0.625, c.CurveElevationAtPvi, 9);
        Assert.Equal(1010, c.HighLowStation.Value, 9);
        Assert.Equal(10.85 + 0.03 * 60 / 2, c.HighLowElevation.Value, 9);
    }

    [Fact]
    public void Same_sign_grades_have_no_high_or_low_point()
    {
        var e = VerticalCurveElements.Compute(0.04, 0.01, 90, ProfileSegmentKind.ParabolaSymmetric);

        Assert.Null(e.HighLowOffset);
        Assert.Equal(3000, e.R, 6);
    }

    [Fact]
    public void Circular_curve_from_radius()
    {
        var e = VerticalCurveElements.Compute(0.03, -0.02, 2000, ProfileSegmentKind.Circular);

        Assert.Equal(100, e.L, 9);
        Assert.Equal(2000, e.R, 9);
        Assert.Equal(50, e.T, 9);
        Assert.Equal(0.625, e.E, 9);
    }

    [Fact]
    public void Asymmetric_parabola()
    {
        var c = VerticalCurve.FromSegment(ProfileSegment.AsymmetricParabola(1000, 50, 0.03, -0.02, 40, 60), 1);
        var e = c.Elements;

        Assert.True(e.IsAsymmetric);
        Assert.Equal(100, e.L, 9);
        Assert.Equal(40, e.T1, 9);
        Assert.Equal(60, e.T2, 9);
        Assert.Equal(0.05 * 40 * 60 / 200, e.E, 9);
        Assert.Equal(960, c.StartStation, 9);
        Assert.Equal(1060, c.EndStation, 9);
    }

    [Fact]
    public void Asymmetric_high_point_on_the_second_branch()
    {
        // L1 60, L2 40: branch 2 grade i2 − r2·u, r2 = −0.05·60/(100·40) = −0.00075 → u = 0.02/0.00075 = 26.667 before TC.
        var c = VerticalCurve.FromSegment(ProfileSegment.AsymmetricParabola(1000, 50, 0.03, -0.02, 60, 40), 1);

        Assert.Equal(1040 - 0.02 / 0.00075, c.HighLowStation.Value, 9);
        Assert.Equal(49.2 + 0.02 * (0.02 / 0.00075) / 2, c.HighLowElevation.Value, 9);
        Assert.Equal("Điểm cao=1+013.33  CĐ=49.47", VerticalCurveBoxText.Build(c)[4]);
    }

    [Fact]
    public void Asymmetric_high_point_on_the_first_branch()
    {
        // L1 80, L2 20: r1 = −0.05·20/(100·80) = −0.000125 → x = 0.01/0.000125 = 80 would be at the PVI; use i1 = 0.005: x = 40.
        var c = VerticalCurve.FromSegment(ProfileSegment.AsymmetricParabola(1000, 50, 0.005, -0.045, 80, 20), 1);

        Assert.Equal(960, c.HighLowStation.Value, 9);
        Assert.Equal(50 - 0.005 * 80 + 0.005 * 40 / 2, c.HighLowElevation.Value, 9);
    }

    [Fact]
    public void Asymmetric_vertex_matches_the_curve_at_the_pvi_when_it_falls_there()
    {
        var c = VerticalCurve.FromSegment(ProfileSegment.AsymmetricParabola(1000, 50, 0.03, -0.02, 40, 60), 1);

        Assert.Equal(1000, c.HighLowStation.Value, 9);
        Assert.Equal(c.CurveElevationAtPvi, c.HighLowElevation.Value, 9);
    }

    [Fact]
    public void Circular_curve_keeps_the_segment_stations()
    {
        var s = ProfileSegment.Circular(1000, 12.35, 0.03, -0.02, 2000);
        s.StartStation = 949.99;
        s.EndStation = 1050.02;
        s.StartElevation = 10.8497;
        s.EndElevation = 11.3496;

        var c = VerticalCurve.FromSegment(s, 1);

        Assert.Equal((949.99, 1050.02, 10.8497, 11.3496), (c.StartStation, c.EndStation, c.StartElevation, c.EndElevation));
    }

    [Theory]
    [InlineData(3.0, 0.03, 0.03, true)]      // percent → fraction
    [InlineData(-2.0, -0.02, -0.02, true)]
    [InlineData(0.03, 0.0301, 0.03, false)]  // already a fraction
    [InlineData(0.5, 0.0, 0.5, false)]       // no geometry to compare with
    [InlineData(0.9, 0.03, 0.9, false)]      // 30×: not a unit mix-up, left alone
    public void Normalize_grade(double reported, double geometric, double expected, bool rescaled)
    {
        Assert.Equal(expected, ProfileSegment.NormalizeGrade(reported, geometric, out var r), 12);
        Assert.Equal(rescaled, r);
    }

    [Fact]
    public void Segments_give_curves_and_corners_in_station_order()
    {
        var segments = new List<ProfileSegment>
        {
            ProfileSegment.Tangent(1050, 11.35, 1200, 8.35),                  // −2 %
            Crest(),
            ProfileSegment.Tangent(0, -16.15, 950, 10.85),                    // +3 %
            ProfileSegment.Tangent(1200, 8.35, 1300, 8.35),                   // 0 %: corner at 1200 without a curve
            ProfileSegment.Tangent(1300, 8.35, 1400, 8.35),                   // same grade: no PVI
        };

        var curves = VerticalCurve.FromSegments(segments);

        Assert.Equal(2, curves.Count);
        Assert.True(curves[0].HasCurve);
        Assert.Equal(1000, curves[0].PviStation);
        Assert.False(curves[1].HasCurve);
        Assert.Equal("Đ2", curves[1].Name);
        Assert.Equal(1200, curves[1].PviStation);
        Assert.Equal(2, curves[1].A, 9);
    }

    [Fact]
    public void Box_text()
    {
        var lines = VerticalCurveBoxText.Build(VerticalCurve.FromSegment(Crest(), 1));

        Assert.Equal(new[] { "i1=+3.00%  i2=-2.00%", "R=2000  K=100", "T=50  E=0.63", "CĐ đỉnh=12.35", "Điểm cao=1+010.00  CĐ=11.75" }, lines);
    }

    [Fact]
    public void Box_text_ignores_windows_culture()
    {
        TestCulture.Run("vi-VN", () =>
            Assert.Equal("T=50  E=0.63", VerticalCurveBoxText.Build(VerticalCurve.FromSegment(Crest(), 1))[2]));
    }

    [Fact]
    public void Box_text_without_curve()
    {
        var lines = VerticalCurveBoxText.Build(VerticalCurve.Corner(100, 5.5, 0, -0.01, 3));

        Assert.Equal(new[] { "i1=0.00%  i2=-1.00%", "CĐ đỉnh=5.5" }, lines);
    }

    [Fact]
    public void Table_row()
    {
        var table = VerticalCurveTableBuilder.Build(new[] { VerticalCurve.FromSegment(Crest(), 1) }, c => "x");

        Assert.Equal(new[] { "Đỉnh", "Lý trình", "CĐ đỉnh", "i1 (%)", "i2 (%)", "A (%)", "R", "K", "T", "E", "Lý trình TĐ", "Lý trình TC", "Điểm cao/thấp", "Cảnh báo" },
            table.Headers);
        Assert.Equal(new[] { "Đ1", "1+000.00", "12.35", "+3.00", "-2.00", "5.00", "2000.00", "100.00", "50.00", "0.63", "0+950.00", "1+050.00", "1+010.00 / 11.75", "x" },
            table.Rows[0]);
    }
}

public class VerticalCurveRuleCheckerTests
{
    private static VerticalRules Rules() => new VerticalRules
    {
        MinRadiusCrest = { new SpeedValueRule { DesignSpeed = 60, Value = 2500 } },
        MinRadiusSag = { new SpeedValueRule { DesignSpeed = 60, Value = 1000 } },
        MinLength = { new SpeedValueRule { DesignSpeed = 60, Value = 50 } },
        MaxGrade = { new SpeedValueRule { DesignSpeed = 60, Value = 6 } },
    };

    private static VerticalCurve Curve(double gIn, double gOut, double length) =>
        VerticalCurve.FromSegment(ProfileSegment.Parabola(1000, 10, gIn, gOut, length), 1);

    [Fact]
    public void Empty_tables_give_no_issues()
    {
        Assert.Empty(VerticalCurveRuleChecker.Check(Curve(0.09, -0.02, 10), 60, new VerticalRules()));
        Assert.Empty(VerticalCurveRuleChecker.Check(Curve(0.09, -0.02, 10), 60, null));
    }

    [Fact]
    public void Crest_radius_below_rmin()
    {
        var issues = VerticalCurveRuleChecker.Check(Curve(0.03, -0.02, 100), 60, Rules());   // R 2000 < 2500

        var issue = Assert.Single(issues);
        Assert.Equal(VerticalCurveIssueCode.RadiusTooSmall, issue.Code);
        Assert.Equal("Đ1: R = 2000.00 m nhỏ hơn Rmin lồi = 2500.00 m (V = 60 km/h).", issue.Message);
    }

    [Fact]
    public void Sag_uses_the_sag_table()
    {
        Assert.Empty(VerticalCurveRuleChecker.Check(Curve(-0.02, 0.03, 100), 60, Rules()));   // R 2000 ≥ 1000
    }

    [Fact]
    public void Short_curve_and_steep_grade()
    {
        var issues = VerticalCurveRuleChecker.Check(Curve(-0.07, 0.03, 40), 60, Rules());   // sag R 400

        Assert.Equal(new[] { VerticalCurveIssueCode.RadiusTooSmall, VerticalCurveIssueCode.LengthTooShort, VerticalCurveIssueCode.GradeTooSteep },
            issues.Select(i => i.Code));
        Assert.Equal("Đ1: i1 = -7.00% dốc hơn imax = 6.00%.", issues[2].Message);
    }

    [Fact]
    public void Missing_speed_row_is_reported_once()
    {
        var issues = VerticalCurveRuleChecker.Check(Curve(0.03, -0.02, 100), 80, Rules());

        var issue = Assert.Single(issues);
        Assert.Equal(VerticalCurveIssueCode.NoRuleForSpeed, issue.Code);
        Assert.Equal("Preset chưa có Rmin lồi, Lmin, imax cho vận tốc 80 km/h.", issue.Message);
    }

    [Fact]
    public void Corner_without_curve_checks_grades_only()
    {
        var issues = VerticalCurveRuleChecker.Check(VerticalCurve.Corner(100, 5, 0.08, 0.01, 1), 60, Rules());

        Assert.Equal(VerticalCurveIssueCode.GradeTooSteep, Assert.Single(issues).Code);
    }
}

public class VerticalCurveSessionTests
{
    private static IReadOnlyList<ProfileSegment> Design() => new List<ProfileSegment>
    {
        ProfileSegment.Tangent(0, -16.15, 950, 10.85),
        ProfileSegment.Parabola(1000, 12.35, 0.03, -0.02, 100),
        ProfileSegment.Tangent(1050, 11.35, 1200, 8.35),
    };

    private static VerticalCurveSession Loaded(ProjectPreset preset = null)
    {
        var s = new VerticalCurveSession(preset ?? new ProjectPreset());
        s.SetSource("Trắc dọc PV1");
        s.SetProfiles(new[]
        {
            new KeyValuePair<string, IReadOnlyList<ProfileSegment>>("TK", Design()),
            new KeyValuePair<string, IReadOnlyList<ProfileSegment>>("TK2", new List<ProfileSegment>()),
            new KeyValuePair<string, IReadOnlyList<ProfileSegment>>("Loi", null),
        });
        return s;
    }

    [Fact]
    public void Defaults()
    {
        var s = new VerticalCurveSession(new ProjectPreset());

        Assert.Equal("chưa chọn", s.SourceText);
        Assert.True(s.WriteBox);
        Assert.False(s.WriteTable || s.WriteCsv || s.WriteXlsx);
        Assert.Equal(60, s.DesignSpeed);
        Assert.Contains(60.0, s.AvailableSpeeds);
        Assert.False(s.CanApply);
        Assert.Equal("Chưa chọn trắc dọc", s.SummaryText);
        Assert.Equal("Preset chưa có bảng cong đứng – không kiểm tra R, L, i", s.RminText);
    }

    [Fact]
    public void Profiles_fill_rows()
    {
        var s = Loaded();

        Assert.Equal("TK", s.Profile);
        var row = Assert.Single(s.Rows);
        Assert.Equal("2000.00", row.R);
        Assert.False(row.HasWarning);
        Assert.True(s.CanApply);
        Assert.Equal("1 đỉnh, 1 đường cong đứng", s.SummaryText);
    }

    [Fact]
    public void Other_profiles()
    {
        var s = Loaded();

        s.ProfileIndex = 1;
        Assert.Empty(s.Rows);
        Assert.False(s.CanApply);
        Assert.Equal("Trắc dọc TK2 không có đỉnh", s.SummaryText);

        s.ProfileIndex = 2;
        Assert.Equal("Không đọc được trắc dọc Loi", s.SummaryText);
    }

    [Fact]
    public void Speed_change_rechecks()
    {
        var preset = new ProjectPreset { DesignSpeed = 40 };
        preset.VerticalRules.MinRadiusCrest.Add(new SpeedValueRule { DesignSpeed = 60, Value = 2500 });
        preset.VerticalRules.MinRadiusCrest.Add(new SpeedValueRule { DesignSpeed = 40, Value = 700 });
        var s = Loaded(preset);

        Assert.Equal(new[] { 40.0, 60.0 }, s.AvailableSpeeds);
        Assert.False(s.Rows[0].HasWarning);
        Assert.Equal("Rmin lồi 700 m", s.RminText);

        s.DesignSpeed = 60;

        Assert.True(s.Rows[0].HasWarning);
        Assert.Equal(1, s.WarningCount);
        Assert.Equal("1 đỉnh, 1 đường cong đứng, 1 đỉnh có cảnh báo", s.SummaryText);
        Assert.Equal("Rmin lồi 2500 m", s.RminText);
    }

    [Fact]
    public void Needs_an_output()
    {
        var s = Loaded();

        s.WriteBox = false;

        Assert.False(s.CanApply);
        Assert.Equal("Chọn ít nhất một đầu ra", s.SummaryText);
        s.WriteXlsx = true;
        Assert.True(s.CanApply);
    }

    [Fact]
    public void Chosen_profile_survives_a_reload()
    {
        var s = Loaded();
        s.ProfileIndex = 1;

        s.SetProfiles(new[]
        {
            new KeyValuePair<string, IReadOnlyList<ProfileSegment>>("A", Design()),
            new KeyValuePair<string, IReadOnlyList<ProfileSegment>>("TK2", Design()),
        });

        Assert.Equal("TK2", s.Profile);
    }
}
