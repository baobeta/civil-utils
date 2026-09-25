using System;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Core.Drainage;
using C3DTools.Core.Tables;
using C3DTools.Civil2021.Services;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.DrainageCommands))]

namespace C3DTools.Civil2021.Commands;

public sealed class DrainageCommands
{
    [CommandMethod("C3DTOOLS", "CTCONG", CommandFlags.Modal)]
    public void CheckPipe()
    {
        if (!HostServices.TryGet(out _, out var editor, out var database, out _)) return;
        CommandRunner.Run(editor, () =>
        {
            PipeData pipeData;
            using (var read = database.TransactionManager.StartOpenCloseTransaction())
            {
                var pipe = HostServices.PromptEntity<Pipe>(read, editor, "Chọn Pipe cần kiểm tra");
                if (pipe == null) throw new OperationCanceledException();
                var centerline = HostServices.PromptYesNo(editor, "Z của StartPoint/EndPoint là cao độ tim cống?", true);
                pipeData = PipeAdapter.ToData(read, pipe, centerline);
            }

            var preset = PresetService.Load();
            if (preset.PipeRules == null)
                throw new InvalidOperationException("Chưa có quy tắc cống. Chạy CTCONFIG để thiết lập trước khi dùng CTCONG.");
            var minSlopePercent = preset.PipeRules.MinSlope * 100.0;
            var minCover = preset.PipeRules.MinCover;
            var result = PipeChecker.Check(pipeData, preset.PipeRules);
            foreach (var issue in result.Issues) editor.WriteMessage("\n" + issue.Message);
            var status = result.Issues.Count == 0 ? "Đạt" : "Không đạt: " + string.Join("; ", result.Issues.Select(x => x.Code.ToString()));
            var table = new TableData(
                "Pipe",
                "L (m)",
                "D trong (m)",
                "Tường (m)",
                "Độ dốc",
                "Độ dốc tối thiểu",
                "Chôn đầu (m)",
                "Chôn cuối (m)",
                "Kết quả");
            table.AddRow(
                pipeData.Name,
                HostServices.Format(pipeData.Length, 3),
                HostServices.Format(pipeData.InnerDiameter, 3),
                HostServices.Format(pipeData.WallThickness, 3),
                (result.Slope * 100.0).ToString("0.00") + "%",
                minSlopePercent.ToString("0.00") + "%",
                HostServices.Format(result.StartCover, 3),
                HostServices.Format(result.EndCover, 3),
                status);

            var insert = HostServices.PromptPoint(editor, "Điểm đặt bảng kiểm tra cống");
            var defaultCsv = Path.Combine(
                HostServices.DrawingFolder(database),
                HostServices.SanitizeFileName(pipeData.Name) + "-CTCONG.csv");
            var csvPath = HostServices.PromptCsvPath(editor, defaultCsv, optional: true);

            using var transaction = database.TransactionManager.StartTransaction();
            TableBuilder.Add(transaction, database, table, insert, "0");
            editor.UpdateScreen();
            if (!HostServices.Confirm(editor, "Giữ bảng kiểm tra?"))
            {
                transaction.Abort();
                return;
            }
            transaction.Commit();
            if (!string.IsNullOrWhiteSpace(csvPath)) CsvExportService.Save(table, csvPath);
            editor.WriteMessage($"\nKết quả: {status}" +
                (string.IsNullOrWhiteSpace(csvPath) ? "." : $"; CSV: {csvPath}"));
            editor.WriteMessage(" Một lệnh U hoàn tác bảng trong bản vẽ.");
        });
    }
}
