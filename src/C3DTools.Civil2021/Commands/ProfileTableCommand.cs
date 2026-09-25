using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Curves;
using C3DTools.Civil2021.Drawing;
using C3DTools.Civil2021.Profiles;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Curves;
using C3DTools.Core.Presets;
using C3DTools.Core.Profiles;
using C3DTools.Core.Stations;
using C3DTools.Core.Tables;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.ProfileTableCommand))]

namespace C3DTools.Civil2021.Commands;

public class ProfileTableCommand
{
    private const string Command = "CTTRACDOC";
    private const string Tool = "TRACDOC";

    /// <summary>CTTRACDOC: the Vietnamese profile data table under a profile view (lines + text), CSV, Excel; a rerun replaces the old table.</summary>
    [CommandMethod("C3DTOOLS", "CTTRACDOC", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void ProfileTable()
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

    /// <summary>The picked profile view, its alignment's profiles by name, curve stakes and sample line stations.</summary>
    private sealed class ViewSource
    {
        public ObjectId Id { get; set; }
        public string Handle { get; set; }
        public Dictionary<string, ObjectId> Surfaces { get; } = new Dictionary<string, ObjectId>();
        public Dictionary<string, ObjectId> Designs { get; } = new Dictionary<string, ObjectId>();
        public List<StakeStation> CurveStakes { get; } = new List<StakeStation>();
        public List<StakeStation> SampleLines { get; } = new List<StakeStation>();
    }

    private static void Run()
    {
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        var messages = new List<string>();
        var preset = PresetLocator.LoadForDrawing(messages);
        foreach (var m in messages) Prompts.Say(ed, m);

        var memory = ToolWindow.Options;
        var session = new ProfileTableSession(preset)
        {
            IntervalText = memory.Get(Command, "Interval", "20"),
            WriteTable = memory.Get(Command, "Bang", true),
            WriteCsv = memory.Get(Command, "CSV", false),
            WriteXlsx = memory.Get(Command, "Excel", false),
        };
        var mode = memory.Get(Command, "Mode", (int)ProfileStationMode.AllStakes);
        if (mode >= 0 && mode <= (int)ProfileStationMode.SampleLines) session.StationMode = (ProfileStationMode)mode;
        foreach (var m in session.PresetMessages) Prompts.Say(ed, m);

        ViewSource view = null;
        var first = VerticalCurveCommand.PickFirst(ed);
        if (!first.IsNull) view = Load(doc, preset, first, session, memory.Get(Command, "Surface", ""), memory.Get(Command, "Design", "")) ?? view;

        while (true)
        {
            var window = new ProfileTableWindow(session);
            var action = window.ShowModal();
            memory.Set(Command, "Interval", session.IntervalText);
            memory.Set(Command, "Bang", session.WriteTable);
            memory.Set(Command, "CSV", session.WriteCsv);
            memory.Set(Command, "Excel", session.WriteXlsx);
            memory.Set(Command, "Mode", (int)session.StationMode);
            memory.Set(Command, "Surface", session.SurfaceProfile ?? "");
            memory.Set(Command, "Design", session.DesignProfile ?? "");
            ToolWindow.SaveOptions();

            switch (action)
            {
                case DialogAction.Pick:
                    var picked = Prompts.PickEntity<ProfileView>(ed, "Chọn trắc dọc (profile view): ");
                    if (picked.IsNull) continue;
                    view = Load(doc, preset, picked, session, session.SurfaceProfile, session.DesignProfile) ?? view;
                    continue;
                case DialogAction.Preview:
                case DialogAction.Apply:
                    if (view == null || !session.CanApply) continue;
                    if (Write(doc, view, session, askToKeep: action == DialogAction.Preview)) return;
                    continue;   // Khong or an error: back to the dialog
                default:
                    return;
            }
        }
    }

    /// <summary>Reads the view, its alignment's profiles, curve stakes and sample lines into the session; null (with a message) on failure.</summary>
    private static ViewSource Load(Document doc, ProjectPreset preset, ObjectId id, ProfileTableSession session, string surface, string design)
    {
        var ed = doc.Editor;
        try
        {
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var pv = (ProfileView)tr.GetObject(id, OpenMode.ForRead);
                var alignment = (Alignment)tr.GetObject(pv.AlignmentId, OpenMode.ForRead);
                var source = new ViewSource { Id = id, Handle = pv.Handle.ToString() };
                foreach (var (profileId, name, type) in ProfileReader.ProfilesOf(tr, alignment))
                {
                    var target = type == ProfileType.EG ? source.Surfaces : source.Designs;
                    if (!target.ContainsKey(name)) target[name] = profileId;
                }

                double start = pv.StationStart, end = pv.StationEnd;
                if (!(end > start))
                {
                    Prompts.Say(ed, $"Trắc dọc {pv.Name} không có khoảng lý trình.");
                    return null;
                }

                ReadCurveStakes(doc, preset, alignment, start, end, source.CurveStakes);
                ReadSampleLines(tr, alignment, ed, source.SampleLines);
                tr.Commit();

                session.SetSource($"{pv.Name} (tuyến {alignment.Name}, {StationFormatter.Format(start, 2)} – {StationFormatter.Format(end, 2)})", start, end);
                session.SetProfiles(source.Surfaces.Keys, source.Designs.Keys, surface, design);
                if (source.Surfaces.Count == 0 && source.Designs.Count == 0) Prompts.Say(ed, $"Tuyến {alignment.Name} chưa có trắc dọc.");
                return source;
            }
        }
        catch (System.Exception ex)
        {
            Prompts.Say(ed, $"Không đọc được trắc dọc: {ex.Message}");
            return null;
        }
    }

    /// <summary>NĐ/TĐ/P/TC/NC of the alignment's own curves, as CTTOADO reads them. A failure leaves only interval stakes.</summary>
    private static void ReadCurveStakes(Document doc, ProjectPreset preset, Alignment alignment, double start, double end, List<StakeStation> result)
    {
        try
        {
            var source = new AlignmentSource(doc, alignment.ObjectId, alignment.Handle.ToString(), alignment.Name);
            var curves = new CurveDesignSession(preset);
            source.LoadInto(curves);
            if (curves.Design == null) return;
            var asBuilt = AsBuiltDesign.Read(alignment, source, curves.Design, new List<BoxSite>(), doc.Editor);
            foreach (var stake in RouteStakes.FromStations(asBuilt, alignment.StartingStation, alignment.EndingStation))
            {
                if (stake.Kind != StakeKind.Start && stake.Kind != StakeKind.End && stake.Station >= start && stake.Station <= end)
                    result.Add(new StakeStation(stake.Station, stake.Name, StakeOrigin.Curve));
            }
        }
        catch (System.Exception ex)
        {
            Prompts.Say(doc.Editor, $"Không đọc được đường cong của tuyến ({ex.Message}); chỉ có cọc chi tiết.");
        }
    }

    /// <summary>Station and name of every sample line of the alignment's sample line groups. A failure leaves the list as far as it got.</summary>
    private static void ReadSampleLines(Transaction tr, Alignment alignment, Autodesk.AutoCAD.EditorInput.Editor ed, List<StakeStation> result)
    {
        try
        {
            foreach (ObjectId groupId in alignment.GetSampleLineGroupIds())
            {
                if (!(tr.GetObject(groupId, OpenMode.ForRead) is SampleLineGroup group)) continue;
                foreach (ObjectId lineId in group.GetSampleLineIds())
                {
                    if (tr.GetObject(lineId, OpenMode.ForRead) is SampleLine line)
                        result.Add(new StakeStation(line.Station, line.Name, StakeOrigin.Extra));
                }
            }
        }
        catch (System.Exception ex)
        {
            Prompts.Say(ed, $"Không đọc được cọc mặt cắt (sample line): {ex.Message}");
        }
    }

    /// <summary>Reads elevations at the stations, builds the table, draws it (one transaction, one undo) and writes CSV/Excel. True when kept.</summary>
    private static bool Write(Document doc, ViewSource view, ProfileTableSession session, bool askToKeep)
    {
        var ed = doc.Editor;
        List<StakeStation> stations;
        try
        {
            stations = session.Stations(view.CurveStakes, view.SampleLines);
        }
        catch (System.ArgumentException ex)
        {
            Prompts.Say(ed, ex.Message);
            return false;
        }

        if (stations.Count == 0)
        {
            Prompts.Say(ed, session.IsSampleLines ? "Tuyến không có cọc mặt cắt (sample line) trong khoảng lý trình của trắc dọc." : "Không có cọc nào.");
            return false;
        }

        ProfileTableModel model;
        using (var tr = doc.TransactionManager.StartTransaction())
        {
            var ground = Elevations(tr, ed, view.Surfaces, session.SurfaceProfile, stations, "tự nhiên");
            var design = Elevations(tr, ed, view.Designs, session.DesignProfile, stations, "thiết kế");
            List<ProfileSegment> segments = null;
            if (session.DesignProfile != null && view.Designs.TryGetValue(session.DesignProfile, out var designId))
            {
                try
                {
                    segments = ProfileReader.Read((Profile)tr.GetObject(designId, OpenMode.ForRead));
                }
                catch (System.Exception ex)
                {
                    Prompts.Say(ed, $"Không đọc được các đoạn của trắc dọc {session.DesignProfile} ({ex.Message}); dòng dốc dọc và cong đứng để trống.");
                }
            }

            model = session.Build(stations, ground, design, segments);
            tr.Commit();
        }

        foreach (var m in model.Messages) Prompts.Say(ed, m);

        if (session.WriteTable)
        {
            bool keep;
            var fallback = false;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    var d = new TaggedDrawing(tr, doc.Database, Tool, view.Handle);
                    d.EraseTagged(new HashSet<ObjectId> { view.Id }, (id, tag) => tag.Kind == ProfileTableWriter.TableKind);
                    var pv = (ProfileView)tr.GetObject(view.Id, OpenMode.ForRead);
                    var frame = new ProfileViewFrame(tr, pv);
                    var h = session.TextHeight;
                    var labelChars = model.Rows.Count == 0 ? 0 : model.Rows.Max(r => r.Label.Length);
                    var origin = frame.ToXY(frame.StationStart, frame.ElevationMin);
                    // A gap of 2 text heights below the view keeps the table clear of the view's own axis labels.
                    var layout = ProfileTableLayout.Build(model, s => frame.ToXY(s, frame.ElevationMin).X,
                        top: origin.Y - 2 * h, left: origin.X, right: frame.ToXY(frame.StationEnd, frame.ElevationMin).X,
                        rowHeight: session.RowHeight, textHeight: h, labelWidth: 0.7 * h * labelChars + 2 * h,
                        rotateStationText: session.RotateStationText);
                    ProfileTableWriter.Write(d, layout, Tool);
                    fallback = frame.UsedFallback;

                    tr.TransactionManager.QueueForGraphicsFlush();
                    ed.UpdateScreen();
                    if (fallback)
                        Prompts.Say(ed, "Không lấy được toạ độ từ trắc dọc (FindXYAtStationAndElevation); bảng đặt theo gốc trắc dọc, hãy kiểm tra vị trí.");
                    keep = !askToKeep || Prompts.AskKeep(ed);
                }
                catch (System.Exception ex)
                {
                    Prompts.Say(ed, $"Lỗi khi vẽ bảng trắc dọc: {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
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

        TableFiles.Write(ed, model.Export, "TRACDOC", session.WriteCsv, session.WriteXlsx, "Trắc dọc", "bảng trắc dọc");
        Prompts.Say(ed, $"Hoàn thành: bảng trắc dọc {session.SummaryText}.\n");
        return true;
    }

    /// <summary>Profile.ElevationAt per station; null where it fails (outside the profile) or when no profile is chosen.</summary>
    private static List<double?> Elevations(Transaction tr, Autodesk.AutoCAD.EditorInput.Editor ed, Dictionary<string, ObjectId> ids, string name,
        List<StakeStation> stations, string kind)
    {
        if (name == null || !ids.TryGetValue(name, out var id)) return null;
        Profile profile;
        try
        {
            profile = (Profile)tr.GetObject(id, OpenMode.ForRead);
        }
        catch (System.Exception ex)
        {
            Prompts.Say(ed, $"Không mở được trắc dọc {kind} {name}: {ex.Message}");
            return null;
        }

        var result = new List<double?>();
        var missing = 0;
        foreach (var s in stations)
        {
            double? z = null;
            try
            {
                var v = profile.ElevationAt(s.Station);
                if (!double.IsNaN(v) && !double.IsInfinity(v)) z = v;
            }
            catch (System.Exception)
            {
                // Station outside the profile's extent: empty cell.
            }

            if (z == null) missing++;
            result.Add(z);
        }

        if (missing > 0) Prompts.Say(ed, $"{missing} cọc nằm ngoài trắc dọc {kind} {name}: ô cao độ để trống.");
        return result;
    }
}
