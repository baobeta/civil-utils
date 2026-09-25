using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Curves;
using C3DTools.Civil2021.Drawing;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Curves;
using C3DTools.Core.Presets;
using C3DTools.Core.Stations;
using C3DTools.Core.Tables;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using CivilSurface = Autodesk.Civil.DatabaseServices.Surface;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.StakeTableCommand))]

namespace C3DTools.Civil2021.Commands;

public class StakeTableCommand
{
    private const string Command = "CTTOADO";
    private const string Tool = "TOADO";
    private const string TableLayer = "TOADO_BANG";
    private const string PointLayer = "TOADO_DIEM";
    private const string Title = "BẢNG TOẠ ĐỘ CỌC";
    private const short TableKind = 1;
    private const short CogoKind = 2;

    /// <summary>CTTOADO: stake coordinate table of an alignment (AutoCAD Table, CSV, Excel, COGO points); a rerun replaces the old output.</summary>
    [CommandMethod("C3DTOOLS", "CTTOADO", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void StakeTable()
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

    /// <summary>The picked alignment: its stations, curve stakes and segment boundaries.</summary>
    private sealed class Route
    {
        public ObjectId Id { get; set; }
        public string Handle { get; set; }
        public string Name { get; set; }
        public double Start { get; set; }
        public double End { get; set; }
        public List<StakeStation> CurveStakes { get; } = new List<StakeStation>();
        public List<double> GeometryStations { get; } = new List<double>();
    }

    private static void Run()
    {
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        var messages = new List<string>();
        var preset = PresetLocator.LoadForDrawing(messages);
        foreach (var m in messages) Prompts.Say(ed, m);

        var memory = ToolWindow.Options;
        var session = new StakeTableSession(preset)
        {
            IntervalText = memory.Get(Command, "Interval", "20"),
            IncludeCurveStakes = memory.Get(Command, "Curve", true),
            IncludeGeometryPoints = memory.Get(Command, "Geometry", false),
            WriteTable = memory.Get(Command, "Bang", true),
            WriteCsv = memory.Get(Command, "CSV", false),
            WriteXlsx = memory.Get(Command, "Excel", false),
            WriteCogo = memory.Get(Command, "COGO", false),
        };

        var surfaces = ReadSurfaces(doc);
        session.SetSurfaces(surfaces.Select(s => s.name));
        var lastSurface = memory.Get(Command, "Surface", "");
        var remembered = session.SurfaceNames.ToList().IndexOf(lastSurface);
        if (lastSurface.Length > 0 && remembered > 0) session.SurfaceIndex = remembered;

        Route route = null;
        var first = PickFirst(ed);
        if (!first.IsNull) route = Load(doc, preset, first, session) ?? route;
        if (route != null) Preview(doc, route, session, surfaces);

        while (true)
        {
            var window = new StakeTableWindow(session);
            var action = window.ShowModal();
            memory.Set(Command, "Interval", session.IntervalText);
            memory.Set(Command, "Curve", session.IncludeCurveStakes);
            memory.Set(Command, "Geometry", session.IncludeGeometryPoints);
            memory.Set(Command, "Bang", session.WriteTable);
            memory.Set(Command, "CSV", session.WriteCsv);
            memory.Set(Command, "Excel", session.WriteXlsx);
            memory.Set(Command, "COGO", session.WriteCogo);
            memory.Set(Command, "Surface", session.Surface ?? "");
            ToolWindow.SaveOptions();

            switch (action)
            {
                case DialogAction.Pick:
                    var picked = Prompts.PickEntity<Alignment>(ed, "Chọn alignment: ");
                    if (picked.IsNull) continue;
                    var loaded = Load(doc, preset, picked, session);
                    if (loaded == null) continue;
                    route = loaded;
                    Preview(doc, route, session, surfaces);
                    continue;
                case DialogAction.Preview:
                    if (route != null && session.CanApply) Preview(doc, route, session, surfaces);
                    continue;
                case DialogAction.Apply:
                    if (route == null || !session.CanApply) continue;
                    var located = Preview(doc, route, session, surfaces);
                    if (located == null) continue;
                    if (Write(doc, route, session, located, preset)) return;
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
            Prompts.Say(doc.Editor, $"Không đọc được danh sách mặt phủ ({ex.Message}); bảng không có cột Z.");
        }

        return result;
    }

    /// <summary>Reads the alignment into a Route and the session; null (with a message) when it can't be read.</summary>
    private static Route Load(Document doc, ProjectPreset preset, ObjectId id, StakeTableSession session)
    {
        var ed = doc.Editor;
        try
        {
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var alignment = (Alignment)tr.GetObject(id, OpenMode.ForRead);
                var route = new Route
                {
                    Id = id,
                    Handle = alignment.Handle.ToString(),
                    Name = alignment.Name,
                    Start = alignment.StartingStation,
                    End = alignment.EndingStation,
                };
                if (!(route.End > route.Start))
                {
                    Prompts.Say(ed, $"Alignment {route.Name} không có chiều dài.");
                    return null;
                }

                ReadCurveStakes(doc, preset, alignment, route);
                foreach (var segment in AlignmentGeometryReader.Read(alignment, null))
                {
                    route.GeometryStations.Add(segment.StartStation);
                    route.GeometryStations.Add(segment.StartStation + segment.Length);
                }

                tr.Commit();
                session.SetSource($"Alignment {route.Name} ({NumberFormat.Fixed(route.End - route.Start, 2)} m)", route.Start, route.End);
                return route;
            }
        }
        catch (System.Exception ex)
        {
            Prompts.Say(ed, $"Không đọc được alignment: {ex.Message}");
            return null;
        }
    }

    /// <summary>NĐ/TĐ/P/TC/NC of the alignment's own curves (as CTYTC "Chỉ cắm cọc" reads them). A failure leaves only interval stakes.</summary>
    private static void ReadCurveStakes(Document doc, ProjectPreset preset, Alignment alignment, Route route)
    {
        try
        {
            var source = new AlignmentSource(doc, route.Id, route.Handle, route.Name);
            var curves = new CurveDesignSession(preset);
            source.LoadInto(curves);
            if (curves.Design == null) return;
            var asBuilt = AsBuiltDesign.Read(alignment, source, curves.Design, new List<BoxSite>(), doc.Editor);
            foreach (var stake in RouteStakes.FromStations(asBuilt, route.Start, route.End))
            {
                if (stake.Kind != StakeKind.Start && stake.Kind != StakeKind.End)
                    route.CurveStakes.Add(new StakeStation(stake.Station, stake.Name, StakeOrigin.Curve));
            }
        }
        catch (System.Exception ex)
        {
            Prompts.Say(doc.Editor, $"Không đọc được đường cong của tuyến ({ex.Message}); chỉ có cọc chi tiết.");
        }
    }

    /// <summary>Locates the stations of the current options and puts the table in the dialog. Null when nothing could be located.</summary>
    private static List<StakePoint> Preview(Document doc, Route route, StakeTableSession session, List<(ObjectId id, string name)> surfaces)
    {
        var ed = doc.Editor;
        List<StakeStation> stations;
        try
        {
            stations = session.Stations(route.CurveStakes, route.GeometryStations);
        }
        catch (ArgumentException ex)
        {
            Prompts.Say(ed, ex.Message);
            return null;
        }

        var surfaceId = surfaces.FirstOrDefault(s => s.name == session.Surface).id;
        var points = new List<StakePoint>();
        var failed = 0;
        using (var tr = doc.TransactionManager.StartTransaction())
        {
            var alignment = (Alignment)tr.GetObject(route.Id, OpenMode.ForRead);
            var surface = surfaceId.IsNull ? null : tr.GetObject(surfaceId, OpenMode.ForRead) as CivilSurface;
            foreach (var s in stations)
            {
                double x = 0, y = 0;
                try
                {
                    alignment.PointLocation(s.Station, 0, ref x, ref y);
                }
                catch (System.Exception ex)
                {
                    if (failed++ < 5) Prompts.Say(ed, $"Cọc {s.Name} tại {NumberFormat.Fixed(s.Station, 2)}: không lấy được điểm trên alignment ({ex.Message}); bỏ qua.");
                    continue;
                }

                points.Add(new StakePoint(s.Name, s.Station, x, y, surface == null ? (double?)null : Elevation(surface, x, y)));
            }

            tr.Commit();
        }

        session.SetPreview(points);
        return points.Count > 0 ? points : null;
    }

    /// <summary>Z of the surface at (x, y); null outside the surface or when Civil 3D can't tell.</summary>
    private static double? Elevation(CivilSurface surface, double x, double y)
    {
        try
        {
            var z = surface.FindElevationAtXY(x, y);
            return double.IsNaN(z) || double.IsInfinity(z) ? (double?)null : z;
        }
        catch (System.Exception)
        {
            return null;   // outside the surface boundary or in a hole
        }
    }

    /// <summary>Table and COGO points in one transaction (one undo), replacing this alignment's previous ones; then CSV/Excel. True when done.</summary>
    private static bool Write(Document doc, Route route, StakeTableSession session, List<StakePoint> points, ProjectPreset preset)
    {
        var ed = doc.Editor;
        var table = session.Table;
        Point3d? insert = null;
        if (session.WriteTable)
        {
            insert = Prompts.PickPoint(ed, "Điểm chèn bảng toạ độ: ");
            if (insert == null) return false;
        }

        var cogoCount = 0;
        var unnamed = 0;
        if (session.WriteTable || session.WriteCogo)
        {
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    var d = new TaggedDrawing(tr, doc.Database, Tool, route.Handle);
                    var civil = session.WriteCogo ? CivilDocument.GetCivilDocument(doc.Database) : null;
                    EraseOld(d, civil, session);

                    if (session.WriteTable)
                    {
                        d.EnsureLayer(TableLayer, 3);
                        var h = preset.CurveBox?.TextHeight > 0 ? preset.CurveBox.TextHeight : 2.5;
                        var entity = CurveTableWriter.Create(doc.Database, table, Title + " - " + route.Name, insert.Value, h);
                        d.Add(entity, TableLayer, new ToolTag(Tool) { Kind = TableKind });
                        entity.GenerateLayout();
                    }

                    if (session.WriteCogo)
                    {
                        d.EnsureLayer(PointLayer, 1);
                        var layerId = d.LayerId(PointLayer);
                        for (var i = 0; i < points.Count; i++)
                        {
                            var p = points[i];
                            var pointId = civil.CogoPoints.Add(new Point3d(p.X, p.Y, p.Z ?? 0), p.Name, true);
                            var point = (CogoPoint)tr.GetObject(pointId, OpenMode.ForWrite);
                            try
                            {
                                point.PointName = p.Name;
                            }
                            catch (System.Exception)
                            {
                                unnamed++;   // name already used by another COGO point
                            }

                            point.LayerId = layerId;
                            d.Tag(point, new ToolTag(Tool) { Kind = CogoKind, Number = i + 1 });
                            cogoCount++;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Prompts.Say(ed, $"Lỗi khi tạo bảng toạ độ: {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
                    return false;
                }

                tr.Commit();
            }
        }

        if (session.WriteCsv) WriteFile(ed, table, "csv", "CSV", (t, path) => TableExport.WriteCsv(t, path));
        if (session.WriteXlsx) WriteFile(ed, table, "xlsx", "Excel", (t, path) => TableExport.WriteXlsx(t, path, "Toạ độ cọc"));
        if (unnamed > 0) Prompts.Say(ed, $"{unnamed} điểm COGO không đặt được tên (trùng tên điểm có sẵn); tên cọc nằm trong mô tả.");
        var cogoText = session.WriteCogo ? $", {cogoCount} điểm COGO" : "";
        Prompts.Say(ed, $"Hoàn thành: bảng toạ độ {table.Rows.Count} cọc{cogoText}.\n");
        return true;
    }

    /// <summary>Erases what the previous run for this alignment made of the outputs being written (COGO points through the point collection).</summary>
    private static void EraseOld(TaggedDrawing d, CivilDocument civil, StakeTableSession session)
    {
        var old = TaggedDrawing.FindTagged(d.Transaction, d.Database, Tool, d.TagHandle).ToList();
        foreach (var (id, tag) in old)
        {
            if (tag.Kind == TableKind && session.WriteTable)
            {
                d.Transaction.GetObject(id, OpenMode.ForWrite).Erase();
            }
            else if (tag.Kind == CogoKind && session.WriteCogo)
            {
                try
                {
                    civil.CogoPoints.Remove(id);
                }
                catch (System.Exception)
                {
                    d.Transaction.GetObject(id, OpenMode.ForWrite).Erase();
                }
            }
        }
    }

    /// <summary>&lt;DWGPREFIX&gt;&lt;drawing name&gt;_TOADO.&lt;ext&gt;; skipped with a message when the drawing was never saved.</summary>
    private static void WriteFile(Editor ed, TableData table, string ext, string kind, Action<TableData, string> write)
    {
        var folder = PresetLocator.DrawingFolder();
        if (folder == null)
        {
            Prompts.Say(ed, $"Bản vẽ chưa được lưu: bỏ qua xuất {kind}.");
            return;
        }

        try
        {
            var name = Convert.ToString(AcCoreApp.GetSystemVariable("DWGNAME"));
            var path = TableExport.SuggestPath(Path.Combine(folder, name), "TOADO", ext);
            write(table, path);
            Prompts.Say(ed, $"Đã xuất bảng toạ độ: {path}");
        }
        catch (System.Exception ex)
        {
            Prompts.Say(ed, $"Không ghi được {kind}: {ex.Message}");
        }
    }
}
