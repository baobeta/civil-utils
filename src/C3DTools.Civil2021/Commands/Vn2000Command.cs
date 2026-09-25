using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Curves;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Geodesy;
using C3DTools.Core.Tables;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcEntity = Autodesk.AutoCAD.DatabaseServices.Entity;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.Vn2000Command))]

namespace C3DTools.Civil2021.Commands;

public class Vn2000Command
{
    private const string Command = "CTVN2000";

    /// <summary>CTVN2000: moves the selection or every COGO point from one VN-2000 central meridian / zone to another, in one undo.</summary>
    [CommandMethod("C3DTOOLS", "CTVN2000", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void Vn2000()
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
        var session = new Vn2000Session(preset)
        {
            FromText = memory.Get(Command, "From", "105°45'"),
            ToText = memory.Get(Command, "To", "106°15'"),
            FromZoneWidth = memory.Get(Command, "FromZone", 3),
            ToZoneWidth = memory.Get(Command, "ToZone", 3),
        };

        var cogoIds = ReadCogo(doc, session);
        if (memory.Get(Command, "Cogo", false)) session.TargetCogo = true;

        var selection = new List<ObjectId>();
        var implied = ed.SelectImplied();
        if (implied.Status == PromptStatus.OK && implied.Value != null)
        {
            ed.SetImpliedSelection(new ObjectId[0]);
            selection = implied.Value.GetObjectIds().ToList();
            Load(doc, selection, session);
        }

        while (true)
        {
            var window = new Vn2000Window(session);
            var action = window.ShowModal();
            memory.Set(Command, "From", session.FromText);
            memory.Set(Command, "To", session.ToText);
            memory.Set(Command, "FromZone", session.FromZoneWidth);
            memory.Set(Command, "ToZone", session.ToZoneWidth);
            memory.Set(Command, "Cogo", session.TargetCogo);
            ToolWindow.SaveOptions();

            switch (action)
            {
                case DialogAction.Pick:
                    var result = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nChọn đối tượng cần chuyển kinh tuyến: " });
                    if (result.Status != PromptStatus.OK || result.Value == null) continue;
                    selection = result.Value.GetObjectIds().ToList();
                    session.TargetSelection = true;
                    Load(doc, selection, session);
                    continue;
                case DialogAction.Preview:
                case DialogAction.Apply:
                    if (!session.CanApply) continue;
                    var ids = session.Target == Vn2000Target.Cogo ? cogoIds : selection;
                    if (Write(doc, ids, session, askToKeep: action == DialogAction.Preview)) return;
                    continue;   // Khong or an error: back to the dialog
                default:
                    return;
            }
        }
    }

    /// <summary>Every COGO point of the drawing into the session (Easting/Northing); an empty list on failure.</summary>
    private static List<ObjectId> ReadCogo(Document doc, Vn2000Session session)
    {
        var ids = new List<ObjectId>();
        try
        {
            var civil = CivilDocument.GetCivilDocument(doc.Database);
            var items = new List<Vn2000Item>();
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in civil.GetAllPointIds())
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is CogoPoint point)) continue;
                    ids.Add(id);
                    items.Add(new Vn2000Item("Điểm " + point.PointNumber.ToString(CultureInfo.InvariantCulture), point.Easting, point.Northing));
                }

                tr.Commit();
            }

            session.SetCogo(items);
        }
        catch (System.Exception ex)
        {
            Prompts.Say(doc.Editor, $"Không đọc được điểm COGO ({ex.Message}); chỉ chuyển được đối tượng chọn.");
            session.SetCogo(null);
            ids.Clear();
        }

        return ids;
    }

    /// <summary>Reference points of the selection into the session; unsupported objects are counted and reported.</summary>
    private static void Load(Document doc, List<ObjectId> ids, Vn2000Session session)
    {
        var items = new List<Vn2000Item>();
        var skipped = 0;
        try
        {
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                foreach (var id in ids)
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead) as AcEntity;
                    if (entity == null || !Collect(tr, entity, items)) skipped++;
                }

                tr.Commit();
            }
        }
        catch (System.Exception ex)
        {
            Prompts.Say(doc.Editor, $"Không đọc được đối tượng chọn: {ex.Message}");
            return;
        }

        if (skipped > 0) Prompts.Say(doc.Editor, $"{skipped} đối tượng không hỗ trợ (hoặc polyline không nằm trên mặt XY) sẽ giữ nguyên.");
        session.SetSelection($"{ids.Count - skipped} đối tượng, {items.Count} điểm", items);
    }

    /// <summary>Adds the points of a supported entity; false when the entity is not supported.</summary>
    private static bool Collect(Transaction tr, AcEntity entity, List<Vn2000Item> items)
    {
        var name = entity.GetRXClass().DxfName + " " + entity.Handle;
        switch (entity)
        {
            case DBPoint p:
                items.Add(new Vn2000Item(name, p.Position.X, p.Position.Y));
                return true;
            case Line l:
                items.Add(new Vn2000Item(name + " đầu", l.StartPoint.X, l.StartPoint.Y));
                items.Add(new Vn2000Item(name + " cuối", l.EndPoint.X, l.EndPoint.Y));
                return true;
            case Arc a:
                items.Add(new Vn2000Item(name + " tâm", a.Center.X, a.Center.Y, a.Radius));
                return true;
            case Circle c:
                items.Add(new Vn2000Item(name + " tâm", c.Center.X, c.Center.Y, c.Radius));
                return true;
            case Polyline pl:
                if (!IsPlan(pl.Normal)) return false;
                for (var i = 0; i < pl.NumberOfVertices; i++)
                {
                    var v = pl.GetPoint3dAt(i);
                    items.Add(new Vn2000Item(name + " đỉnh " + (i + 1).ToString(CultureInfo.InvariantCulture), v.X, v.Y));
                }

                return true;
            case Polyline2d pl2:
                if (!IsPlan(pl2.Normal)) return false;
                var n2 = 0;
                foreach (ObjectId vid in pl2)
                {
                    var v = (Vertex2d)tr.GetObject(vid, OpenMode.ForRead);
                    items.Add(new Vn2000Item(name + " đỉnh " + (++n2).ToString(CultureInfo.InvariantCulture), v.Position.X, v.Position.Y));
                }

                return true;
            case Polyline3d pl3:
                var n3 = 0;
                foreach (ObjectId vid in pl3)
                {
                    var v = (PolylineVertex3d)tr.GetObject(vid, OpenMode.ForRead);
                    items.Add(new Vn2000Item(name + " đỉnh " + (++n3).ToString(CultureInfo.InvariantCulture), v.Position.X, v.Position.Y));
                }

                return true;
            case BlockReference b:
                items.Add(new Vn2000Item(name, b.Position.X, b.Position.Y));
                return true;
            case DBText t:
                items.Add(new Vn2000Item(name, t.Position.X, t.Position.Y));
                return true;
            case MText mt:
                items.Add(new Vn2000Item(name, mt.Location.X, mt.Location.Y));
                return true;
            case CogoPoint cp:
                items.Add(new Vn2000Item("Điểm " + cp.PointNumber.ToString(CultureInfo.InvariantCulture), cp.Easting, cp.Northing));
                return true;
            default:
                return false;
        }
    }

    /// <summary>OCS = WCS in plan: vertex X/Y can be converted directly.</summary>
    private static bool IsPlan(Vector3d normal) => normal.IsParallelTo(Vector3d.ZAxis) && normal.Z > 0;

    /// <summary>Moves every object in one transaction (one undo). True when kept.</summary>
    private static bool Write(Document doc, List<ObjectId> ids, Vn2000Session session, bool askToKeep)
    {
        var ed = doc.Editor;
        session.UpdateStatistics();
        var t = session.Transform();
        var moved = 0;
        var failed = 0;
        var blocks = 0;
        var arcs = 0;
        var bulged = 0;
        var texts = 0;
        bool keep;
        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            try
            {
                foreach (var id in ids)
                {
                    if (id.IsErased) continue;
                    try
                    {
                        var entity = (AcEntity)tr.GetObject(id, OpenMode.ForWrite);
                        if (entity is BlockReference) blocks++;
                        if (entity is Arc || entity is Circle) arcs++;
                        if (entity is Polyline bp && bp.HasBulges) bulged++;
                        if (entity is DBText || entity is MText) texts++;
                        if (Move(tr, entity, t)) moved++;
                    }
                    catch (System.Exception ex)
                    {
                        // Locked layer, locked or checked-out COGO point: that object stays where it is.
                        if (failed++ < 5) Prompts.Say(ed, $"Không chuyển được đối tượng {id.Handle}: {ex.Message}");
                    }
                }

                tr.TransactionManager.QueueForGraphicsFlush();
                ed.UpdateScreen();
                Prompts.Say(ed, $"Đã chuyển {moved} đối tượng" + (failed > 0 ? $", {failed} đối tượng giữ nguyên" : "") + ". " + session.DistortionText + ".");
                if (arcs > 0)
                    Prompts.Say(ed, $"{arcs} cung/đường tròn chỉ dời tâm, giữ bán kính và góc (sai lệch bán kính tới {NumberFormat.Fixed(session.MaxRadiusError * 1000, 1)} mm).");
                if (bulged > 0)
                    Prompts.Say(ed, $"{bulged} polyline có cung: chỉ dời đỉnh, cung trong polyline giữ độ phình.");
                if (blocks > 0 || texts > 0)
                    Prompts.Say(ed, $"{blocks} block, {texts} TEXT/MTEXT được xoay thêm theo chênh lệch góc hội tụ kinh tuyến (tới {NumberFormat.Fixed(session.MaxRotation * 180 / System.Math.PI * 3600, 1)}\").");
                keep = moved > 0 && (!askToKeep || Prompts.AskKeep(ed));
            }
            catch (System.Exception ex)
            {
                Prompts.Say(ed, $"Lỗi khi chuyển kinh tuyến: {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
                return false;
            }

            if (keep) tr.Commit();
            else tr.Abort();
        }

        if (!keep)
        {
            ed.Regen();
            return false;
        }

        Prompts.Say(ed, $"Hoàn thành: {session.SummaryText}.\n");
        return true;
    }

    private static Point3d Map(Vn2000Transform t, Point3d p)
    {
        var q = t.Apply(p.X, p.Y);
        return new Point3d(q.X, q.Y, p.Z);
    }

    /// <summary>Moves one entity's points; blocks, TEXT and MTEXT also turn by the convergence change. False when the type is not supported.</summary>
    private static bool Move(Transaction tr, AcEntity entity, Vn2000Transform t)
    {
        switch (entity)
        {
            case DBPoint p:
                p.Position = Map(t, p.Position);
                return true;
            case Line l:
                var start = Map(t, l.StartPoint);
                var end = Map(t, l.EndPoint);
                l.StartPoint = start;
                l.EndPoint = end;
                return true;
            case Arc a:
                a.Center = Map(t, a.Center);
                return true;
            case Circle c:
                c.Center = Map(t, c.Center);
                return true;
            case Polyline pl:
                if (!IsPlan(pl.Normal)) return false;
                for (var i = 0; i < pl.NumberOfVertices; i++)
                {
                    var v = Map(t, pl.GetPoint3dAt(i));
                    pl.SetPointAt(i, new Point2d(v.X, v.Y));
                }

                return true;
            case Polyline2d pl2:
                if (!IsPlan(pl2.Normal)) return false;
                foreach (ObjectId vid in pl2)
                {
                    var v = (Vertex2d)tr.GetObject(vid, OpenMode.ForWrite);
                    v.Position = Map(t, v.Position);
                }

                return true;
            case Polyline3d pl3:
                foreach (ObjectId vid in pl3)
                {
                    var v = (PolylineVertex3d)tr.GetObject(vid, OpenMode.ForWrite);
                    v.Position = Map(t, v.Position);
                }

                return true;
            case BlockReference b:
                // TransformBy moves the attributes with the block; the rotation keeps its bearing to true north.
                b.TransformBy(MoveAndTurn(t, b.Position));
                return true;
            case DBText text:
                // Like a block: moved by its position and turned by the convergence change there.
                text.TransformBy(MoveAndTurn(t, text.Position));
                return true;
            case MText mt:
                mt.TransformBy(MoveAndTurn(t, mt.Location));
                return true;
            case CogoPoint cp:
                // Easting/Northing have public setters in the 2021 API (reflection); a locked point throws and is reported.
                var q = t.Apply(cp.Easting, cp.Northing);
                cp.Easting = q.X;
                cp.Northing = q.Y;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Moves from its converted position and turns about it by the convergence change there.</summary>
    private static Matrix3d MoveAndTurn(Vn2000Transform t, Point3d from)
    {
        var to = Map(t, from);
        return Matrix3d.Rotation(t.RotationDelta(from.X, from.Y), Vector3d.ZAxis, to) * Matrix3d.Displacement(to - from);
    }
}
