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
        StakeGenerationSession NewSession()
        {
            var s = new StakeGenerationSession(preset);
            s.StraightSpacingText = memory.Get(command, "Straight", s.StraightSpacingText);
            s.CurveSpacingText = memory.Get(command, "Curve", s.CurveSpacingText);
            s.DensifyCurves = memory.Get(command, "Densify", s.DensifyCurves);
            s.HalfWidthText = memory.Get(command, "HalfWidth", s.HalfWidthText);
            s.SubStakeStyle = memory.Get(command, "SubStake", s.SubStakeStyle);
            s.NoHundreds = memory.Get(command, "NoH", s.NoHundreds);
            s.WriteLabels = memory.Get(command, "Labels", s.WriteLabels);
            s.AlternateSides = memory.Get(command, "AlternateEnds", s.AlternateSides);
            s.StationModeIndex = memory.Get(command, "StationMode", s.StationModeIndex);
            s.SkipHundredPositions = memory.Get(command, "SkipH", s.SkipHundredPositions);
            s.PlainCurveNames = memory.Get(command, "PlainCurveNames", s.PlainCurveNames);
            return s;
        }

        var session = NewSession();
        Route route = null;
        var first = RoutePicker.Resolve(doc, out var why);
        if (why != null) Prompts.Say(ed, why);
        if (!first.IsNull) route = LoadRoute(doc, preset, first, session);
        if (route != null) RoutePicker.Remember(doc, route.Id);

        while (true)
        {
            var action = new StakeGenerateWindow(session).ShowModal();
            if (action == DialogAction.Reset)
            {
                // Not saved first: the values being reset must not be written back.
                ToolWindow.ResetOptions(command);
                session = NewSession();
                if (route != null) route = LoadRoute(doc, preset, route.Id, session) ?? route;
                if (route != null) RoutePicker.Remember(doc, route.Id);
                continue;
            }

            memory.Set(command, "Straight", session.StraightSpacingText);
            memory.Set(command, "Curve", session.CurveSpacingText);
            memory.Set(command, "Densify", session.DensifyCurves);
            memory.Set(command, "HalfWidth", session.HalfWidthText);
            memory.Set(command, "SubStake", session.SubStakeStyle);
            memory.Set(command, "NoH", session.NoHundreds);
            memory.Set(command, "Labels", session.WriteLabels);
            memory.Set(command, "AlternateEnds", session.AlternateSides);
            memory.Set(command, "StationMode", session.StationModeIndex);
            memory.Set(command, "SkipH", session.SkipHundredPositions);
            memory.Set(command, "PlainCurveNames", session.PlainCurveNames);
            ToolWindow.SaveOptions();

            switch (action)
            {
                case DialogAction.Pick:
                    var picked = Prompts.PickEntity<Alignment>(ed, "Chọn alignment: ");
                    if (picked.IsNull) continue;
                    route = LoadRoute(doc, preset, picked, session) ?? route;
                    if (route != null) RoutePicker.Remember(doc, route.Id);
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
            ToolWindow.Trace($"CTPHATCOC: xem trước, nhóm '{session.Group ?? "(mới)"}', chèn={session.InsertMode}, từ {session.FromText} tới {session.ToText}, cọc C {session.StraightSpacingText} từ {session.DetailStartText}, chêm cong={session.DensifyCurves} {session.CurveSpacingText}");
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
                    Prompts.Say(ed, $"Đã ghi tên {labelled.names} cọc, lý trình tại {labelled.stations} cọc ({StakeLabelOptions.StationModes[session.StationModeIndex]}), layer {StakeLabelWriter.Layer}.");
                    ToolWindow.Trace($"ghi tên cọc: {labelled.names} tên, {labelled.stations} lý trình, chế độ {session.StationModeIndex}, xen kẽ={session.AlternateSides}");
                }

                ToolWindow.Trace("CTPHATCOC: commit");
                RoutePicker.Save(tr, doc);
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
        StakeRenameSession NewSession()
        {
            var s = new StakeRenameSession(preset);
            s.DetailPrefix = memory.Get(command, "Prefix", s.DetailPrefix);
            s.KeepPrefixesText = memory.Get(command, "Keep", s.KeepPrefixesText);
            s.RenameCurveKeys = memory.Get(command, "CurveKeys", s.RenameCurveKeys);
            s.NameByStation = memory.Get(command, "ByStation", s.NameByStation);
            s.NoHundreds = memory.Get(command, "NoH", s.NoHundreds);
            s.ContinuousThroughH = memory.Get(command, "ContinuousH", s.ContinuousThroughH);
            s.RestartPerKm = memory.Get(command, "RestartPerKm", s.RestartPerKm);
            s.NoRestartFrom100 = memory.Get(command, "No100", s.NoRestartFrom100);
            s.WriteLabels = memory.Get(command, "Labels", s.WriteLabels);
            s.AlternateSides = memory.Get(command, "AlternateEnds", s.AlternateSides);
            s.StationModeIndex = memory.Get(command, "StationMode", s.StationModeIndex);
            s.SkipHundredPositions = memory.Get(command, "SkipH", s.SkipHundredPositions);
            s.PlainCurveNames = memory.Get(command, "PlainCurveNames", s.PlainCurveNames);
            return s;
        }

        var session = NewSession();
        StakeGroup ids = null;
        // A sample line or alignment selected before the command; else the active route.
        var presel = PickFirst<SampleLine>(ed);
        if (presel.IsNull) presel = PickFirst<Alignment>(ed);
        ObjectId source;
        if (!presel.IsNull)
        {
            source = presel;
        }
        else
        {
            source = RoutePicker.Resolve(doc, out var why);
            if (why != null) Prompts.Say(ed, why);
        }

        if (!source.IsNull) ids = LoadGroup(doc, preset, source, session) ?? ids;
        if (ids != null) RoutePicker.Remember(doc, ids.AlignmentId);

        while (true)
        {
            var action = new StakeRenameWindow(session).ShowModal();
            if (action == DialogAction.Reset)
            {
                // Not saved first: the values being reset must not be written back. The group stays.
                ToolWindow.ResetOptions(command);
                session = NewSession();
                if (ids != null)
                {
                    var reloaded = ReloadGroup(doc, preset, ids.GroupId, ids.AlignmentId, session);
                    if (reloaded != null) { ids = reloaded; RoutePicker.Remember(doc, ids.AlignmentId); }
                    else Prompts.Say(ed, "Không tải lại được nhóm cọc; giữ lại nhóm cũ.");
                }
                continue;
            }

            memory.Set(command, "Prefix", session.DetailPrefix);
            memory.Set(command, "Keep", session.KeepPrefixesText);
            memory.Set(command, "CurveKeys", session.RenameCurveKeys);
            memory.Set(command, "ByStation", session.NameByStation);
            memory.Set(command, "NoH", session.NoHundreds);
            memory.Set(command, "ContinuousH", session.ContinuousThroughH);
            memory.Set(command, "RestartPerKm", session.RestartPerKm);
            memory.Set(command, "No100", session.NoRestartFrom100);
            memory.Set(command, "Labels", session.WriteLabels);
            memory.Set(command, "AlternateEnds", session.AlternateSides);
            memory.Set(command, "StationMode", session.StationModeIndex);
            memory.Set(command, "SkipH", session.SkipHundredPositions);
            memory.Set(command, "PlainCurveNames", session.PlainCurveNames);
            ToolWindow.SaveOptions();

            switch (action)
            {
                case DialogAction.Pick:
                    var picked = PickGroupObject(ed);
                    if (picked.IsNull) continue;
                    var loaded = LoadGroup(doc, preset, picked, session);
                    if (loaded == null) continue;
                    ids = loaded;
                    source = picked;
                    RoutePicker.Remember(doc, ids.AlignmentId);
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

    /// <summary>
    /// Reloads a known group by id without prompting the user. Used by the Reset path so reset never re-asks
    /// which group to use. Returns null (with a message) when the group or alignment can no longer be read.
    /// </summary>
    private static StakeGroup ReloadGroup(Document doc, ProjectPreset preset, ObjectId groupId, ObjectId alignmentId, StakeRenameSession session)
    {
        var ed = doc.Editor;
        try
        {
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var alignment = (Alignment)tr.GetObject(alignmentId, OpenMode.ForRead);
                var lines = SampleLineStakes.Read(tr, groupId);
                var groupName = ((SampleLineGroup)tr.GetObject(groupId, OpenMode.ForRead)).Name;
                var curves = AlignmentCurves.Read(doc, preset, alignment);
                tr.Commit();
                session.SetStakes($"{groupName} ({alignment.Name}, {lines.Count} cọc)", StakeClassifier.Classify(lines.Select(l => l.stake), curves.Keys));
                return new StakeGroup { GroupId = groupId, AlignmentId = alignmentId, Lines = lines.Select(l => l.id).ToList() };
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
                    Prompts.Say(ed, $"Đã ghi tên {labelled.names} cọc, lý trình tại {labelled.stations} cọc ({StakeLabelOptions.StationModes[session.StationModeIndex]}), layer {StakeLabelWriter.Layer}.");
                    ToolWindow.Trace($"ghi tên cọc: {labelled.names} tên, {labelled.stations} lý trình, chế độ {session.StationModeIndex}, xen kẽ={session.AlternateSides}");
                }

                RoutePicker.Save(tr, doc);
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
