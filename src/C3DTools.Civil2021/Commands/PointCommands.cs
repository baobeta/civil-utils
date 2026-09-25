using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Core.Points;
using C3DTools.Civil2021.Services;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.PointCommands))]

namespace C3DTools.Civil2021.Commands;

public sealed class PointCommands
{
    [CommandMethod("C3DTOOLS", "CTDIEM", CommandFlags.Modal)]
    public void ImportPoints()
    {
        if (!HostServices.TryGet(out _, out var editor, out var database, out var civil)) return;
        CommandRunner.Run(editor, () =>
        {
            var fileOptions = new PromptOpenFileOptions("\nChọn file điểm CSV/TXT: ")
            {
                Filter = "Điểm (*.csv;*.txt)|*.csv;*.txt|Tất cả (*.*)|*.*"
            };
            var fileResult = editor.GetFileNameForOpen(fileOptions);
            if (fileResult.Status != PromptStatus.OK) throw new OperationCanceledException();

            var preset = PresetService.Load();
            var delimiter = PromptDelimiter(editor, preset.PointFile.Delimiter);
            var columns = HostServices.PromptText(editor, "Thứ tự cột", preset.PointFile.Columns, allowSpaces: false)
                .Replace(" ", "")
                .ToUpperInvariant();
            var lines = TextFileReader.ReadAllLines(fileResult.StringResult);
            var result = PointFileParser.Parse(lines, new PointFileOptions
            {
                Columns = columns,
                Delimiter = delimiter,
                MinNorthing = preset.PointFile.MinNorthing,
                MaxNorthing = preset.PointFile.MaxNorthing,
                MinEasting = preset.PointFile.MinEasting,
                MaxEasting = preset.PointFile.MaxEasting
            });

            foreach (var issue in result.Issues.Take(30))
                editor.WriteMessage($"\n{issue.Severity}: dòng {issue.Line}: {issue.Message}");
            if (result.Issues.Count > 30)
                editor.WriteMessage($"\n... và {result.Issues.Count - 30} cảnh báo/lỗi khác.");
            if (result.HasErrors)
                throw new InvalidOperationException("File có lỗi nên chưa thể nhập điểm.");
            if (result.Points.Count == 0)
                throw new InvalidOperationException("File không có điểm hợp lệ.");

            using var transaction = database.TransactionManager.StartTransaction();
            var duplicateNumbers = new HashSet<uint>();
            foreach (var point in result.Points)
            {
                var existing = civil.CogoPoints.GetPointByPointNumber(point.Number);
                if (!existing.IsNull && existing.IsValid)
                {
                    duplicateNumbers.Add(point.Number);
                    continue;
                }
                var location = new Point3d(
                    point.Easting,
                    point.Northing,
                    point.Elevation ?? 0.0);
                var id = civil.CogoPoints.Add(location, point.Description ?? "", false);
                var cogo = (CogoPoint)transaction.GetObject(id, OpenMode.ForWrite);
                cogo.PointNumber = point.Number;
                cogo.RawDescription = point.Description ?? "";
            }

            editor.WriteMessage($"\nPreview: thêm {result.Points.Count - duplicateNumbers.Count} COGO point, " +
                $"bỏ qua {duplicateNumbers.Count} số trùng.");
            editor.UpdateScreen();
            if (!HostServices.Confirm(editor, "Nhập các điểm đã kiểm tra?"))
            {
                transaction.Abort();
                return;
            }
            transaction.Commit();
            editor.WriteMessage($"\nĐã nhập {result.Points.Count - duplicateNumbers.Count} COGO point. Một lệnh U hoàn tác toàn bộ.");
        });
    }

    private static char PromptDelimiter(Editor editor, char defaultDelimiter)
    {
        var options = new PromptKeywordOptions("\nDấu phân cách [Comma/Semicolon/Tab]: ")
        {
            AllowNone = false
        };
        HostServices.AddKeyword(options, "Comma", "Comma");
        HostServices.AddKeyword(options, "Semicolon", "Semicolon");
        HostServices.AddKeyword(options, "Tab", "Tab");
        options.Keywords.Default = defaultDelimiter == ';' ? "Semicolon" : defaultDelimiter == '\t' ? "Tab" : "Comma";
        var result = editor.GetKeywords(options);
        if (result.Status != PromptStatus.OK) throw new OperationCanceledException();
        if (result.StringResult.Equals("Semicolon", StringComparison.OrdinalIgnoreCase)) return ';';
        if (result.StringResult.Equals("Tab", StringComparison.OrdinalIgnoreCase)) return '\t';
        return ',';
    }
}
