using System.Collections.Generic;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using C3DTools.Civil2021.Curves;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.CurveStylesCommand))]

namespace C3DTools.Civil2021.Commands;

public class CurveStylesCommand
{
    /// <summary>CTYTCMAU: TCVN curve label style, alignment label set and geometry-point abbreviations, as one undo.</summary>
    [CommandMethod("C3DTOOLS", "CTYTCMAU", CommandFlags.Modal)]
    public void CurveStyles()
    {
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        var db = doc.Database;

        var existing = new List<string>();
        var missing = new List<string>();
        TcvnStyleImporter.Describe(CivilDocument.GetCivilDocument(db), existing, missing);
        ed.WriteMessage(TcvnStyleImporter.TemplatePath() == null
            ? "\nKhông có mẫu Resources\\C3DTools-TCVN.dwg: kiểu sẽ được tạo bằng mã."
            : $"\nMẫu: {TcvnStyleImporter.TemplatePath()}");
        ed.WriteMessage($"\nSẽ thêm: {List(missing)}; đã có (Ghi de sẽ ghi đè): {List(existing)}.");
        ed.WriteMessage("\nViết tắt điểm hình học (PC=TĐ, PT=TC, TS=NĐ, ST=NC, SC=TĐ, CS=TC, PI=Đ, MP=P) được đặt ở cả hai lựa chọn.");

        var options = new PromptKeywordOptions("\nNhập kiểu TCVN [ThemMoi/GhiDe/Huy]", "ThemMoi GhiDe Huy") { AllowNone = true };
        options.Keywords.Default = "ThemMoi";
        var answer = ed.GetKeywords(options);
        if (answer.Status != PromptStatus.OK && answer.Status != PromptStatus.None) return;
        var choice = answer.Status == PromptStatus.None ? "ThemMoi" : answer.StringResult;
        if (choice == "Huy") return;

        using (doc.LockDocument())
        using (var tr = db.TransactionManager.StartTransaction())
        {
            try
            {
                if (!TcvnStyleImporter.Import(tr, db, choice == "GhiDe", m => ed.WriteMessage("\n" + m)))
                {
                    ed.WriteMessage("\nKhông nhập được kiểu TCVN. Bản vẽ không thay đổi.");
                    return;   // disposing without Commit aborts
                }

                tr.Commit();
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nLỗi khi nhập kiểu TCVN: {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
                return;
            }
        }

        ed.WriteMessage("\nHoàn thành CTYTCMAU. Định dạng số (độ phút giây, 0.01) chỉnh thêm trong Label Style Composer nếu cần.\n");
    }

    private static string List(List<string> items) => items.Count == 0 ? "không có" : string.Join(", ", items);
}
