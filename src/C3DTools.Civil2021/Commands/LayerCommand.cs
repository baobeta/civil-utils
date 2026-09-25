using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using C3DTools.Civil2021.Curves;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Drawing;
using C3DTools.Core.Presets;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcEntity = Autodesk.AutoCAD.DatabaseServices.Entity;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.LayerCommand))]

namespace C3DTools.Civil2021.Commands;

public class LayerCommand
{
    private const string Command = "CTLAYER";

    /// <summary>CTLAYER: moves objects to the preset's standard layers (LayerMap), creates them, optionally purges empty layers; one undo.</summary>
    [CommandMethod("C3DTOOLS", "CTLAYER", CommandFlags.Modal)]
    public void StandardiseLayers()
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
        if (preset.LayerMap == null || preset.LayerMap.Count == 0)
            Prompts.Say(ed, "Preset chưa có LayerMap: nhập layer đích trong bảng rồi bấm \"Lưu vào preset\".");

        var memory = ToolWindow.Options;
        var session = new LayerMapSession(preset.LayerMap)
        {
            ApplyToBlocks = memory.Get(Command, "Blocks", false),
            PurgeEmpty = memory.Get(Command, "Purge", false),
        };
        foreach (var w in session.Warnings) Prompts.Say(ed, w);
        session.Load(Scan(doc.Database));
        var presetName = string.IsNullOrWhiteSpace(preset.Name) ? "mặc định" : preset.Name;

        while (true)
        {
            var window = new LayerWindow(session, $"Toàn bản vẽ: {session.Rows.Count} layer · preset: {presetName}", () => SavePreset(preset, session));
            var action = window.ShowModal();
            memory.Set(Command, "Blocks", session.ApplyToBlocks);
            memory.Set(Command, "Purge", session.PurgeEmpty);
            ToolWindow.SaveOptions();

            switch (action)
            {
                case DialogAction.Pick:
                    FindLayer(doc, session);
                    continue;
                case DialogAction.Preview:
                case DialogAction.Apply:
                    if (!session.CanApply) continue;
                    if (Apply(doc, session, askToKeep: action == DialogAction.Preview)) return;
                    continue;   // Khong: back to the dialog, nothing changed
                default:
                    return;
            }
        }
    }

    /// <summary>"Chọn trên bản vẽ…": selects the row of the picked object's layer.</summary>
    private static void FindLayer(Document doc, LayerMapSession session)
    {
        var id = Prompts.PickEntity<AcEntity>(doc.Editor, "Chọn đối tượng để tìm layer của nó: ");
        if (id.IsNull) return;
        string layer;
        using (var tr = doc.TransactionManager.StartTransaction())
        {
            layer = ((AcEntity)tr.GetObject(id, OpenMode.ForRead)).Layer;
            tr.Commit();
        }

        if (!session.Select(layer)) Prompts.Say(doc.Editor, $"Layer {layer} không có trong bảng (layer của xref).");
    }

    /// <summary>Every layer of the drawing (not xref layers) with its object counts in the layouts and in block definitions.</summary>
    private static List<LayerUsage> Scan(Database db)
    {
        var inLayouts = new Dictionary<ObjectId, int>();
        var inBlocks = new Dictionary<ObjectId, int>();
        var result = new List<LayerUsage>();
        using (var tr = db.TransactionManager.StartTransaction())
        {
            foreach (var btr in Containers(tr, db, includeBlocks: true))
            {
                var counts = btr.IsLayout ? inLayouts : inBlocks;
                ForEachEntity(tr, btr, e =>
                {
                    if (KeepsInsertLayer(btr, e, db)) return;
                    counts[e.LayerId] = (counts.TryGetValue(e.LayerId, out var n) ? n : 0) + 1;
                });
            }

            var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            foreach (ObjectId id in layers)
            {
                var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (layer.IsDependent) continue;
                result.Add(new LayerUsage(layer.Name,
                    inLayouts.TryGetValue(id, out var a) ? a : 0,
                    inBlocks.TryGetValue(id, out var b) ? b : 0));
            }

            tr.Commit();
        }

        return result.OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Model space and paper space layouts; with includeBlocks also block definitions: not xrefs or dependent blocks, and of
    /// the anonymous ones only the *U representations of dynamic blocks (never *D dimension, *X hatch or other anonymous blocks).
    /// </summary>
    private static IEnumerable<BlockTableRecord> Containers(Transaction tr, Database db, bool includeBlocks)
    {
        var blocks = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var dynamicRepresentations = new HashSet<ObjectId>();
        if (includeBlocks)
        {
            foreach (ObjectId id in blocks)
            {
                var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (btr.IsLayout || btr.IsAnonymous || btr.IsFromExternalReference || !btr.IsDynamicBlock) continue;
                foreach (ObjectId representation in btr.GetAnonymousBlockIds()) dynamicRepresentations.Add(representation);
            }
        }

        foreach (ObjectId id in blocks)
        {
            var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
            if (btr.IsLayout) yield return btr;
            else if (includeBlocks && !btr.IsFromExternalReference && !btr.IsDependent
                     && (!btr.IsAnonymous || (dynamicRepresentations.Contains(id) && btr.Name.StartsWith("*U", StringComparison.OrdinalIgnoreCase))))
                yield return btr;
        }
    }

    /// <summary>Layer-0 objects inside a block definition take the layer of each insert: they are never counted or moved.</summary>
    private static bool KeepsInsertLayer(BlockTableRecord owner, AcEntity entity, Database db) =>
        !owner.IsLayout && entity.LayerId == db.LayerZero;

    /// <summary>Each entity of the block and the attributes of its block references, opened for read.</summary>
    private static void ForEachEntity(Transaction tr, BlockTableRecord btr, Action<AcEntity> visit)
    {
        foreach (ObjectId id in btr)
        {
            if (id.IsErased || !(tr.GetObject(id, OpenMode.ForRead) is AcEntity entity)) continue;
            visit(entity);
            if (!(entity is BlockReference reference)) continue;
            foreach (ObjectId attributeId in reference.AttributeCollection)
            {
                if (!attributeId.IsErased && tr.GetObject(attributeId, OpenMode.ForRead) is AcEntity attribute) visit(attribute);
            }
        }
    }

    /// <returns>True when the result was kept.</returns>
    private static bool Apply(Document doc, LayerMapSession session, bool askToKeep)
    {
        var ed = doc.Editor;
        var db = doc.Database;
        var moves = session.Moves();
        int moved = 0, created = 0, purged = 0, keptOnZero = 0;
        var notes = new List<string>();
        bool keep, failed = false;

        using (doc.LockDocument())
        using (var tr = db.TransactionManager.StartTransaction())
        {
            try
            {
                var targets = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
                foreach (var move in moves)
                {
                    if (targets.ContainsKey(move.To)) continue;
                    targets[move.To] = EnsureLayer(tr, db, move, notes, ref created);
                }

                var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                var map = new Dictionary<ObjectId, ObjectId>();
                foreach (var move in moves)
                {
                    if (layers.Has(move.From)) map[layers[move.From]] = targets[move.To];
                }

                if (map.Count > 0)
                {
                    foreach (var btr in Containers(tr, db, session.ApplyToBlocks))
                    {
                        ForEachEntity(tr, btr, e =>
                        {
                            if (!map.TryGetValue(e.LayerId, out var target)) return;
                            if (KeepsInsertLayer(btr, e, db))
                            {
                                keptOnZero++;
                                return;
                            }

                            // Objects on locked layers move too.
                            var entity = (AcEntity)tr.GetObject(e.ObjectId, OpenMode.ForWrite, false, true);
                            entity.LayerId = target;
                            moved++;
                        });
                    }
                }

                if (keptOnZero > 0) notes.Add($"{keptOnZero} đối tượng layer 0 trong định nghĩa block giữ nguyên layer 0 (theo layer của block chèn).");
                if (session.PurgeEmpty) purged = PurgeEmptyLayers(tr, db, new HashSet<ObjectId>(targets.Values), notes);

                tr.TransactionManager.QueueForGraphicsFlush();
                ed.UpdateScreen();
                foreach (var n in notes.Take(10)) Prompts.Say(ed, n);
                keep = !askToKeep || Prompts.AskKeep(ed);
            }
            catch (System.Exception ex)
            {
                Prompts.Say(ed, $"Lỗi khi chuẩn hoá layer: {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
                keep = false;
                failed = true;
            }

            if (keep) tr.Commit();
            else tr.Abort();
        }

        if (!keep || failed)
        {
            ed.Regen();
            return false;
        }

        ed.Regen();
        var purgeText = session.PurgeEmpty ? $", xoá {purged} layer rỗng" : "";
        Prompts.Say(ed, $"Đã chuyển {moved} đối tượng từ {moves.Count} layer, tạo {created} layer mới{purgeText}.\n");
        return true;
    }

    /// <summary>The target layer's id; created with the move's ACI colour and linetype (Continuous when not loaded) if missing.</summary>
    private static ObjectId EnsureLayer(Transaction tr, Database db, LayerMove move, List<string> notes, ref int created)
    {
        var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (layers.Has(move.To)) return layers[move.To];

        SymbolUtilityServices.ValidateSymbolName(move.To, false);
        var linetypes = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
        var linetype = db.ContinuousLinetype;
        if (!string.IsNullOrWhiteSpace(move.Linetype) && !SymbolUtilityServices.IsLinetypeContinuousName(move.Linetype))
        {
            if (linetypes.Has(move.Linetype)) linetype = linetypes[move.Linetype];
            else notes.Add($"Linetype {move.Linetype} chưa nạp trong bản vẽ: layer {move.To} dùng Continuous.");
        }

        var record = new LayerTableRecord
        {
            Name = move.To,
            Color = Color.FromColorIndex(ColorMethod.ByAci, move.Color),
            LinetypeObjectId = linetype,
        };
        layers.UpgradeOpen();
        var id = layers.Add(record);
        tr.AddNewlyCreatedDBObject(record, true);
        created++;
        return id;
    }

    /// <summary>Erases layers nothing references any more (Database.Purge), except 0, Defpoints, the current layer, xref layers and the targets.</summary>
    private static int PurgeEmptyLayers(Transaction tr, Database db, ISet<ObjectId> keep, List<string> notes)
    {
        var candidates = new ObjectIdCollection();
        var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        foreach (ObjectId id in layers)
        {
            if (id.IsErased || id == db.LayerZero || id == db.Clayer || keep.Contains(id)) continue;
            var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
            if (layer.IsDependent || SymbolUtilityServices.IsLayerDefpointsName(layer.Name)) continue;
            candidates.Add(id);
        }

        try
        {
            db.Purge(candidates);   // leaves only the ids nothing hard-references
        }
        catch (System.Exception ex)
        {
            notes.Add($"Không xoá được layer rỗng: {ex.Message}.");
            return 0;
        }

        var count = 0;
        foreach (ObjectId id in candidates)
        {
            try
            {
                tr.GetObject(id, OpenMode.ForWrite).Erase();
                count++;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                notes.Add($"Không xoá được layer {id.Handle}: {ex.Message}.");
            }
        }

        return count;
    }

    /// <summary>Writes a temporary file next to path, then swaps it in, so a failed write never leaves half a preset.</summary>
    private static void WriteAtomically(string path, string text)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, text, new UTF8Encoding(false));
        try
        {
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    /// <summary>
    /// "Lưu vào preset": the merged LayerMap goes into the drawing folder's preset (the single *.c3dtools.json there, else
    /// &lt;drawing name&gt;.c3dtools.json created from the preset in use). Returns the message for the dialog.
    /// </summary>
    private static string SavePreset(ProjectPreset current, LayerMapSession session)
    {
        var folder = PresetLocator.DrawingFolder();
        if (folder == null) return "Bản vẽ chưa được lưu: chưa có thư mục để ghi preset.";

        try
        {
            var found = Directory.GetFiles(folder, "*.c3dtools.json");
            var drawingName = Path.GetFileNameWithoutExtension(Convert.ToString(AcCoreApp.GetSystemVariable("DWGNAME")));
            var path = found.Length == 1 ? found[0] : Path.Combine(folder, drawingName + ".c3dtools.json");

            var rules = session.MergedRules();
            string json;
            if (File.Exists(path))
            {
                try
                {
                    // Only the LayerMap node changes: keys this version does not know survive.
                    json = PresetSerializer.ReplaceSection(File.ReadAllText(path), nameof(ProjectPreset.LayerMap), rules);
                }
                catch (System.Exception ex)
                {
                    return $"Không đọc được {Path.GetFileName(path)} ({ex.Message}); không ghi đè.";
                }
            }
            else
            {
                current.LayerMap = rules;
                json = PresetSerializer.Save(current);
            }

            WriteAtomically(path, json);
            session.UseRules(rules);
            var warning = found.Length > 1 ? " Thư mục có nhiều file *.c3dtools.json: C3DTools chỉ tự dùng khi có đúng một file." : "";
            return $"Đã lưu {rules.Count} quy tắc vào {Path.GetFileName(path)}.{warning}";
        }
        catch (System.Exception ex)
        {
            return $"Không ghi được preset: {ex.Message}";
        }
    }
}
