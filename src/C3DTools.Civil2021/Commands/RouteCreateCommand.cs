using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using C3DTools.Civil2021.Curves;
using C3DTools.Civil2021.Services;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Curves;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
using CivilSurface = Autodesk.Civil.DatabaseServices.Surface;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.RouteCreateCommand))]

namespace C3DTools.Civil2021.Commands;

public class RouteCreateCommand
{
    private const string Command = "CTTUYEN";
    private const string PreferredStyle = "TCVN_Tuyen";

    /// <summary>CTTUYEN: a new alignment from a polyline or picked points, with its plan scale, speed, ground profile and assembly.</summary>
    [CommandMethod("C3DTOOLS", "CTTUYEN", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void CreateRoute()
    {
        try
        {
            Run();
        }
        catch (System.Exception ex)
        {
            // Last resort: never let an exception reach AutoCAD's unhandled-exception dialog.
            Prompts.Say(AcCoreApp.DocumentManager.MdiActiveDocument?.Editor, $"Lỗi C3DTools: {ex.Message}");
        }
    }

    private static void Run()
    {
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        var messages = new List<string>();
        var preset = PresetLocator.LoadForDrawing(messages);
        foreach (var m in messages) Prompts.Say(ed, m);

        var memory = ToolWindow.Options;
        var session = new RouteCreationSession(preset)
        {
            ScaleText = memory.Get(Command, "Scale", "1000"),
            LayerName = memory.Get(Command, "Layer", "TUYEN"),
            OpenCurveDesign = memory.Get(Command, "OpenCurveDesign", true),
            LoadAllAssemblies = memory.Get(Command, "LoadAllAssemblies", true),
        };
        ReadDrawing(doc, session);

        var polylineId = PickFirst(ed);
        if (!polylineId.IsNull) session.SetPolylineSource(Describe(doc, polylineId));

        while (true)
        {
            var window = new RouteCreateWindow(session, ReadAssemblies);
            var action = window.ShowModal();
            memory.Set(Command, "Scale", session.ScaleText);
            memory.Set(Command, "Layer", session.LayerName);
            memory.Set(Command, "OpenCurveDesign", session.OpenCurveDesign);
            memory.Set(Command, "LoadAllAssemblies", session.LoadAllAssemblies);
            ToolWindow.SaveOptions();

            switch (action)
            {
                case DialogAction.Pick:
                    var picked = Prompts.PickEntity<AcPolyline>(ed, "Chọn polyline tim tuyến: ");
                    if (picked.IsNull) continue;
                    polylineId = picked;
                    session.SetPolylineSource(Describe(doc, picked));
                    continue;
                case DialogAction.PickPoints:
                    var points = Prompts.PickPoints(ed, "Điểm đầu tuyến: ", "Đỉnh tiếp theo [Lui] <Enter: kết thúc>: ");
                    if (points == null || points.Count < 2) continue;
                    polylineId = ObjectId.Null;
                    session.SetPickedPoints(points.Select(p => new PlanPoint(p.X, p.Y)));
                    continue;
                case DialogAction.Preview:
                case DialogAction.Apply:
                    if (!session.CanApply) continue;
                    var created = Write(doc, session, polylineId, askToKeep: action == DialogAction.Preview);
                    if (created.IsNull) continue;   // not kept or failed: back to the dialog
                    if (session.OpenCurveDesign)
                    {
                        ed.SetImpliedSelection(new[] { created });
                        doc.SendStringToExecute("_CTYTC ", true, false, false);
                    }

                    return;
                default:
                    return;
            }
        }
    }

    /// <summary>The polyline selected before the command, or ObjectId.Null.</summary>
    private static ObjectId PickFirst(Editor ed)
    {
        var implied = ed.SelectImplied();
        if (implied.Status != PromptStatus.OK || implied.Value == null) return ObjectId.Null;
        ed.SetImpliedSelection(new ObjectId[0]);
        var polylineClass = RXObject.GetClass(typeof(AcPolyline));
        return implied.Value.GetObjectIds().FirstOrDefault(id => id.ObjectClass.IsDerivedFrom(polylineClass));
    }

    private static string Describe(Document doc, ObjectId polylineId)
    {
        using (var tr = doc.TransactionManager.StartTransaction())
        {
            var pline = (AcPolyline)tr.GetObject(polylineId, OpenMode.ForRead);
            var text = $"Polyline ({pline.NumberOfVertices} đỉnh, {pline.Length:0.00} m)";
            tr.Commit();
            return text;
        }
    }

    /// <summary>Alignment names, alignment styles, label sets, TIN/grid surfaces and assemblies of the drawing.</summary>
    private static void ReadDrawing(Document doc, RouteCreationSession session)
    {
        var civil = CivilDocument.GetCivilDocument(doc.Database);
        using (var tr = doc.TransactionManager.StartTransaction())
        {
            var alignments = civil.GetAlignmentIds().Cast<ObjectId>().Select(id => (tr.GetObject(id, OpenMode.ForRead) as Alignment)?.Name);
            var surfaces = civil.GetSurfaceIds().Cast<ObjectId>()
                .Select(id => tr.GetObject(id, OpenMode.ForRead) as CivilSurface)
                .Where(s => s is TinSurface || s is GridSurface)
                .Select(s => s.Name);
            var assemblies = civil.AssemblyCollection.Cast<ObjectId>().Select(id => (tr.GetObject(id, OpenMode.ForRead) as Assembly)?.Name);
            session.SetDrawing(alignments.ToList(), StyleNames(tr, civil.Styles.AlignmentStyles), PreferredStyle,
                StyleNames(tr, civil.Styles.LabelSetStyles.AlignmentLabelSetStyles), TcvnStyleImporter.LabelSetName, surfaces.ToList(), assemblies.ToList());
            tr.Commit();
        }
    }

    private static List<string> StyleNames(Transaction tr, StyleCollectionBase styles) =>
        styles.Cast<ObjectId>().Select(id => (tr.GetObject(id, OpenMode.ForRead) as StyleBase)?.Name).Where(n => n != null).ToList();

    /// <summary>"Tệp mặt cắt": the assembly names of a DWG, read without opening it in the editor.</summary>
    private static IList<string> ReadAssemblies(string path)
    {
        using (var side = new Database(false, true))
        {
            side.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, true, "");
            var civil = CivilDocument.GetCivilDocument(side);
            using (var tr = side.TransactionManager.StartTransaction())
            {
                var names = civil.AssemblyCollection.Cast<ObjectId>()
                    .Select(id => (tr.GetObject(id, OpenMode.ForRead) as Assembly)?.Name)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToList();
                tr.Commit();
                return names;
            }
        }
    }

    /// <summary>Everything in one transaction (one undo). The new alignment's id, or ObjectId.Null when not kept or failed.</summary>
    private static ObjectId Write(Document doc, RouteCreationSession session, ObjectId polylineId, bool askToKeep)
    {
        var ed = doc.Editor;
        var db = doc.Database;
        ObjectId id;
        bool keep;
        using (doc.LockDocument())
        using (var tr = db.TransactionManager.StartTransaction())
        {
            try
            {
                var civil = CivilDocument.GetCivilDocument(db);
                var layerId = EnsureLayer(tr, db, session.LayerName.Trim());
                var sourceId = session.IsPolyline ? polylineId : AddPolyline(tr, db, session.PickedPoints, layerId);
                var options = new PolylineOptions { PlineId = sourceId, AddCurvesBetweenTangents = false, EraseExistingEntities = !session.IsPolyline };
                id = Alignment.Create(civil, options, session.Name.Trim(), ObjectId.Null, layerId,
                    StyleId(tr, civil.Styles.AlignmentStyles, session.StyleNames, session.StyleIndex),
                    StyleId(tr, civil.Styles.LabelSetStyles.AlignmentLabelSetStyles, session.LabelSetNames, session.LabelSetIndex));
                var alignment = (Alignment)tr.GetObject(id, OpenMode.ForWrite);
                alignment.Description = session.Description.Trim();
                Try(ed, "đặt lý trình đầu", () => alignment.ReferencePointStation = session.StartStation);
                Try(ed, "ghi vận tốc thiết kế", () =>
                {
                    alignment.DesignSpeeds.Add(alignment.StartingStation, session.DesignSpeed);
                    alignment.UseDesignSpeed = true;
                });

                ImportAssemblies(civil, session, alignment, ed);
                RouteTag.Write(tr, db, alignment, session.Scale, session.DesignSpeed, session.Assembly);
                if (session.Surface != null) CreateGroundProfile(tr, civil, alignment, session.Surface, layerId, ed);

                tr.TransactionManager.QueueForGraphicsFlush();
                ed.UpdateScreen();
                keep = !askToKeep || Prompts.AskKeep(ed);
            }
            catch (System.Exception ex)
            {
                Prompts.Say(ed, $"Không tạo được tuyến: {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
                return ObjectId.Null;
            }

            if (keep) tr.Commit();
            else tr.Abort();
        }

        if (!keep)
        {
            ed.Regen();
            return ObjectId.Null;
        }

        Prompts.Say(ed, $"Đã tạo tuyến {session.Name.Trim()}. Một lệnh U hoàn tác toàn bộ.\n");
        return id;
    }

    /// <summary>A step that may fail on an unusual drawing: warns and carries on.</summary>
    private static void Try(Editor ed, string what, Action action)
    {
        try
        {
            action();
        }
        catch (System.Exception ex)
        {
            Prompts.Say(ed, $"Không {what}: {ex.Message}");
        }
    }

    private static ObjectId EnsureLayer(Transaction tr, Database db, string name)
    {
        var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (table.Has(name)) return table[name];
        table.UpgradeOpen();
        var layer = new LayerTableRecord { Name = name, Color = Color.FromColorIndex(ColorMethod.ByAci, 1) };
        var id = table.Add(layer);
        tr.AddNewlyCreatedDBObject(layer, true);
        return id;
    }

    /// <summary>"Chỉ điểm…": a temporary LWPOLYLINE that Alignment.Create consumes (EraseExistingEntities).</summary>
    private static ObjectId AddPolyline(Transaction tr, Database db, IReadOnlyList<PlanPoint> points, ObjectId layerId)
    {
        var pline = CurveGeometryWriter.Polyline(points, false);
        pline.LayerId = layerId;
        var modelSpace = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
        modelSpace.AppendEntity(pline);
        tr.AddNewlyCreatedDBObject(pline, true);
        return pline.ObjectId;
    }

    private static ObjectId StyleId(Transaction tr, StyleCollectionBase styles, IReadOnlyList<string> names, int index)
    {
        if (index >= 0 && index < names.Count && styles.Contains(names[index])) return styles[names[index]];
        return styles.Count > 0 ? styles[0] : throw new InvalidOperationException("Bản vẽ thiếu Alignment Style hoặc Label Set.");
    }

    /// <summary>"Tệp mặt cắt": imports the assemblies to use, below the route start. A failure only warns.</summary>
    private static void ImportAssemblies(CivilDocument civil, RouteCreationSession session, Alignment alignment, Editor ed)
    {
        var names = session.AssembliesToImport;
        if (names.Count == 0 || string.IsNullOrEmpty(session.SectionFile)) return;
        try
        {
            using (var side = new Database(false, true))
            {
                side.ReadDwgFile(session.SectionFile, FileOpenMode.OpenForReadAndAllShare, true, "");
                double x = 0, y = 0;
                alignment.PointLocation(alignment.StartingStation, 0, ref x, ref y);
                var step = 30 * session.Scale / 1000;
                for (var i = 0; i < names.Count; i++)
                {
                    try
                    {
                        civil.AssemblyCollection.ImportAssembly(names[i], side, names[i], new Point3d(x, y - step * (i + 2), 0));
                    }
                    catch (System.Exception ex)
                    {
                        Prompts.Say(ed, $"Không nhập được mặt cắt {names[i]}: {ex.Message}");
                    }
                }
            }

            Prompts.Say(ed, $"Đã nhập {names.Count} mặt cắt từ {session.SectionFile}.");
        }
        catch (System.Exception ex)
        {
            Prompts.Say(ed, $"Không đọc được tệp mặt cắt: {ex.Message}");
        }
    }

    /// <summary>"Trắc dọc tự nhiên": a surface profile named &lt;tuyến&gt;-TN.</summary>
    private static void CreateGroundProfile(Transaction tr, CivilDocument civil, Alignment alignment, string surfaceName, ObjectId layerId, Editor ed)
    {
        var surfaceId = civil.GetSurfaceIds().Cast<ObjectId>()
            .FirstOrDefault(id => (tr.GetObject(id, OpenMode.ForRead) as CivilSurface)?.Name == surfaceName);
        var styleId = HostServices.FirstId(civil.Styles.ProfileStyles);
        var labelSetId = HostServices.FirstId(civil.Styles.LabelSetStyles.ProfileLabelSetStyles);
        if (surfaceId.IsNull || styleId.IsNull || labelSetId.IsNull)
        {
            Prompts.Say(ed, "Không tạo được trắc dọc tự nhiên: thiếu mặt phủ hoặc Profile Style.");
            return;
        }

        Try(ed, "tạo trắc dọc tự nhiên", () => Profile.CreateFromSurface(alignment.Name + "-TN", alignment.ObjectId, surfaceId, layerId, styleId, labelSetId));
    }
}
