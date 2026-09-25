using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Core.Stations;
using C3DTools.Civil2021.Services;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.StationCommands))]

namespace C3DTools.Civil2021.Commands;

public sealed class StationCommands
{
    [CommandMethod("C3DTOOLS", "CTNHAC", CommandFlags.Modal)]
    public void CreateStationLabels()
    {
        if (!HostServices.TryGet(out _, out var editor, out var database, out _)) return;
        CommandRunner.Run(editor, () =>
        {
            using var transaction = database.TransactionManager.StartTransaction();
            var alignmentId = HostServices.PromptEntityId(editor, typeof(Alignment), "Chọn Alignment");
            if (alignmentId.IsNull) throw new OperationCanceledException();
            var alignment = (Alignment)transaction.GetObject(alignmentId, OpenMode.ForRead);
            var stationDecimals = PresetService.Load().StationDecimals;
            var start = HostServices.PromptDouble(editor, "Lý trình đầu", alignment.StartingStation, false, false);
            var end = HostServices.PromptDouble(editor, "Lý trình cuối", alignment.EndingStation, false, false);
            if (end < start) (start, end) = (end, start);
            if (!(end > start)) throw new InvalidOperationException("Lý trình cuối phải lớn hơn lý trình đầu.");
            var interval = HostServices.PromptDouble(editor, "Khoảng lý trình", 20, false, false);
            var offset = HostServices.PromptDouble(editor, "Offset nhãn", 2.0, true, true);
            var height = HostServices.PromptDouble(editor, "Chiều cao chữ", 1.5, false, false);
            var count = (int)Math.Ceiling((end - start) / interval) + 1;
            if (count > 501) throw new InvalidOperationException("Quá nhiều nhãn; hãy tăng khoảng lý trình.");
            var textStyleId = HostServices.TextStyleId(database);
            var modelSpace = (BlockTableRecord)transaction.GetObject(
                SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);

            for (var i = 0; i < count; i++)
            {
                var station = Math.Min(end, start + i * interval);
                double x = 0;
                double y = 0;
                double nextX = 0;
                double nextY = 0;
                alignment.PointLocation(station, offset, ref x, ref y);
                alignment.PointLocation(Math.Min(end, station + Math.Max(0.01, interval / 100.0)),
                    offset, ref nextX, ref nextY);
                var text = new DBText
                {
                    Position = new Point3d(x, y, height),
                    TextString = StationFormatter.Format(station, stationDecimals),
                    Height = height,
                    TextStyleId = textStyleId,
                    LayerId = alignment.LayerId,
                    Rotation = Math.Atan2(nextY - y, nextX - x),
                    Normal = Vector3d.ZAxis
                };
                modelSpace.AppendEntity(text);
                transaction.AddNewlyCreatedDBObject(text, true);
            }

            editor.WriteMessage($"\nPreview: tạo {count} nhãn lý trình theo Alignment.");
            editor.UpdateScreen();
            if (!HostServices.Confirm(editor, "Giữ các nhãn?"))
            {
                transaction.Abort();
                return;
            }
            transaction.Commit();
            editor.WriteMessage($"\nĐã tạo {count} nhãn. Một lệnh U hoàn tác toàn bộ.");
        });
    }

    [CommandMethod("C3DTOOLS", "CTCOC", CommandFlags.Modal)]
    public void CreateSampleLines()
    {
        if (!HostServices.TryGet(out _, out var editor, out var database, out _)) return;
        CommandRunner.Run(editor, () =>
        {
            using var transaction = database.TransactionManager.StartTransaction();
            var alignmentId = HostServices.PromptEntityId(editor, typeof(Alignment), "Chọn Alignment");
            if (alignmentId.IsNull) throw new OperationCanceledException();
            var alignment = (Alignment)transaction.GetObject(alignmentId, OpenMode.ForRead);
            var start = HostServices.PromptDouble(editor, "Lý trình đầu", alignment.StartingStation, false, false);
            var end = HostServices.PromptDouble(editor, "Lý trình cuối", alignment.EndingStation, false, false);
            if (end < start) (start, end) = (end, start);
            var interval = HostServices.PromptDouble(editor, "Khoảng lý trình", 20, false, false);
            var halfWidth = HostServices.PromptDouble(editor, "Nửa chiều rộng cắt ngang", 10, false, false);
            var desired = alignment.Name + "-COC";
            var groupName = HostServices.PromptText(editor, "Tên Sample Line Group", desired);
            var planned = StationPlanner.Build(start, end, interval, null);
            if (planned.Count > 201) throw new InvalidOperationException("Quá nhiều cọc; hãy tăng khoảng lý trình.");

            var finalGroupName = HostServices.UniqueName(groupName,
                candidate => SampleLineGroupExists(transaction, alignment, candidate));
            var groupId = SampleLineGroup.Create(finalGroupName, alignmentId);
            foreach (var station in planned)
            {
                var points = new Point2dCollection();
                double east = 0;
                double north = 0;
                alignment.PointLocation(station.Station, -halfWidth, ref east, ref north);
                points.Add(new Point2d(east, north));
                alignment.PointLocation(station.Station, halfWidth, ref east, ref north);
                points.Add(new Point2d(east, north));
                SampleLine.Create(
                    finalGroupName + "-" + station.Station.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                    groupId,
                    points);
            }

            editor.WriteMessage($"\nPreview: tạo {planned.Count} Sample Line trong '{finalGroupName}'.");
            editor.UpdateScreen();
            if (!HostServices.Confirm(editor, "Giữ Sample Lines?"))
            {
                transaction.Abort();
                return;
            }
            transaction.Commit();
            editor.WriteMessage($"\nĐã tạo {planned.Count} Sample Line. Một lệnh U hoàn tác toàn bộ.");
        });
    }

    private static bool SampleLineGroupExists(Transaction transaction, Alignment alignment, string name) =>
        alignment.GetSampleLineGroupIds().Cast<ObjectId>().Any(id =>
        {
            var group = transaction.GetObject(id, OpenMode.ForRead) as SampleLineGroup;
            return group != null && group.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase);
        });
}
