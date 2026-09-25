using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Curves;
using C3DTools.Civil2021.Drainage;
using C3DTools.Civil2021.Drawing;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Drainage;
using C3DTools.Core.Presets;
using C3DTools.Core.Tables;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using CivilSurface = Autodesk.Civil.DatabaseServices.Surface;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.CulvertTableCommand))]

namespace C3DTools.Civil2021.Commands;

public class CulvertTableCommand
{
    private const string Command = "CTBANGCONG";
    private const string Tool = "BANGCONG";
    private const string TableLayer = "BANGCONG_BANG";
    private const string Title = "BẢNG THỐNG KÊ CỐNG";
    private const short TableKind = 1;
    private const char NameSeparator = '|';

    /// <summary>CTBANGCONG: culvert schedule of the pipe networks crossing an alignment (AutoCAD Table, CSV, Excel); a rerun replaces the old table.</summary>
    [CommandMethod("C3DTOOLS", "CTBANGCONG", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void CulvertTable()
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

    /// <summary>The picked alignment.</summary>
    private sealed class Route
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
        var session = new CulvertSession(preset)
        {
            WriteTable = memory.Get(Command, "Bang", true),
            WriteCsv = memory.Get(Command, "CSV", false),
            WriteXlsx = memory.Get(Command, "Excel", false),
        };

        var networks = ReadNetworks(doc);
        session.SetNetworks(networks.Select(n => n.name), memory.Get(Command, "Networks", "").Split(NameSeparator));
        var surfaces = ReadSurfaces(doc);
        session.SetSurfaces(surfaces.Select(s => s.name), memory.Get(Command, "Surface", ""));
        if (networks.Count == 0) Prompts.Say(ed, "Bản vẽ không có mạng cống (pipe network).");

        Route route = null;
        var first = PickFirst(ed);
        if (!first.IsNull) route = Load(doc, first, session);
        if (route != null && session.CanApply) Preview(doc, route, session, networks, surfaces, preset);

        while (true)
        {
            var window = new CulvertTableWindow(session);
            var action = window.ShowModal();
            memory.Set(Command, "Bang", session.WriteTable);
            memory.Set(Command, "CSV", session.WriteCsv);
            memory.Set(Command, "Excel", session.WriteXlsx);
            memory.Set(Command, "Networks", string.Join(NameSeparator.ToString(), session.SelectedNetworks));
            memory.Set(Command, "Surface", session.Surface ?? "");
            ToolWindow.SaveOptions();

            switch (action)
            {
                case DialogAction.Pick:
                    var picked = Prompts.PickEntity<Alignment>(ed, "Chọn alignment: ");
                    if (picked.IsNull) continue;
                    var loaded = Load(doc, picked, session);
                    if (loaded == null) continue;
                    route = loaded;
                    if (session.CanApply) Preview(doc, route, session, networks, surfaces, preset);
                    continue;
                case DialogAction.Preview:
                    if (route != null && session.CanApply) Preview(doc, route, session, networks, surfaces, preset);
                    continue;
                case DialogAction.Apply:
                    if (route == null || !session.CanApply) continue;
                    if (session.IsStale && !Preview(doc, route, session, networks, surfaces, preset)) continue;
                    if (session.Rows.Count == 0)
                    {
                        Prompts.Say(ed, "Không có cống nào cắt qua tuyến: không có gì để xuất.");
                        continue;
                    }

                    if (Write(doc, route, session, preset)) return;
                    continue;   // no insertion point or an error: back to the dialog
                default:
                    return;
            }
        }
    }

    /// <summary>The first alignment of the selection made before the command, or ObjectId.Null.</summary>
    private static ObjectId PickFirst(Editor ed)
    {
        var implied = ed.SelectImplied();
        if (implied.Status != PromptStatus.OK || implied.Value == null) return ObjectId.Null;
        ed.SetImpliedSelection(new ObjectId[0]);
        var alignmentClass = RXObject.GetClass(typeof(Alignment));
        return implied.Value.GetObjectIds().FirstOrDefault(id => id.ObjectClass.IsDerivedFrom(alignmentClass));
    }

    /// <summary>The drawing's pipe networks by name.</summary>
    private static List<(ObjectId id, string name)> ReadNetworks(Document doc)
    {
        var result = new List<(ObjectId id, string name)>();
        try
        {
            var civil = CivilDocument.GetCivilDocument(doc.Database);
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in civil.GetPipeNetworkIds())
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is Network network) result.Add((id, network.Name));
                }

                tr.Commit();
            }
        }
        catch (System.Exception ex)
        {
            Prompts.Say(doc.Editor, $"Không đọc được danh sách mạng cống: {ex.Message}");
        }

        return result;
    }

    /// <summary>TIN and grid surfaces of the drawing (volume surfaces have no terrain elevation).</summary>
    private static List<(ObjectId id, string name)> ReadSurfaces(Document doc)
    {
        var result = new List<(ObjectId id, string name)>();
        try
        {
            var civil = CivilDocument.GetCivilDocument(doc.Database);
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in civil.GetSurfaceIds())
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is CivilSurface surface && (surface is TinSurface || surface is GridSurface))
                        result.Add((id, surface.Name));
                }

                tr.Commit();
            }
        }
        catch (System.Exception ex)
        {
            Prompts.Say(doc.Editor, $"Không đọc được danh sách mặt phủ ({ex.Message}); cao độ mặt đất chỉ lấy từ hố ga.");
        }

        return result;
    }

    /// <summary>Reads the alignment into a Route and the session; null (with a message) when it can't be read.</summary>
    private static Route Load(Document doc, ObjectId id, CulvertSession session)
    {
        try
        {
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var alignment = (Alignment)tr.GetObject(id, OpenMode.ForRead);
                var route = new Route { Id = id, Handle = alignment.Handle.ToString(), Name = alignment.Name };
                var length = alignment.EndingStation - alignment.StartingStation;
                tr.Commit();
                if (!(length > 0))
                {
                    Prompts.Say(doc.Editor, $"Alignment {route.Name} không có chiều dài.");
                    return null;
                }

                session.SetAlignment($"Alignment {route.Name} ({NumberFormat.Fixed(length, 2)} m)");
                return route;
            }
        }
        catch (System.Exception ex)
        {
            Prompts.Say(doc.Editor, $"Không đọc được alignment: {ex.Message}");
            return null;
        }
    }

    /// <summary>Reads the pipes of the checked networks into the dialog. False when nothing could be read.</summary>
    private static bool Preview(Document doc, Route route, CulvertSession session, List<(ObjectId id, string name)> networks,
        List<(ObjectId id, string name)> surfaces, ProjectPreset preset)
    {
        var ed = doc.Editor;
        var selected = new HashSet<string>(session.SelectedNetworks);
        var networkIds = networks.Where(n => selected.Contains(n.name)).Select(n => n.id).ToList();
        var surfaceId = surfaces.FirstOrDefault(s => s.name == session.Surface).id;
        var messages = new List<string>();
        try
        {
            List<CulvertRecord> records;
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var alignment = (Alignment)tr.GetObject(route.Id, OpenMode.ForRead);
                var surface = surfaceId.IsNull ? null : tr.GetObject(surfaceId, OpenMode.ForRead) as CivilSurface;
                records = PipeNetworkReader.Read(tr, alignment, networkIds, surface, preset.Culvert ?? new CulvertOptions(), messages);
                tr.Commit();
            }

            foreach (var m in messages) Prompts.Say(ed, m);
            session.SetRecords(records);
            if (records.Count == 0) Prompts.Say(ed, $"Không có ống nào của mạng đã chọn nằm trong phạm vi tuyến {route.Name}.");
            return true;
        }
        catch (System.Exception ex)
        {
            foreach (var m in messages) Prompts.Say(ed, m);
            Prompts.Say(ed, $"Không đọc được mạng cống: {ex.Message}");
            return false;
        }
    }

    /// <summary>The table in one transaction (one undo), replacing this alignment's previous one; then CSV/Excel. True when done.</summary>
    private static bool Write(Document doc, Route route, CulvertSession session, ProjectPreset preset)
    {
        var ed = doc.Editor;
        var table = session.BuildTable();
        if (session.WriteTable)
        {
            var insert = Prompts.PickPoint(ed, "Điểm chèn bảng thống kê cống: ");
            if (insert == null) return false;
            if (!WriteTable(doc, route, table, insert.Value, preset)) return false;
        }

        TableFiles.Write(ed, table, Tool, session.WriteCsv, session.WriteXlsx, "Bảng cống", "bảng thống kê cống");
        var warnings = session.WarningCount > 0 ? $", {session.WarningCount} cống có cảnh báo" : "";
        Prompts.Say(ed, $"Hoàn thành: bảng thống kê {table.Rows.Count} cống{warnings}.\n");
        return true;
    }

    private static bool WriteTable(Document doc, Route route, TableData table, Point3d insert, ProjectPreset preset)
    {
        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            try
            {
                var d = new TaggedDrawing(tr, doc.Database, Tool, route.Handle);
                foreach (var (id, tag) in TaggedDrawing.FindTagged(tr, doc.Database, Tool, route.Handle).ToList())
                {
                    if (tag.Kind == TableKind) tr.GetObject(id, OpenMode.ForWrite, false, true).Erase();
                }

                d.EnsureLayer(TableLayer, 3);
                var h = preset.CurveBox?.TextHeight > 0 ? preset.CurveBox.TextHeight : 2.5;
                var entity = CurveTableWriter.Create(doc.Database, table, Title + " - " + route.Name, insert, h);
                d.Add(entity, TableLayer, new ToolTag(Tool) { Kind = TableKind });
                entity.GenerateLayout();
            }
            catch (System.Exception ex)
            {
                Prompts.Say(doc.Editor, $"Lỗi khi tạo bảng thống kê cống: {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
                return false;
            }

            tr.Commit();
            return true;
        }
    }
}
