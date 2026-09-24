using System;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Core.Tables;
using C3DTools.Civil2021.Services;
using CivilSurface = Autodesk.Civil.DatabaseServices.Surface;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.ExportCommands))]

namespace C3DTools.Civil2021.Commands;

public sealed class ExportCommands
{
    [CommandMethod("C3DTOOLS", "CTEXPORT", CommandFlags.Modal)]
    public void ExportInventory()
    {
        if (!HostServices.TryGet(out _, out var editor, out var database, out var civil)) return;
        CommandRunner.Run(editor, () =>
        {
            var type = PromptType(editor);
            TableData table;
            using (var read = database.TransactionManager.StartOpenCloseTransaction())
            {
                table = type == "Alignment"
                    ? BuildAlignmentTable(read, civil)
                    : type == "Surface"
                        ? BuildSurfaceTable(read, civil)
                        : BuildNetworkTable(read, civil);
            }

            var safeType = HostServices.SanitizeFileName(type);
            var defaultCsv = Path.Combine(HostServices.DrawingFolder(database), safeType + "-INVENTORY.csv");
            var csvPath = HostServices.PromptCsvPath(editor, defaultCsv, optional: true);

            var pointOptions = new PromptPointOptions("\nĐiểm đặt bảng AutoCAD (Enter để chỉ xuất CSV): ")
            {
                AllowNone = true
            };
            var pointResult = editor.GetPoint(pointOptions);
            if (pointResult.Status != PromptStatus.OK)
            {
                if (!string.IsNullOrWhiteSpace(csvPath)) CsvExportService.Save(table, csvPath);
                editor.WriteMessage("\nĐã xuất CSV." + (string.IsNullOrWhiteSpace(csvPath) ? "" : ": " + csvPath));
                return;
            }

            using var transaction = database.TransactionManager.StartTransaction();
            TableBuilder.Add(transaction, database, table, pointResult.Value, "0");
            editor.UpdateScreen();
            if (!HostServices.Confirm(editor, "Giữ bảng AutoCAD?"))
            {
                transaction.Abort();
                if (!string.IsNullOrWhiteSpace(csvPath)) CsvExportService.Save(table, csvPath);
                editor.WriteMessage("\nĐã hủy bảng AutoCAD; CSV vẫn được giữ nếu đã chọn.");
                return;
            }
            transaction.Commit();
            if (!string.IsNullOrWhiteSpace(csvPath)) CsvExportService.Save(table, csvPath);
            editor.WriteMessage("\nĐã tạo bảng" + (string.IsNullOrWhiteSpace(csvPath) ? "." : " và CSV: " + csvPath));
        });
    }

    private static string PromptType(Editor editor)
    {
        var options = new PromptKeywordOptions("\nLoại dữ liệu [Alignment/Surface/PipeNetwork]: ")
        {
            AllowNone = false
        };
        HostServices.AddKeyword(options, "Alignment", "Alignment");
        HostServices.AddKeyword(options, "Surface", "Surface");
        HostServices.AddKeyword(options, "PipeNetwork", "PipeNetwork");
        options.Keywords.Default = "Alignment";
        var result = editor.GetKeywords(options);
        if (result.Status != PromptStatus.OK) throw new OperationCanceledException();
        return result.StringResult;
    }

    private static TableData BuildAlignmentTable(Transaction transaction, CivilDocument civil)
    {
        var table = new TableData("Tên", "Loại", "Chiều dài (m)", "Lý trình đầu", "Lý trình cuối");
        foreach (ObjectId id in civil.GetAlignmentIds())
        {
            var a = (Alignment)transaction.GetObject(id, OpenMode.ForRead);
            table.AddRow(a.Name, a.AlignmentType.ToString(),
                HostServices.Format(a.Length, 3),
                HostServices.Format(a.StartingStation, 3),
                HostServices.Format(a.EndingStation, 3));
        }
        EnsureRows(table, "Alignment");
        return table;
    }

    private static TableData BuildSurfaceTable(Transaction transaction, CivilDocument civil)
    {
        var table = new TableData("Tên", "Loại", "Trạng thái rebuild");
        foreach (ObjectId id in civil.GetSurfaceIds())
        {
            var surface = (CivilSurface)transaction.GetObject(id, OpenMode.ForRead);
            table.AddRow(surface.Name, surface.GetType().Name,
                surface.IsOutOfDate ? "Cần rebuild" : "OK");
        }
        EnsureRows(table, "Surface");
        return table;
    }

    private static TableData BuildNetworkTable(Transaction transaction, CivilDocument civil)
    {
        var table = new TableData("Tên", "Parts list", "Số Pipe", "Số Structure");
        foreach (ObjectId id in civil.GetPipeNetworkIds())
        {
            var network = (Network)transaction.GetObject(id, OpenMode.ForRead);
            table.AddRow(network.Name, network.PartsListName,
                network.GetPipeIds().Count.ToString(),
                network.GetStructureIds().Count.ToString());
        }
        EnsureRows(table, "PipeNetwork");
        return table;
    }

    private static void EnsureRows(TableData table, string type)
    {
        if (table.Rows.Count == 0) throw new InvalidOperationException($"Bản vẽ chưa có {type} để xuất.");
    }
}
