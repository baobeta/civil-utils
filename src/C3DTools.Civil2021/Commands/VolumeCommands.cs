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
using C3DTools.Core.Volumes;
using C3DTools.Civil2021.Services;
using CivilSurface = Autodesk.Civil.DatabaseServices.Surface;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.VolumeCommands))]

namespace C3DTools.Civil2021.Commands;

public sealed class VolumeCommands
{
    [CommandMethod("C3DTOOLS", "CTKHOILUONG", CommandFlags.Modal)]
    public void ComputeVolumes()
    {
        if (!HostServices.TryGet(out _, out var editor, out var database, out var civil)) return;
        CommandRunner.Run(editor, () =>
        {
            ObjectId alignmentId;
            double alignmentStart;
            double alignmentEnd;
            string alignmentName;
            ObjectId baseSurfaceId;
            ObjectId designSurfaceId;
            string baseSurfaceName;
            string designSurfaceName;
            using (var read = database.TransactionManager.StartOpenCloseTransaction())
            {
                alignmentId = HostServices.PromptEntityId(editor, typeof(Alignment), "Chọn Alignment làm trục cắt ngang");
                if (alignmentId.IsNull) throw new OperationCanceledException();
                var alignment = (Alignment)read.GetObject(alignmentId, OpenMode.ForRead);
                alignmentStart = alignment.StartingStation;
                alignmentEnd = alignment.EndingStation;
                alignmentName = alignment.Name;
                baseSurfaceId = HostServices.PromptNamedObject(editor, read, "mặt bằng hiện trạng", civil.GetSurfaceIds());
                designSurfaceId = HostServices.PromptNamedObject(editor, read, "mặt bằng thiết kế", civil.GetSurfaceIds());
                if (baseSurfaceId == designSurfaceId)
                    throw new InvalidOperationException("Hai mặt bằng phải khác nhau.");
                baseSurfaceName = HostServices.ReadObjectName(read.GetObject(baseSurfaceId, OpenMode.ForRead));
                designSurfaceName = HostServices.ReadObjectName(read.GetObject(designSurfaceId, OpenMode.ForRead));
            }

            var start = HostServices.PromptDouble(editor, "Lý trình đầu", alignmentStart, false, false);
            var end = HostServices.PromptDouble(editor, "Lý trình cuối", alignmentEnd, false, false);
            if (end < start) (start, end) = (end, start);
            var interval = HostServices.PromptDouble(editor, "Khoảng cắt ngang", 20, false, false);
            var halfWidth = HostServices.PromptDouble(editor, "Nửa chiều rộng cắt ngang", 10, false, false);
            var sampleSpacing = HostServices.PromptDouble(editor, "Khoảng lấy mẫu ngang", 1.0, false, false);
            var insert = HostServices.PromptPoint(editor, "Điểm đặt bảng khối lượng");
            var defaultCsv = Path.Combine(
                HostServices.DrawingFolder(database),
                HostServices.SanitizeFileName(alignmentName) + "-KHOILUONG.csv");
            var csvPath = HostServices.PromptCsvPath(editor, defaultCsv, optional: true);

            VolumeSamplingResult sampling;
            using (var read = database.TransactionManager.StartOpenCloseTransaction())
            {
                sampling = VolumeSamplingService.Sample(
                    (Alignment)read.GetObject(alignmentId, OpenMode.ForRead),
                    (CivilSurface)read.GetObject(baseSurfaceId, OpenMode.ForRead),
                    (CivilSurface)read.GetObject(designSurfaceId, OpenMode.ForRead),
                    start,
                    end,
                    interval,
                    halfWidth,
                    sampleSpacing);
            }
            var volumes = AverageEndArea.Compute(sampling.Sections);
            var volumeDecimals = PresetService.Load().VolumeDecimals;
            var table = BuildTable(volumes, volumeDecimals);

            using var transaction = database.TransactionManager.StartTransaction();
            TableBuilder.Add(transaction, database, table, insert, "0");
            editor.WriteMessage($"\nPreview: {volumes.Count} cắt ngang, " +
                $"tổng cắt {HostServices.Format(volumes.Last().CumulativeCut, volumeDecimals)} m3, " +
                $"tổng đắp {HostServices.Format(volumes.Last().CumulativeFill, volumeDecimals)} m3.");
            if (sampling.SkippedStations.Count > 0)
                editor.WriteMessage($"\nBỏ qua {sampling.SkippedStations.Count} cắt ngang ngoài phạm vi mặt bằng.");
            editor.UpdateScreen();
            if (!HostServices.Confirm(editor, "Giữ bảng khối lượng?"))
            {
                transaction.Abort();
                return;
            }
            transaction.Commit();
            if (!string.IsNullOrWhiteSpace(csvPath)) CsvExportService.Save(table, csvPath);
            editor.WriteMessage($"\nĐã tạo bảng khối lượng" +
                (string.IsNullOrWhiteSpace(csvPath) ? "." : $"; CSV: {csvPath}"));
            editor.WriteMessage(" Một lệnh U hoàn tác bảng trong bản vẽ.");
        });
    }

    private static TableData BuildTable(
        System.Collections.Generic.IReadOnlyList<VolumeRow> rows,
        int volumeDecimals)
    {
        var table = new TableData(
            "Lý trình",
            "Diện tích cắt (m2)",
            "Diện tích đắp (m2)",
            "Cắt lũy kế (m3)",
            "Đắp lũy kế (m3)");
        foreach (var row in rows)
        {
            table.AddRow(
                HostServices.Format(row.Station, 3),
                HostServices.Format(row.CutArea, 3),
                HostServices.Format(row.FillArea, 3),
                HostServices.Format(row.CumulativeCut, volumeDecimals),
                HostServices.Format(row.CumulativeFill, volumeDecimals));
        }
        return table;
    }
}
