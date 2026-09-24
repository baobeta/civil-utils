using System;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Services;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.AlignmentCommands))]

namespace C3DTools.Civil2021.Commands;

public sealed class AlignmentCommands
{
    [CommandMethod("C3DTOOLS", "CTALIGN", CommandFlags.Modal)]
    public void CreateAlignmentFromPolyline()
    {
        if (!HostServices.TryGet(out _, out var editor, out var database, out var civil)) return;
        CommandRunner.Run(editor, () =>
        {
            using var transaction = database.TransactionManager.StartTransaction();
            var polylineId = HostServices.PromptEntityId(editor, typeof(Polyline), "Chọn LWPOLYLINE làm tim tuyến");
            if (polylineId.IsNull) throw new OperationCanceledException();
            var polyline = (Polyline)transaction.GetObject(polylineId, OpenMode.ForRead);
            if (polyline.NumberOfVertices < 2)
                throw new InvalidOperationException("Polyline phải có ít nhất hai đỉnh.");
            if (polyline.Closed)
                throw new InvalidOperationException("Polyline đang khép kín; hãy mở polyline trước khi tạo Alignment.");

            var desired = HostServices.PromptText(editor, "Tên Alignment", polyline.Layer);
            var reverse = HostServices.PromptYesNo(editor, "Đảo chiều polyline?", false);
            var name = HostServices.UniqueName(desired, candidate => AlignmentExists(civil, transaction, candidate));
            var styleId = HostServices.FirstId(civil.Styles.AlignmentStyles);
            var labelSetId = HostServices.FirstId(civil.Styles.LabelSetStyles.AlignmentLabelSetStyles);
            if (styleId.IsNull || labelSetId.IsNull)
                throw new InvalidOperationException("Bản vẽ thiếu Alignment Style hoặc Label Set.");

            var sourceId = reverse ? AddReversedPolyline(transaction, database, polyline) : polylineId;
            var options = new PolylineOptions
            {
                PlineId = sourceId,
                AddCurvesBetweenTangents = false,
                EraseExistingEntities = reverse
            };
            var alignmentId = Alignment.Create(
                civil,
                options,
                name,
                ObjectId.Null,
                polyline.LayerId,
                styleId,
                labelSetId);
            editor.WriteMessage($"\nPreview: Alignment '{name}' được tạo theo thứ tự đỉnh của polyline.");
            editor.UpdateScreen();
            if (!HostServices.Confirm(editor, "Giữ Alignment?"))
            {
                transaction.Abort();
                return;
            }
            transaction.Commit();
            editor.WriteMessage($"\nĐã tạo Alignment: {name}. Một lệnh U hoàn tác toàn bộ.");
        });
    }

    [CommandMethod("C3DTOOLS", "CTPROFILE", CommandFlags.Modal)]
    public void CreateProfileFromSurface()
    {
        if (!HostServices.TryGet(out _, out var editor, out var database, out var civil)) return;
        CommandRunner.Run(editor, () =>
        {
            ObjectId alignmentId;
            string alignmentName;
            ObjectId surfaceId;
            string surfaceName;
            using (var read = database.TransactionManager.StartOpenCloseTransaction())
            {
                alignmentId = HostServices.PromptEntityId(editor, typeof(Alignment), "Chọn Alignment");
                if (alignmentId.IsNull) throw new OperationCanceledException();
                var selectedAlignment = (Alignment)read.GetObject(alignmentId, OpenMode.ForRead);
                alignmentName = selectedAlignment.Name;
                surfaceId = HostServices.PromptNamedObject(editor, read, "mặt bằng", civil.GetSurfaceIds());
                surfaceName = HostServices.ReadObjectName(read.GetObject(surfaceId, OpenMode.ForRead));
            }

            var desired = alignmentName + "-" + surfaceName + "-PROFILE";
            var profileName = HostServices.PromptText(editor, "Tên Profile", desired);
            var insert = HostServices.PromptPoint(editor, "Điểm đặt Profile View");
            var profileStyleId = HostServices.FirstId(civil.Styles.ProfileStyles);
            var profileLabelSetId = HostServices.FirstId(civil.Styles.LabelSetStyles.ProfileLabelSetStyles);
            var bandSetId = HostServices.FirstId(civil.Styles.ProfileViewBandSetStyles);
            var viewStyleId = HostServices.FirstId(civil.Styles.ProfileViewStyles);
            if (profileStyleId.IsNull || profileLabelSetId.IsNull || bandSetId.IsNull || viewStyleId.IsNull)
                throw new InvalidOperationException("Bản vẽ thiếu Profile/View style hoặc profile label set.");

            using var transaction = database.TransactionManager.StartTransaction();
            var writeAlignment = (Alignment)transaction.GetObject(alignmentId, OpenMode.ForRead);
            var finalName = HostServices.UniqueName(profileName,
                candidate => ProfileExists(transaction, writeAlignment, candidate));
            var profileId = Profile.CreateFromSurface(
                finalName,
                alignmentId,
                surfaceId,
                writeAlignment.LayerId,
                profileStyleId,
                profileLabelSetId);
            ProfileView.Create(
                alignmentId,
                insert,
                finalName + "-VIEW",
                bandSetId,
                viewStyleId);
            editor.WriteMessage($"\nPreview: Profile '{finalName}' và Profile View sẽ được tạo.");
            editor.UpdateScreen();
            if (!HostServices.Confirm(editor, "Giữ Profile và Profile View?"))
            {
                transaction.Abort();
                return;
            }
            transaction.Commit();
            editor.WriteMessage($"\nĐã tạo Profile '{finalName}' và Profile View. Một lệnh U hoàn tác toàn bộ.");
        });
    }

    private static ObjectId AddReversedPolyline(Transaction transaction, Database database, Polyline source)
    {
        var reversed = new Polyline
        {
            Elevation = source.Elevation,
            Normal = source.Normal,
            Thickness = source.Thickness,
            LayerId = source.LayerId
        };
        var count = source.NumberOfVertices;
        for (var sourceIndex = count - 1; sourceIndex >= 0; sourceIndex--)
        {
            var point = source.GetPoint3dAt(sourceIndex);
            var bulge = sourceIndex > 0 ? source.GetBulgeAt(sourceIndex - 1) : 0.0;
            reversed.AddVertexAt(count - 1 - sourceIndex, new Point2d(point.X, point.Y), 0, 0, bulge);
        }
        var modelSpace = (BlockTableRecord)transaction.GetObject(
            SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        modelSpace.AppendEntity(reversed);
        transaction.AddNewlyCreatedDBObject(reversed, true);
        return reversed.ObjectId;
    }

    private static bool AlignmentExists(CivilDocument civil, Transaction transaction, string name)
    {
        foreach (ObjectId id in civil.GetAlignmentIds())
        {
            var alignment = transaction.GetObject(id, OpenMode.ForRead) as Alignment;
            if (alignment != null && alignment.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)) return true;
        }
        return false;
    }

    private static bool ProfileExists(Transaction transaction, Alignment alignment, string name) =>
        alignment.GetProfileIds().Cast<ObjectId>().Any(id =>
        {
            var profile = transaction.GetObject(id, OpenMode.ForRead) as Profile;
            return profile != null && profile.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase);
        });
}
