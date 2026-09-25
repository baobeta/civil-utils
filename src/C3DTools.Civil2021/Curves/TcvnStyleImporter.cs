using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.Settings;

namespace C3DTools.Civil2021.Curves;

/// <summary>
/// CTYTCMAU: the TCVN curve label style, alignment label set and geometry-point abbreviations.
/// Taken from Resources/C3DTools-TCVN.dwg when it ships with the add-in (S4), otherwise built in code (S5).
/// Runs inside the caller's transaction; every Civil 3D call that is not runtime-verified is guarded.
/// </summary>
internal static class TcvnStyleImporter
{
    public const string CurveLabelStyleName = "YTC_TCVN";
    public const string LabelSetName = "TCVN_Tuyen";
    public const string CurveTableStyleName = "YTC_TCVN";
    private const string TemplateFile = "C3DTools-TCVN.dwg";
    private const string TextComponentName = "YTC";
    private const double PlanReadableBiasDegrees = 110;

    /// <summary>
    /// Simplest property-field form; the exact format codes (DMS, 0.01) are version-specific and not verified
    /// on 2021, so the user refines them in the style editor (Label Style Composer).
    /// </summary>
    private const string CurveLabelContents =
        "A=<[Curve Delta Angle]>  P=<[Curve External Secant]>\\PR=<[Curve Radius]>  K=<[Curve Length]>\\PT=<[Curve Tangent]>";

    /// <summary>Abbreviation settings: PC→TĐ, PT→TC, TS→NĐ, ST→NC, SC→TĐ, CS→TC, PI→Đ, midpoint→P.</summary>
    private static readonly (AbbreviationAlignmentType type, string code, string text)[] Abbreviations =
    {
        (AbbreviationAlignmentType.TangentCurveIntersect, "PC", "TĐ"),
        (AbbreviationAlignmentType.CurveTangentIntersect, "PT", "TC"),
        (AbbreviationAlignmentType.TangentSpiralIntersect, "TS", "NĐ"),
        (AbbreviationAlignmentType.SpiralTangentIntersect, "ST", "NC"),
        (AbbreviationAlignmentType.SpiralCurveIntersect, "SC", "TĐ"),
        (AbbreviationAlignmentType.CurveSpiralIntersect, "CS", "TC"),
        (AbbreviationAlignmentType.TangentTangentIntersect, "PI", "Đ"),
        (AbbreviationAlignmentType.MidCurvePoint, "MP", "P"),
    };

    public const string ManualSteps =
        "Làm thủ công: Settings > Drawing Settings > Abbreviations > Alignment Geometry Point Text: " +
        "PC=TĐ, PT=TC, TS=NĐ, ST=NC, SC=TĐ, CS=TC, PI=Đ, Mid-Curve Point=P.";

    /// <summary>The template shipped next to the DLL, or null.</summary>
    public static string TemplatePath()
    {
        try
        {
            var dllDir = Path.GetDirectoryName(typeof(TcvnStyleImporter).Assembly.Location);
            var path = Path.Combine(dllDir ?? "", "Resources", TemplateFile);
            return File.Exists(path) ? path : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>True when the curve label style and the label set are both in the drawing.</summary>
    public static bool IsInstalled(CivilDocument civil) =>
        CurveLabelStyles(civil).Contains(CurveLabelStyleName) && LabelSets(civil).Contains(LabelSetName);

    /// <summary>"name (kind)" of each style, split into already in the drawing and missing.</summary>
    public static void Describe(CivilDocument civil, List<string> existing, List<string> missing)
    {
        void Check(StyleCollectionBase styles, string name, string kind) =>
            (styles.Contains(name) ? existing : missing).Add($"{name} ({kind})");

        Check(CurveLabelStyles(civil), CurveLabelStyleName, "nhãn đường cong");
        Check(LabelSets(civil), LabelSetName, "bộ nhãn Alignment");
        if (TemplatePath() != null) Check(civil.Styles.TableStyles.AlignmentCurveTableStyles, CurveTableStyleName, "bảng đường cong");
    }

    /// <summary>
    /// Adds the styles (overwrite: also rewrites those that exist) and sets the abbreviations, in the open transaction.
    /// Returns false when nothing could be written; say receives one line per step.
    /// </summary>
    public static bool Import(Transaction tr, Database db, bool overwrite, Action<string> say)
    {
        var civil = CivilDocument.GetCivilDocument(db);
        var ok = false;
        var template = TemplatePath();
        if (template != null) ok = ImportTemplate(template, db, overwrite, say);
        if (!ok) ok = BuildInCode(tr, civil, overwrite, say);
        SetAbbreviations(civil, say);
        return ok;
    }

    private static LabelStyleCollection CurveLabelStyles(CivilDocument civil) =>
        civil.Styles.LabelStyles.AlignmentLabelStyles.CurveLabelStyles;

    private static AlignmentLabelSetStyleCollection LabelSets(CivilDocument civil) =>
        civil.Styles.LabelSetStyles.AlignmentLabelSetStyles;

    /// <summary>S4: side-loads the template and exports its styles with StyleBase.ExportTo.</summary>
    private static bool ImportTemplate(string path, Database target, bool overwrite, Action<string> say)
    {
        var resolver = overwrite ? StyleConflictResolverType.Override : StyleConflictResolverType.Ignore;
        try
        {
            using (var source = new Database(false, true))
            {
                source.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, true, null);
                using (var tr = source.TransactionManager.StartTransaction())
                {
                    var civil = CivilDocument.GetCivilDocument(source);
                    var found = 0;
                    found += Export(tr, CurveLabelStyles(civil), CurveLabelStyleName, target, resolver, say);
                    found += Export(tr, LabelSets(civil), LabelSetName, target, resolver, say);
                    found += Export(tr, civil.Styles.TableStyles.AlignmentCurveTableStyles, CurveTableStyleName, target, resolver, say);
                    tr.Commit();
                    if (found > 0) return true;
                }
            }

            say($"Mẫu {path} không có kiểu {CurveLabelStyleName}/{LabelSetName}: tạo kiểu bằng mã.");
        }
        catch (Exception ex)
        {
            say($"Không nhập được kiểu từ mẫu {path} ({ex.Message}): tạo kiểu bằng mã.");
        }

        return false;
    }

    private static int Export(Transaction tr, StyleCollectionBase styles, string name, Database target,
        StyleConflictResolverType resolver, Action<string> say)
    {
        if (!styles.Contains(name)) return 0;
        var style = (StyleBase)tr.GetObject(styles[name], OpenMode.ForRead);
        style.ExportTo(target, resolver);
        say($"Đã nhập kiểu {name} từ mẫu.");
        return 1;
    }

    /// <summary>S5: curve label style with a bordered text component, and the label set that uses it.</summary>
    private static bool BuildInCode(Transaction tr, CivilDocument civil, bool overwrite, Action<string> say)
    {
        ObjectId curveStyleId;
        try
        {
            var styles = CurveLabelStyles(civil);
            var exists = styles.Contains(CurveLabelStyleName);
            curveStyleId = exists ? styles[CurveLabelStyleName] : styles.Add(CurveLabelStyleName);
            if (!exists || overwrite)
            {
                ConfigureCurveLabel((LabelStyle)tr.GetObject(curveStyleId, OpenMode.ForWrite), tr, say);
                say(exists ? $"Đã ghi đè kiểu nhãn {CurveLabelStyleName}." : $"Đã tạo kiểu nhãn {CurveLabelStyleName}.");
            }
        }
        catch (Exception ex)
        {
            say($"Không tạo được kiểu nhãn đường cong {CurveLabelStyleName}: {ex.Message}");
            return false;
        }

        try
        {
            var sets = LabelSets(civil);
            var exists = sets.Contains(LabelSetName);
            if (exists && !overwrite) return true;
            var setId = exists ? sets[LabelSetName] : sets.Add(LabelSetName);
            var set = (AlignmentLabelSetStyle)tr.GetObject(setId, OpenMode.ForWrite);
            for (var i = set.Count - 1; i >= 0; i--) set.RemoveAt(i);
            AddToSet(set, curveStyleId, LabelStyleType.AlignmentCurve, CurveLabelStyleName, say);
            var geometryPoint = GeometryPointStyle(tr, civil);
            if (geometryPoint.id.IsNull)
                say("Bản vẽ không có kiểu nhãn điểm hình học (Geometry Point): thêm Major Geometry Points vào bộ nhãn bằng tay.");
            else
                AddToSet(set, geometryPoint.id, LabelStyleType.AlignmentGeometryPoint, geometryPoint.name, say);
            say(exists ? $"Đã ghi đè bộ nhãn {LabelSetName}." : $"Đã tạo bộ nhãn {LabelSetName}.");
        }
        catch (Exception ex)
        {
            say($"Không tạo được bộ nhãn {LabelSetName}: {ex.Message}. Tạo bằng tay: Alignment Label Set, " +
                $"thêm Geometry Points (Major Geometry Points) và Curves với kiểu {CurveLabelStyleName}.");
        }

        return true;
    }

    private static void ConfigureCurveLabel(LabelStyle style, Transaction tr, Action<string> say)
    {
        ObjectId componentId = ObjectId.Null;
        foreach (ObjectId id in style.GetComponents(LabelStyleComponentType.Text))
            if (((LabelStyleComponent)tr.GetObject(id, OpenMode.ForRead)).Name == TextComponentName) componentId = id;
        if (componentId.IsNull) componentId = style.AddComponent(TextComponentName, LabelStyleComponentType.Text);
        var text = (LabelStyleTextComponent)tr.GetObject(componentId, OpenMode.ForWrite);

        Try(say, "nội dung nhãn", () => text.Text.Contents.Value = CurveLabelContents);
        Try(say, "khung chữ nhật", () =>
        {
            text.Border.Visible.Value = true;
            text.Border.BorderType.Value = TextBorderType.Rectangular;
        });
        Try(say, "Plan Readable", () =>
        {
            style.Properties.PlanReadability.PlanReadable.Value = true;
            style.Properties.PlanReadability.PlanReadableBias.Value = PlanReadableBiasDegrees * Math.PI / 180;
        });
    }

    private static void AddToSet(AlignmentLabelSetStyle set, ObjectId styleId, LabelStyleType type, string name, Action<string> say)
    {
        try
        {
            set.Add(styleId);
        }
        catch (Exception)
        {
            try
            {
                set.Add(type, name);
            }
            catch (Exception ex)
            {
                say($"Không thêm được {name} vào bộ nhãn {LabelSetName}: {ex.Message}");
            }
        }
    }

    /// <summary>A geometry point label style for "Major Geometry Points": one with that name, else the first.</summary>
    private static (ObjectId id, string name) GeometryPointStyle(Transaction tr, CivilDocument civil)
    {
        var styles = civil.Styles.LabelStyles.AlignmentLabelStyles.GeometryPointLabelStyles;
        var named = new List<(ObjectId id, string name)>();
        foreach (ObjectId id in styles) named.Add((id, ((StyleBase)tr.GetObject(id, OpenMode.ForRead)).Name));
        var major = named.FirstOrDefault(s => s.name.IndexOf("Major", StringComparison.OrdinalIgnoreCase) >= 0);
        return major.id.IsNull ? named.FirstOrDefault() : major;
    }

    private static void SetAbbreviations(CivilDocument civil, Action<string> say)
    {
        SettingsAbbreviationAlignment settings;
        try
        {
            settings = civil.Settings.DrawingSettings.AbbreviationsSettings.AlignmentGeoPointText;
        }
        catch (Exception ex)
        {
            say($"Không đọc được thiết lập viết tắt ({ex.Message}). {ManualSteps}");
            return;
        }

        var failed = new List<string>();
        foreach (var (type, code, text) in Abbreviations)
        {
            try
            {
                settings.SetAlignmentAbbreviation(type, text);
            }
            catch (Exception)
            {
                failed.Add(code);
            }
        }

        if (failed.Count == 0) say("Đã đặt viết tắt điểm hình học: PC=TĐ, PT=TC, TS=NĐ, ST=NC, SC=TĐ, CS=TC, PI=Đ, MP=P.");
        else say($"Không đặt được viết tắt {string.Join(", ", failed)}. {ManualSteps}");
    }

    private static void Try(Action<string> say, string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            say($"Không đặt được {what} cho kiểu nhãn {CurveLabelStyleName} ({ex.Message}); chỉnh trong Label Style Composer.");
        }
    }
}
