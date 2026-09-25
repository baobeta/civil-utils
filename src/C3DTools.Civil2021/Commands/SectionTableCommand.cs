using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Curves;
using C3DTools.Civil2021.Drawing;
using C3DTools.Civil2021.Sections;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Profiles;
using C3DTools.Core.Sections;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.SectionTableCommand))]

namespace C3DTools.Civil2021.Commands;

public class SectionTableCommand
{
    private const string Command = "CTTRACNGANG";

    /// <summary>CTTRACNGANG: the Vietnamese data table under each section view (lines + text) with cut/fill areas, CSV, Excel; a rerun replaces the old tables.</summary>
    [CommandMethod("C3DTOOLS", "CTTRACNGANG", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void SectionTable()
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

    private static void Run()
    {
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        var messages = new List<string>();
        var preset = PresetLocator.LoadForDrawing(messages);
        foreach (var m in messages) Prompts.Say(ed, m);

        var memory = ToolWindow.Options;
        var session = new SectionTableSession(preset)
        {
            WholeGroup = memory.Get(Command, "CaNhom", false),
            WriteTable = memory.Get(Command, "Bang", true),
            WriteCsv = memory.Get(Command, "CSV", false),
            WriteXlsx = memory.Get(Command, "Excel", false),
        };
        foreach (var m in session.PresetMessages) Prompts.Say(ed, m);

        var views = new List<SectionViewSource>();
        var first = ImpliedViews(ed);
        if (first.Count > 0) views = Load(doc, first, session, memory.Get(Command, "Ground", ""), memory.Get(Command, "Design", "")) ?? views;

        while (true)
        {
            var window = new SectionTableWindow(session);
            var action = window.ShowModal();
            memory.Set(Command, "CaNhom", session.WholeGroup);
            memory.Set(Command, "Bang", session.WriteTable);
            memory.Set(Command, "CSV", session.WriteCsv);
            memory.Set(Command, "Excel", session.WriteXlsx);
            memory.Set(Command, "Ground", session.GroundSection ?? "");
            memory.Set(Command, "Design", session.DesignSection ?? "");
            ToolWindow.SaveOptions();

            switch (action)
            {
                case DialogAction.Pick:
                    var picked = PickViews(ed, "Chọn trắc ngang (section view): ");
                    if (picked.Count == 0) continue;
                    views = Load(doc, picked, session, session.GroundSection, session.DesignSection) ?? views;
                    continue;
                case DialogAction.Preview:
                case DialogAction.Apply:
                    if (views.Count == 0 || !session.CanApply) continue;
                    if (Write(doc, views, session, askToKeep: action == DialogAction.Preview)) return;
                    continue;
                default:
                    return;
            }
        }
    }

    /// <summary>Section views of the selection made before the command.</summary>
    internal static List<ObjectId> ImpliedViews(Editor ed)
    {
        var implied = ed.SelectImplied();
        if (implied.Status != PromptStatus.OK || implied.Value == null) return new List<ObjectId>();
        ed.SetImpliedSelection(new ObjectId[0]);
        return OnlyViews(implied.Value.GetObjectIds());
    }

    /// <summary>A window/crossing selection kept to section views; empty when cancelled.</summary>
    internal static List<ObjectId> PickViews(Editor ed, string message)
    {
        var result = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n" + message });
        if (result.Status != PromptStatus.OK || result.Value == null) return new List<ObjectId>();
        var views = OnlyViews(result.Value.GetObjectIds());
        if (views.Count == 0) Prompts.Say(ed, "Không có trắc ngang (section view) nào trong các đối tượng đã chọn.");
        return views;
    }

    private static List<ObjectId> OnlyViews(IEnumerable<ObjectId> ids)
    {
        var viewClass = RXObject.GetClass(typeof(SectionView));
        return ids.Where(id => id.ObjectClass.IsDerivedFrom(viewClass)).Distinct().ToList();
    }

    /// <summary>Reads the views' sample lines and sections into the session; null (with a message) when none is usable.</summary>
    private static List<SectionViewSource> Load(Document doc, List<ObjectId> ids, SectionTableSession session, string ground, string design)
    {
        var ed = doc.Editor;
        try
        {
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var result = ReadViews(tr, ids, ed);
                tr.Commit();
                if (result.Count == 0)
                {
                    Prompts.Say(ed, "Không đọc được trắc ngang nào đã chọn.");
                    return null;
                }

                var all = result.SelectMany(v => v.Sections).ToList();
                var groundNames = all.Where(s => s.IsSurface).Select(s => s.SourceName).Distinct().ToList();
                var designNames = all.OrderBy(s => s.DesignRank).Select(s => s.SourceName).Distinct().ToList();
                var description = result.Count == 1
                    ? $"{result[0].Name} (cọc {result[0].SampleLineName})"
                    : $"{result.Count} trắc ngang, cọc {result.OrderBy(v => v.Station).First().SampleLineName} … {result.OrderBy(v => v.Station).Last().SampleLineName}";
                session.SetSource(description, result.Count);
                session.SetSections(groundNames, designNames, ground, design);
                if (all.Count == 0) Prompts.Say(ed, "Các cọc mặt cắt (sample line) chưa lấy mặt cắt nào (Sample More Sources).");
                return result;
            }
        }
        catch (System.Exception ex)
        {
            Prompts.Say(ed, $"Không đọc được trắc ngang: {ex.Message}");
            return null;
        }
    }

    private static List<SectionViewSource> ReadViews(Transaction tr, IEnumerable<ObjectId> ids, Editor ed)
    {
        var result = new List<SectionViewSource>();
        foreach (var id in ids)
        {
            try
            {
                if (!(tr.GetObject(id, OpenMode.ForRead) is SectionView view)) continue;
                var source = SectionReader.Read(tr, view, m => Prompts.Say(ed, m));
                if (source != null) result.Add(source);
            }
            catch (System.Exception ex)
            {
                Prompts.Say(ed, $"Không đọc được trắc ngang {id.Handle}: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>Reads the chosen sections of every view, builds the tables, draws them (one transaction, one undo) and writes CSV/Excel. True when kept.</summary>
    private static bool Write(Document doc, List<SectionViewSource> picked, SectionTableSession session, bool askToKeep)
    {
        var ed = doc.Editor;
        void Say(string m) => Prompts.Say(ed, m);

        var inputs = new List<(SectionViewSource view, SectionInput input)>();
        using (var tr = doc.TransactionManager.StartTransaction())
        {
            var views = picked;
            if (session.WholeGroup)
            {
                var ids = picked.SelectMany(v => SectionReader.GroupViews(tr, v, Say)).Distinct().ToList();
                var known = new HashSet<ObjectId>(picked.Select(v => v.ViewId));
                views = picked.Concat(ReadViews(tr, ids.Where(id => !known.Contains(id)), ed)).ToList();
            }

            foreach (var v in views.OrderBy(v => v.Station))
            {
                var groundLine = v.Sections.FirstOrDefault(s => s.SourceName == session.GroundSection);
                if (groundLine == null)
                {
                    Say($"Cọc {v.SampleLineName} không có mặt cắt {session.GroundSection}; bỏ qua trắc ngang {v.Name}.");
                    continue;
                }

                var ground = SectionReader.Points(tr, groundLine.Id, Say);
                if (ground == null)
                {
                    Say($"Bỏ qua trắc ngang {v.Name}: không đọc được đường tự nhiên.");
                    continue;
                }

                SectionProfile design = null;
                if (session.DesignSection != null)
                {
                    var designLine = v.Sections.FirstOrDefault(s => s.SourceName == session.DesignSection);
                    if (designLine == null) Say($"Cọc {v.SampleLineName} không có mặt cắt {session.DesignSection}: dòng thiết kế để trống, diện tích = 0.");
                    else design = SectionReader.Points(tr, designLine.Id, Say);
                }

                inputs.Add((v, new SectionInput(v.SampleLineName, v.Station, ground, design)));
            }

            tr.Commit();
        }

        if (inputs.Count == 0)
        {
            Say("Không có trắc ngang nào đọc được điểm mặt cắt; bản vẽ không thay đổi.");
            return false;
        }

        // inputs are in station order and Build keeps that order (stable sort), so models[i] belongs to inputs[i].
        var models = session.Build(inputs.Select(i => i.input));
        foreach (var m in models.SelectMany(m => m.Messages).Distinct()) Say(m);

        if (session.WriteTable)
        {
            bool keep;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    var fallback = false;
                    int skippedColumns = 0, skippedTexts = 0;
                    var h = session.TextHeight;
                    for (var i = 0; i < inputs.Count; i++)
                    {
                        var v = inputs[i].view;
                        var model = models[i];
                        var d = new TaggedDrawing(tr, doc.Database, SectionTableWriter.Tool, v.Handle);
                        d.EraseTagged(new HashSet<ObjectId> { v.ViewId }, (id, tag) => tag.Kind == SectionTableWriter.TableKind);
                        var view = (SectionView)tr.GetObject(v.ViewId, OpenMode.ForRead);
                        var frame = new SectionViewFrame(tr, view);
                        var origin = frame.ToXY(frame.OffsetLeft, frame.ElevationMin);
                        var right = frame.ToXY(frame.OffsetRight, frame.ElevationMin).X;
                        if (!(right > origin.X))
                        {
                            Say($"Trắc ngang {v.Name} không có khoảng offset; bỏ qua.");
                            continue;
                        }

                        var labelChars = model.Rows.Count == 0 ? 0 : model.Rows.Max(r => r.Label.Length);
                        var layout = SectionTableLayout.Build(model, o => frame.ToXY(o, frame.ElevationMin).X,
                            top: origin.Y - 2 * h, left: origin.X, right: right, rowHeight: session.RowHeight, textHeight: h,
                            labelWidth: ProfileTableLayout.CharWidth * h * labelChars + 2 * h, rotateText: session.RotateText);
                        SectionTableWriter.Write(d, layout);
                        fallback |= frame.UsedFallback;
                        skippedColumns += layout.SkippedColumns;
                        skippedTexts += layout.SkippedTexts;
                    }

                    if (skippedColumns > 0)
                        Say($"{skippedColumns} cột offset không ghi chữ vì quá sát cột bên cạnh (hoặc nằm ngoài trắc ngang); giảm chiều cao chữ nếu cần.");
                    if (skippedTexts > 0) Say($"{skippedTexts} chữ khoảng cách bị bỏ vì ô quá hẹp.");
                    if (fallback)
                        Say("Không lấy được toạ độ từ trắc ngang (FindXYAtOffsetAndElevation); bảng đặt theo Location của trắc ngang, hãy kiểm tra vị trí.");

                    tr.TransactionManager.QueueForGraphicsFlush();
                    ed.UpdateScreen();
                    keep = !askToKeep || Prompts.AskKeep(ed);
                }
                catch (System.Exception ex)
                {
                    Say($"Lỗi khi vẽ bảng trắc ngang: {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
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

        TableFiles.Write(ed, session.Export, "TRACNGANG", session.WriteCsv, session.WriteXlsx, "Trắc ngang", "bảng trắc ngang");
        Say($"Hoàn thành: {session.SummaryText}.\n");
        return true;
    }
}
