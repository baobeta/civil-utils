using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Curves;
using C3DTools.Civil2021.Drawing;
using C3DTools.Civil2021.Profiles;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Presets;
using C3DTools.Core.Profiles;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.VerticalCurveCommand))]

namespace C3DTools.Civil2021.Commands;

public class VerticalCurveCommand
{
    private const string Command = "CTCONGDUNG";
    private const string Tool = "CONGDUNG";
    private const string Title = "BẢNG YẾU TỐ CONG ĐỨNG";
    private const short TableKind = 2;

    /// <summary>CTCONGDUNG: vertical curve elements of a design profile, checked against the preset; box above each PVI, table, CSV, Excel.</summary>
    [CommandMethod("C3DTOOLS", "CTCONGDUNG", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void VerticalCurves()
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

    /// <summary>The picked profile view.</summary>
    private sealed class ViewSource
    {
        public ObjectId Id { get; set; }
        public string Handle { get; set; }
        public string Name { get; set; }
    }

    private static void Run()
    {
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        var messages = new List<string>();
        var preset = PresetLocator.LoadForDrawing(messages);
        foreach (var m in messages) Prompts.Say(ed, m);

        var memory = ToolWindow.Options;
        var session = new VerticalCurveSession(preset)
        {
            WriteBox = memory.Get(Command, "Khung", true),
            WriteTable = memory.Get(Command, "Bang", false),
            WriteCsv = memory.Get(Command, "CSV", false),
            WriteXlsx = memory.Get(Command, "Excel", false),
        };
        var speed = memory.Get(Command, "V", session.DesignSpeed);
        if (session.AvailableSpeeds.Contains(speed)) session.DesignSpeed = speed;

        ViewSource view = null;
        var first = PickFirst(ed);
        if (!first.IsNull) view = Load(doc, first, session, memory.Get(Command, "Profile", "")) ?? view;

        while (true)
        {
            var window = new VerticalCurveWindow(session);
            var action = window.ShowModal();
            memory.Set(Command, "Khung", session.WriteBox);
            memory.Set(Command, "Bang", session.WriteTable);
            memory.Set(Command, "CSV", session.WriteCsv);
            memory.Set(Command, "Excel", session.WriteXlsx);
            memory.Set(Command, "V", session.DesignSpeed);
            memory.Set(Command, "Profile", session.Profile ?? "");
            ToolWindow.SaveOptions();

            switch (action)
            {
                case DialogAction.Pick:
                    var picked = Prompts.PickEntity<ProfileView>(ed, "Chọn trắc dọc (profile view): ");
                    if (picked.IsNull) continue;
                    view = Load(doc, picked, session, session.Profile) ?? view;
                    continue;
                case DialogAction.Preview:
                case DialogAction.Apply:
                    if (view == null || !session.CanApply) continue;
                    if (Write(doc, view, session, preset, askToKeep: action == DialogAction.Preview)) return;
                    continue;   // Khong, no insertion point or an error: back to the dialog
                default:
                    return;
            }
        }
    }

    /// <summary>The first profile view of the selection made before the command, or ObjectId.Null.</summary>
    internal static ObjectId PickFirst(Editor ed)
    {
        var implied = ed.SelectImplied();
        if (implied.Status != PromptStatus.OK || implied.Value == null) return ObjectId.Null;
        ed.SetImpliedSelection(new ObjectId[0]);
        var viewClass = RXObject.GetClass(typeof(ProfileView));
        return implied.Value.GetObjectIds().FirstOrDefault(id => id.ObjectClass.IsDerivedFrom(viewClass));
    }

    /// <summary>Reads the view's alignment profiles (all but surface profiles) into the session; null (with a message) on failure.</summary>
    private static ViewSource Load(Document doc, ObjectId id, VerticalCurveSession session, string preferred)
    {
        var ed = doc.Editor;
        try
        {
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var pv = (ProfileView)tr.GetObject(id, OpenMode.ForRead);
                var alignment = (Alignment)tr.GetObject(pv.AlignmentId, OpenMode.ForRead);
                var source = new ViewSource { Id = id, Handle = pv.Handle.ToString(), Name = pv.Name };
                var profiles = new List<KeyValuePair<string, IReadOnlyList<ProfileSegment>>>();
                var names = new HashSet<string>();
                foreach (var (profileId, name, type) in ProfileReader.ProfilesOf(tr, alignment).OrderBy(p => p.type == ProfileType.FG ? 0 : 1))
                {
                    if (type == ProfileType.EG || !names.Add(name)) continue;
                    IReadOnlyList<ProfileSegment> segments = null;
                    try
                    {
                        segments = ProfileReader.Read((Profile)tr.GetObject(profileId, OpenMode.ForRead), m => Prompts.Say(ed, m));
                    }
                    catch (System.Exception ex)
                    {
                        Prompts.Say(ed, $"Không đọc được các đoạn của trắc dọc {name}: {ex.Message}");
                    }

                    profiles.Add(new KeyValuePair<string, IReadOnlyList<ProfileSegment>>(name, segments));
                }

                tr.Commit();
                if (profiles.Count == 0) Prompts.Say(ed, $"Tuyến {alignment.Name} không có trắc dọc thiết kế.");
                session.SetSource($"{source.Name} (tuyến {alignment.Name})");
                session.SetProfiles(profiles, preferred);
                return source;
            }
        }
        catch (System.Exception ex)
        {
            Prompts.Say(ed, $"Không đọc được trắc dọc: {ex.Message}");
            return null;
        }
    }

    /// <summary>Boxes and table in one transaction (one undo), replacing this view's previous ones; then CSV/Excel. True when kept.</summary>
    private static bool Write(Document doc, ViewSource view, VerticalCurveSession session, ProjectPreset preset, bool askToKeep)
    {
        var ed = doc.Editor;
        var h = preset.CurveBox?.TextHeight > 0 ? preset.CurveBox.TextHeight : 2.5;
        Point3d? insert = null;
        if (session.WriteTable)
        {
            insert = Prompts.PickPoint(ed, "Điểm chèn bảng cong đứng: ");
            if (insert == null) return false;
        }

        if (session.WriteBox || session.WriteTable)
        {
            bool keep;
            var fallback = false;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    var d = new TaggedDrawing(tr, doc.Database, Tool, view.Handle);
                    d.EraseTagged(new HashSet<ObjectId> { view.Id },
                        (id, tag) => (tag.Kind == ProfileBoxWriter.BoxKind && session.WriteBox) || (tag.Kind == TableKind && session.WriteTable));
                    d.EnsureLayer(ProfileBoxWriter.Layer, 2);

                    if (session.WriteBox)
                    {
                        var pv = (ProfileView)tr.GetObject(view.Id, OpenMode.ForRead);
                        var frame = new ProfileViewFrame(tr, pv);
                        ProfileBoxWriter.Write(d, frame, session.Curves, h, 4 * h, Tool);
                        fallback = frame.UsedFallback;
                    }

                    if (session.WriteTable)
                    {
                        var table = CurveTableWriter.Create(doc.Database, session.Table, Title + " - " + session.Profile, insert.Value, h);
                        d.Add(table, ProfileBoxWriter.Layer, new ToolTag(Tool) { Kind = TableKind });
                        table.GenerateLayout();
                    }

                    tr.TransactionManager.QueueForGraphicsFlush();
                    ed.UpdateScreen();
                    if (fallback)
                        Prompts.Say(ed, "Không lấy được toạ độ từ trắc dọc (FindXYAtStationAndElevation); khung đặt theo gốc trắc dọc và tỷ lệ đứng của kiểu trắc dọc, hãy kiểm tra vị trí.");
                    keep = !askToKeep || Prompts.AskKeep(ed);
                }
                catch (System.Exception ex)
                {
                    Prompts.Say(ed, $"Lỗi khi vẽ yếu tố cong đứng: {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
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
        }

        TableFiles.Write(ed, session.Table, "CONGDUNG", session.WriteCsv, session.WriteXlsx, "Cong đứng", "bảng cong đứng");
        var warnings = Enumerable.Range(0, session.Curves.Count).SelectMany(i => session.IssuesAt(i)).Select(i => i.Message).Distinct();
        foreach (var warning in warnings) Prompts.Say(ed, warning);

        Prompts.Say(ed, $"Hoàn thành: {session.SummaryText}.\n");
        return true;
    }
}
