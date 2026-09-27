using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Curves;
using C3DTools.Civil2021.Services;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Presets;
using C3DTools.Core.Stations;
using C3DTools.Core.Tables;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.StakeCommands))]

namespace C3DTools.Civil2021.Commands;

/// <summary>CTPHATCOC (phát sinh / chèn cọc) and CTDANHCOC (đánh lại tên cọc): stakes as Civil 3D sample lines.</summary>
public class StakeCommands
{
    [CommandMethod("C3DTOOLS", "CTPHATCOC", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void GenerateStakes() => Guard(RunGenerate);

    [CommandMethod("C3DTOOLS", "CTDANHCOC", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void RenameStakes() => Guard(RunRename);

    private static void Guard(Action run)
    {
        try
        {
            ToolWindow.Trace("=== lệnh cọc bắt đầu, C3DTools " + typeof(StakeCommands).Assembly.GetName().Version);
            run();
            ToolWindow.Trace("=== lệnh cọc kết thúc");
        }
        catch (System.Exception ex)
        {
            ToolWindow.Trace("=== lệnh cọc lỗi: " + ex.Message);
            // Last resort: never let an exception reach AutoCAD's unhandled-exception dialog.
            ToolWindow.LogError("CTPHATCOC/CTDANHCOC", ex);
            Prompts.Say(AcCoreApp.DocumentManager.MdiActiveDocument?.Editor, $"Lỗi C3DTools: {ex.Message} Chi tiết: {ToolWindow.ErrorLogPath}");
        }
    }

    /// <summary>The sample line group CTDANHCOC works on: its lines in the session's order, and its alignment.</summary>
    private sealed class StakeGroup
    {
        public ObjectId GroupId { get; set; }
        public ObjectId AlignmentId { get; set; }
        public List<ObjectId> Lines { get; set; }
    }

    /// <summary>The picked alignment, its sample line groups and its curves.</summary>
    private sealed class Route
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public List<(ObjectId id, string name)> Groups { get; set; }
        public AlignmentCurves Curves { get; set; }
    }

    // ---------------------------------------------------------------- CTPHATCOC

    private static void RunGenerate()
    {
        const string command = "CTPHATCOC";
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        var preset = LoadPreset(ed);
        var memory = ToolWindow.Options;
        var session = new StakeGenerationSession(preset)
        {
            StraightSpacingText = memory.Get(command, "Straight", "20"),
            CurveSpacingText = memory.Get(command, "Curve", "10"),
            HalfWidthText = memory.Get(command, "HalfWidth", "60"),
            SubStakeStyle = memory.Get(command, "SubStake", false),
            WriteLabels = memory.Get(command, "Labels", true),
            AlternateSides = memory.Get(command, "Alternate", true),
            LabelStations = memory.Get(command, "LabelStations", false),
        };

        Route route = null;
        var first = PickFirst<Alignment>(ed);
        if (!first.IsNull) route = LoadRoute(doc, preset, first, session);

        while (true)
        {
            var action = new StakeGenerateWindow(session).ShowModal();
            memory.Set(command, "Straight", session.StraightSpacingText);
            memory.Set(command, "Curve", session.CurveSpacingText);
            memory.Set(command, "HalfWidth", session.HalfWidthText);
            memory.Set(command, "SubStake", session.SubStakeStyle);
            memory.Set(command, "Labels", session.WriteLabels);
            memory.Set(command, "Alternate", session.AlternateSides);
            memory.Set(command, "LabelStations", session.LabelStations);
            ToolWindow.SaveOptions();

            switch (action)
            {
                case DialogAction.Pick:
                    var picked = Prompts.PickEntity<Alignment>(ed, "Chọn alignment: ");
                    if (!picked.IsNull) route = LoadRoute(doc, preset, picked, session) ?? route;
                    continue;
                case DialogAction.PickFrom:
                case DialogAction.PickTo:
                    if (route == null) continue;
                    var station = PickStation(doc, route, action == DialogAction.PickFrom ? "Điểm đầu khoảng phát sinh: " : "Điểm cuối khoảng phát sinh: ");
                    if (station == null) continue;
                    if (action == DialogAction.PickFrom) session.SetFrom(station.Value);
                    else session.SetTo(station.Value);
                    continue;
                case DialogAction.PickPoints:
                    if (route == null) continue;
                    foreach (var s in PickStations(doc, route)) session.AddInsertStation(s);
                    continue;
                case DialogAction.Preview:
                    if (route != null && session.CanApply) PreviewGenerate(doc, route, session);
                    continue;
                case DialogAction.Apply:
                    if (route == null || !session.CanApply || !PreviewGenerate(doc, route, session)) continue;
                    if (WriteGenerate(doc, route, session, preset)) return;
                    continue;
                default:
                    return;
            }
        }
    }

    private static Route LoadRoute(Document doc, ProjectPreset preset, ObjectId id, StakeGenerationSession session)
    {
        try
        {
            ToolWindow.Trace("CTPHATCOC: đọc alignment");
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var alignment = (Alignment)tr.GetObject(id, OpenMode.ForRead);
                var route = new Route
                {
                    Id = id,
                    Name = alignment.Name,
                    Groups = SampleLineStakes.Groups(tr, alignment),
                    Curves = AlignmentCurves.Read(doc, preset, alignment),
                };
                session.SetSource($"Alignment {alignment.Name} ({NumberFormat.Fixed(alignment.EndingStation - alignment.StartingStation, 2)} m)",
                    alignment.StartingStation, alignment.EndingStation, route.Groups.Select(g => g.name));
                if (session.IsNewGroup && session.NewGroupName.Length == 0) session.NewGroupName = alignment.Name + "-COC";
                tr.Commit();
                ToolWindow.Trace($"CTPHATCOC: đã đọc {route.Name}, {route.Groups.Count} nhóm cọc, {route.Curves.Keys.Count} cọc chủ yếu");
                return route;
            }
        }
        catch (System.Exception ex)
        {
            Prompts.Say(doc.Editor, $"Không đọc được alignment: {ex.Message}");
            return null;
        }
    }

    /// <summary>Plans with the current options and fills the preview; false with a message when the options can't be planned.</summary>
    private static bool PreviewGenerate(Document doc, Route route, StakeGenerationSession session)
    {
        try
        {
            ToolWindow.Trace($"CTPHATCOC: xem trước, nhóm '{session.Group ?? "(mới)"}', chèn={session.InsertMode}, từ {session.FromText} tới {session.ToText}, thẳng {session.StraightSpacingText}, cong {session.CurveSpacingText}");
            var existing = ExistingStakes(doc, route, session.Group);
            session.SetPreview(session.Plan(existing, route.Curves.Zones, route.Curves.Keys), existing);
            ToolWindow.Trace($"CTPHATCOC: xem trước xong, {session.Planned.Count} cọc ({existing.Count} cọc có sẵn)");
            return true;
        }
        catch (ArgumentException ex)
        {
            Prompts.Say(doc.Editor, ex.Message);
            return false;
        }
    }

    private static bool WriteGenerate(Document doc, Route route, StakeGenerationSession session, ProjectPreset preset)
    {
        var ed = doc.Editor;
        ToolWindow.Trace("CTPHATCOC: bắt đầu ghi cọc");
        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            try
            {
                var alignment = (Alignment)tr.GetObject(route.Id, OpenMode.ForRead);
                var groupId = session.IsNewGroup
                    ? SampleLineStakes.CreateGroup(tr, alignment, session.NewGroupName)
                    : route.Groups.First(g => g.name == session.Group).id;
                var result = SampleLineStakes.Write(tr, alignment, groupId, session.Planned, session.PlannedLabels, session.HalfWidth, m => Prompts.Say(ed, m));
                if (session.WriteLabels)
                {
                    ToolWindow.Trace("CTPHATCOC: ghi tên cọc");
                    var labelled = StakeLabelWriter.Write(tr, doc.Database, alignment, groupId, session.Planned, session.PlannedLabels,
                        StakeLabelWriter.TextHeight(alignment, preset), session.LabelOptions, m => Prompts.Say(ed, m));
                    Prompts.Say(ed, $"Đã ghi tên {labelled} cọc trên layer {StakeLabelWriter.Layer}.");
                }

                ToolWindow.Trace("CTPHATCOC: commit");
                tr.Commit();
                ToolWindow.Trace("CTPHATCOC: commit xong, " + SampleLineStakes.Summary(result));
                Prompts.Say(ed, $"Hoàn thành: {SampleLineStakes.Summary(result)}. Một lệnh U hoàn tác toàn bộ.\n");
                return true;
            }
            catch (System.Exception ex)
            {
                ToolWindow.LogError("CTPHATCOC ghi cọc", ex);
                Prompts.Say(ed, $"Lỗi khi phát sinh cọc: {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
                return false;
            }
        }
    }

    /// <summary>The chosen group's stakes, classified; empty for a new group.</summary>
    private static List<RouteStake> ExistingStakes(Document doc, Route route, string groupName)
    {
        if (groupName == null) return new List<RouteStake>();
        var groupId = route.Groups.First(g => g.name == groupName).id;
        using (var tr = doc.TransactionManager.StartTransaction())
        {
            var lines = SampleLineStakes.Read(tr, groupId);
            tr.Commit();
            return StakeClassifier.Classify(lines.Select(l => l.stake), route.Curves.Keys);
        }
    }

    private static double? PickStation(Document doc, Route route, string message)
    {
        ToolWindow.Trace("CTPHATCOC: chọn điểm lý trình");
        // Pick first, open the alignment after: no object stays open while the user pans, zooms or cancels.
        var point = Prompts.PickPoint(doc.Editor, message);
        if (point == null) return null;
        using (var tr = doc.TransactionManager.StartTransaction())
        {
            var alignment = (Alignment)tr.GetObject(route.Id, OpenMode.ForRead);
            double? result = null;
            try
            {
                double station = 0, offset = 0;
                alignment.StationOffset(point.Value.X, point.Value.Y, ref station, ref offset);
                result = Math.Max(alignment.StartingStation, Math.Min(alignment.EndingStation, station));
            }
            catch (System.Exception)
            {
                Prompts.Say(doc.Editor, "Điểm chọn nằm ngoài phạm vi alignment.");
            }

            tr.Commit();
            return result;
        }
    }

    /// <summary>"Chỉ điểm…" in Chèn mode: points picked one after another, as stations.</summary>
    private static List<double> PickStations(Document doc, Route route)
    {
        var result = new List<double>();
        var points = Prompts.PickPoints(doc.Editor, "Điểm cọc cần chèn: ", "Điểm cọc tiếp theo [Lui] <Enter: xong>: ");
        if (points == null) return result;
        using (var tr = doc.TransactionManager.StartTransaction())
        {
            var alignment = (Alignment)tr.GetObject(route.Id, OpenMode.ForRead);
            foreach (var p in points)
            {
                try
                {
                    double station = 0, offset = 0;
                    alignment.StationOffset(p.X, p.Y, ref station, ref offset);
                    result.Add(Math.Max(alignment.StartingStation, Math.Min(alignment.EndingStation, station)));
                }
                catch (System.Exception)
                {
                    Prompts.Say(doc.Editor, "Một điểm nằm ngoài phạm vi alignment: bỏ qua.");
                }
            }

            tr.Commit();
        }

        return result;
    }

    // ---------------------------------------------------------------- CTDANHCOC

    private static void RunRename()
    {
        const string command = "CTDANHCOC";
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        var preset = LoadPreset(ed);
        var memory = ToolWindow.Options;
        var session = new StakeRenameSession(preset)
        {
            DetailPrefix = memory.Get(command, "Prefix", "C"),
            KeepPrefixesText = memory.Get(command, "Keep", ""),
            RenameCurveKeys = memory.Get(command, "CurveKeys", true),
            NameByStation = memory.Get(command, "ByStation", false),
            NoHundreds = memory.Get(command, "NoH", false),
            ContinuousThroughH = memory.Get(command, "ContinuousH", true),
            RestartPerKm = memory.Get(command, "RestartKm", true),
            NoRestartFrom100 = memory.Get(command, "No100", true),
            WriteLabels = memory.Get(command, "Labels", true),
            AlternateSides = memory.Get(command, "Alternate", true),
            LabelStations = memory.Get(command, "LabelStations", false),
        };

        StakeGroup ids = null;
        var first = PickFirst<Autodesk.AutoCAD.DatabaseServices.Entity>(ed);
        if (!first.IsNull) ids = LoadGroup(doc, preset, first, session) ?? ids;

        while (true)
        {
            var action = new StakeRenameWindow(session).ShowModal();
            memory.Set(command, "Prefix", session.DetailPrefix);
            memory.Set(command, "Keep", session.KeepPrefixesText);
            memory.Set(command, "CurveKeys", session.RenameCurveKeys);
            memory.Set(command, "ByStation", session.NameByStation);
            memory.Set(command, "NoH", session.NoHundreds);
            memory.Set(command, "ContinuousH", session.ContinuousThroughH);
            memory.Set(command, "RestartKm", session.RestartPerKm);
            memory.Set(command, "No100", session.NoRestartFrom100);
            memory.Set(command, "Labels", session.WriteLabels);
            memory.Set(command, "Alternate", session.AlternateSides);
            memory.Set(command, "LabelStations", session.LabelStations);
            ToolWindow.SaveOptions();

            switch (action)
            {
                case DialogAction.Pick:
                    var picked = PickGroupObject(ed);
                    if (!picked.IsNull) ids = LoadGroup(doc, preset, picked, session) ?? ids;
                    continue;
                case DialogAction.Preview:
                case DialogAction.Apply:
                    if (ids == null || !session.CanApply) continue;
                    if (WriteRename(doc, ids, session, preset, askToKeep: action == DialogAction.Preview)) return;
                    continue;
                default:
                    return;
            }
        }
    }

    private static ObjectId PickGroupObject(Editor ed)
    {
        var options = new PromptEntityOptions("\nChọn một cọc (Sample Line) hoặc alignment: ");
        options.SetRejectMessage("\nChỉ chọn Sample Line hoặc alignment.");
        options.AddAllowedClass(typeof(SampleLine), false);
        options.AddAllowedClass(typeof(Alignment), false);
        var result = ed.GetEntity(options);
        return result.Status == PromptStatus.OK ? result.ObjectId : ObjectId.Null;
    }

    /// <summary>
    /// The group of a picked sample line, or of a picked alignment (asked on the command line when it has several).
    /// Fills the session; returns the sample lines in the session's order, or null with a message.
    /// </summary>
    private static StakeGroup LoadGroup(Document doc, ProjectPreset preset, ObjectId picked, StakeRenameSession session)
    {
        var ed = doc.Editor;
        try
        {
            var civil = CivilDocument.GetCivilDocument(doc.Database);
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var obj = tr.GetObject(picked, OpenMode.ForRead);
                ObjectId groupId;
                Alignment alignment;
                if (obj is SampleLine line)
                {
                    groupId = line.GroupId;
                    alignment = civil.GetAlignmentIds().Cast<ObjectId>()
                        .Select(id => (Alignment)tr.GetObject(id, OpenMode.ForRead))
                        .FirstOrDefault(a => a.GetSampleLineGroupIds().Cast<ObjectId>().Contains(groupId));
                    if (alignment == null) throw new InvalidOperationException("không tìm thấy alignment của nhóm cọc");
                }
                else if (obj is Alignment a)
                {
                    alignment = a;
                    var groups = SampleLineStakes.Groups(tr, a);
                    if (groups.Count == 0) throw new InvalidOperationException($"alignment {a.Name} chưa có nhóm cọc (chạy CTPHATCOC trước)");
                    groupId = groups.Count == 1 ? groups[0].id : HostServices.PromptNamedObject(ed, tr, "nhóm cọc", new ObjectIdCollection(groups.Select(g => g.id).ToArray()));
                }
                else
                {
                    return null;
                }

                var lines = SampleLineStakes.Read(tr, groupId);
                var groupName = ((SampleLineGroup)tr.GetObject(groupId, OpenMode.ForRead)).Name;
                var curves = AlignmentCurves.Read(doc, preset, alignment);
                tr.Commit();
                session.SetStakes($"{groupName} ({alignment.Name}, {lines.Count} cọc)", StakeClassifier.Classify(lines.Select(l => l.stake), curves.Keys));
                return new StakeGroup { GroupId = groupId, AlignmentId = alignment.ObjectId, Lines = lines.Select(l => l.id).ToList() };
            }
        }
        catch (System.Exception ex)
        {
            Prompts.Say(ed, $"Không đọc được nhóm cọc: {ex.Message}");
            return null;
        }
    }

    private static bool WriteRename(Document doc, StakeGroup group, StakeRenameSession session, ProjectPreset preset, bool askToKeep)
    {
        var ed = doc.Editor;
        bool keep;
        ToolWindow.Trace("CTDANHCOC: bắt đầu đổi tên");
        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            try
            {
                SampleLineStakes.Rename(tr, group.Lines, session.NewNames);
                if (session.WriteLabels)
                {
                    ToolWindow.Trace("CTDANHCOC: ghi tên cọc");
                    var alignment = (Alignment)tr.GetObject(group.AlignmentId, OpenMode.ForRead);
                    var labelled = StakeLabelWriter.Write(tr, doc.Database, alignment, group.GroupId, session.Stakes, session.NewNames,
                        StakeLabelWriter.TextHeight(alignment, preset), session.LabelOptions, m => Prompts.Say(ed, m));
                    Prompts.Say(ed, $"Đã ghi tên {labelled} cọc trên layer {StakeLabelWriter.Layer}.");
                }

                tr.TransactionManager.QueueForGraphicsFlush();
                ed.UpdateScreen();
                keep = !askToKeep || Prompts.AskKeep(ed);
            }
            catch (System.Exception ex)
            {
                Prompts.Say(ed, $"Lỗi khi đổi tên cọc: {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
                return false;
            }

            if (keep) tr.Commit();
            else tr.Abort();
        }

        if (keep) Prompts.Say(ed, $"Hoàn thành: {session.ChangedCount} cọc đổi tên. Một lệnh U hoàn tác toàn bộ.\n");
        return keep;
    }

    // ---------------------------------------------------------------- shared

    private static ProjectPreset LoadPreset(Editor ed)
    {
        var messages = new List<string>();
        var preset = PresetLocator.LoadForDrawing(messages);
        foreach (var m in messages) Prompts.Say(ed, m);
        return preset;
    }

    /// <summary>The first object of type T selected before the command, or ObjectId.Null.</summary>
    private static ObjectId PickFirst<T>(Editor ed)
    {
        var implied = ed.SelectImplied();
        if (implied.Status != PromptStatus.OK || implied.Value == null) return ObjectId.Null;
        ed.SetImpliedSelection(new ObjectId[0]);
        var cls = RXObject.GetClass(typeof(T));
        return implied.Value.GetObjectIds().FirstOrDefault(id => id.ObjectClass.IsDerivedFrom(cls));
    }
}
