using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Curves;
using C3DTools.Civil2021.Drawing;
using C3DTools.Civil2021.Sections;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Sections;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.SheetArrangeCommand))]

namespace C3DTools.Civil2021.Commands;

public class SheetArrangeCommand
{
    private const string Command = "CTXEPTRANG";

    /// <summary>CTXEPTRANG: moves section views (with their CTTRACNGANG tables) onto sheets in station order and draws a frame + title per sheet; one undo.</summary>
    [CommandMethod("C3DTOOLS", "CTXEPTRANG", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void SheetArrange()
    {
        try
        {
            Run();
        }
        catch (System.Exception ex)
        {
            Prompts.Say(AcCoreApp.DocumentManager.MdiActiveDocument?.Editor, $"Lỗi C3DTools: {ex.Message}");
        }
    }

    /// <summary>The views to arrange (station order), the frames' tag handle and where sheet 1 goes.</summary>
    private sealed class Selection
    {
        public List<SheetItem> Items { get; set; }
        public string TagHandle { get; set; }
        public double OriginX { get; set; }
        public double OriginY { get; set; }
    }

    private static void Run()
    {
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        var messages = new List<string>();
        var preset = PresetLocator.LoadForDrawing(messages);
        foreach (var m in messages) Prompts.Say(ed, m);

        var memory = ToolWindow.Options;
        var session = new SheetArrangeSession(preset) { WriteFrames = memory.Get(Command, "Khung", true) };

        Selection selection = null;
        var first = SectionTableCommand.ImpliedViews(ed);
        if (first.Count > 0) selection = Load(doc, first, session) ?? selection;

        while (true)
        {
            var action = new SheetArrangeWindow(session).ShowModal();
            memory.Set(Command, "Khung", session.WriteFrames);
            ToolWindow.SaveOptions();

            switch (action)
            {
                case DialogAction.Pick:
                    var picked = SectionTableCommand.PickViews(ed, "Chọn các trắc ngang (section view) cần xếp: ");
                    if (picked.Count == 0) continue;
                    selection = Load(doc, picked, session) ?? selection;
                    continue;
                case DialogAction.Preview:
                case DialogAction.Apply:
                    if (selection == null || !session.CanApply) continue;
                    if (Write(doc, selection, session, askToKeep: action == DialogAction.Preview)) return;
                    continue;
                default:
                    return;
            }
        }
    }

    /// <summary>Measures the views in station order; null (with a message) when none can be measured.</summary>
    private static Selection Load(Document doc, List<ObjectId> ids, SheetArrangeSession session)
    {
        var ed = doc.Editor;
        void Say(string m) => Prompts.Say(ed, m);
        try
        {
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var items = new List<SheetItem>();
                var groups = new List<string>();
                var unknownStation = 0;
                foreach (var id in ids)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is SectionView view)) continue;
                    var source = SectionReader.Read(tr, view, null);
                    var station = source?.Station ?? double.NaN;
                    if (source == null) unknownStation++;
                    else groups.Add(source.GroupId.IsNull ? null : source.GroupId.Handle.ToString());
                    var item = SheetWriter.Measure(tr, doc.Database, view, station, Say);
                    if (item != null) items.Add(item);
                }

                if (items.Count == 0)
                {
                    tr.Commit();
                    Say("Không đo được trắc ngang nào đã chọn.");
                    return null;
                }

                if (unknownStation > 0) Say($"{unknownStation} trắc ngang không tìm được cọc mặt cắt: xếp sau cùng, theo thứ tự chọn.");
                // Stable: views without a station keep their pick order at the end.
                items = items.OrderBy(i => double.IsNaN(i.Station) ? 1 : 0).ThenBy(i => double.IsNaN(i.Station) ? 0 : i.Station).ToList();

                var tag = SheetWriter.TagHandle(groups, items[0].Handle);
                var previous = SheetWriter.PreviousOrigin(tr, doc.Database, tag);
                var selection = new Selection
                {
                    Items = items,
                    TagHandle = tag,
                    OriginX = previous?.X ?? items.Min(i => i.Min.X),
                    OriginY = previous?.Y ?? items.Min(i => i.Min.Y),
                };
                tr.Commit();

                var stations = items.Select(i => i.Station).ToList();
                session.SetViews($"{items.Count.ToString(CultureInfo.InvariantCulture)} trắc ngang", items.Select(i => (i.Width, i.Height)), stations);
                return selection;
            }
        }
        catch (System.Exception ex)
        {
            Say($"Không đọc được trắc ngang: {ex.Message}");
            return null;
        }
    }

    /// <summary>Erases the previous frames, moves the views and draws the new frames in one transaction (one undo). True when kept.</summary>
    private static bool Write(Document doc, Selection selection, SheetArrangeSession session, bool askToKeep)
    {
        var ed = doc.Editor;
        void Say(string m) => Prompts.Say(ed, m);
        var plan = session.Plan(selection.OriginX, selection.OriginY);
        if (plan == null)
        {
            Say(session.LayoutError ?? "Chưa chọn trắc ngang.");
            return false;
        }

        if (plan.Oversize > 0) Say($"{plan.Oversize} trắc ngang lớn hơn ô của tờ; chúng vẫn được đặt giữa ô nhưng sẽ chờm sang ô bên cạnh.");

        bool keep;
        var usedBlock = false;
        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            try
            {
                var d = new TaggedDrawing(tr, doc.Database, SheetWriter.Tool, selection.TagHandle);
                SheetWriter.WriteOrigin(d, selection.OriginX, selection.OriginY);
                try
                {
                    SheetWriter.Move(tr, selection.Items, plan);
                }
                catch (System.Exception ex)
                {
                    Say($"Không dời được trắc ngang (SectionView.Location): {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
                    tr.Abort();
                    return false;
                }

                if (session.WriteFrames)
                {
                    // Old frames go only when a new set replaces them.
                    d.EraseTagged(null, (id, tag) => tag.Kind == SheetWriter.FrameKind);
                    var titles = Enumerable.Range(0, plan.SheetCount).Select(i => session.Title(plan, i)).ToList();
                    usedBlock = SheetWriter.WriteFrames(d, plan, session.Layout(), session.UnitsPerMm, titles, Say);
                }

                tr.TransactionManager.QueueForGraphicsFlush();
                ed.UpdateScreen();
                keep = !askToKeep || Prompts.AskKeep(ed);
            }
            catch (System.Exception ex)
            {
                Say($"Lỗi khi xếp trắc ngang: {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
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

        ed.Regen();
        var frames = session.WriteFrames ? (usedBlock ? ", khung từ A3.dwg" : ", khung polyline") : "";
        Say($"Hoàn thành: {session.SummaryText}{frames}.\n");
        return true;
    }
}
