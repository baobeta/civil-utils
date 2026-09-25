using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using C3DTools.Civil2021.Curves;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Text;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcEntity = Autodesk.AutoCAD.DatabaseServices.Entity;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.FontCommand))]

namespace C3DTools.Civil2021.Commands;

public class FontCommand
{
    private const string Command = "CTFONT";

    /// <summary>CTFONT: converts Vietnamese text between TCVN3, VNI and Unicode for the selection or the whole drawing, in one undo.</summary>
    [CommandMethod("C3DTOOLS", "CTFONT", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void ConvertFont()
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
        var session = new FontConversionSession(preset.FontConversion)
        {
            TargetIndex = memory.Get(Command, "Target", 0),
            ReplaceStyleFont = memory.Get(Command, "StyleFont", true),
            ConvertBlockDefinitions = memory.Get(Command, "Blocks", false),
        };

        var selection = new ObjectId[0];
        var implied = ed.SelectImplied();
        if (implied.Status == PromptStatus.OK && implied.Value != null)
        {
            selection = implied.Value.GetObjectIds();
            ed.SetImpliedSelection(new ObjectId[0]);
        }

        session.WholeDrawing = selection.Length == 0;

        while (true)
        {
            session.Load(Scan(doc, session, selection, null).Items);
            var window = new FontWindow(session, session.WholeDrawing ? "Toàn bản vẽ" : $"{selection.Length} đối tượng đã chọn");
            var action = window.ShowModal();
            memory.Set(Command, "Target", session.TargetIndex);
            memory.Set(Command, "StyleFont", session.ReplaceStyleFont);
            memory.Set(Command, "Blocks", session.ConvertBlockDefinitions);
            ToolWindow.SaveOptions();

            switch (action)
            {
                case DialogAction.Pick:
                    var picked = PickTexts(ed);
                    if (picked != null)
                    {
                        selection = picked;
                        session.WholeDrawing = false;
                    }

                    continue;
                case DialogAction.Preview:
                    continue;   // rescanned with the current options at the top of the loop
                case DialogAction.Apply:
                    Apply(doc, session, selection);
                    return;
                default:
                    return;
            }
        }
    }

    private static ObjectId[] PickTexts(Editor ed)
    {
        var filter = new SelectionFilter(new[]
        {
            new TypedValue((int)DxfCode.Start, "TEXT,MTEXT,INSERT,ATTDEF,DIMENSION,MULTILEADER,ACAD_TABLE"),
        });
        var options = new PromptSelectionOptions { MessageForAdding = "\nChọn chữ, block, kích thước, MLeader, bảng: " };
        var result = ed.GetSelection(options, filter);
        return result.Status == PromptStatus.OK ? result.Value.GetObjectIds() : null;
    }

    /// <summary>Rescans (so the tie-breaking encoding matches), then converts everything in one transaction: one undo.</summary>
    private static void Apply(Document doc, FontConversionSession session, ObjectId[] selection)
    {
        var ed = doc.Editor;
        var read = Scan(doc, session, selection, null);
        session.Load(read.Items);
        if (session.ChangeCount == 0)
        {
            Prompts.Say(ed, read.FieldObjects.Count > 0 ? $"Không có chữ nào cần chuyển. Bỏ qua {read.FieldObjects.Count} đối tượng có field." : "Không có chữ nào cần chuyển.");
            return;
        }

        Walker walker;
        StylePlan plan = null;
        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            try
            {
                plan = session.ReplaceStyleFont && session.CanReplaceStyleFont ? StylePlan.Make(tr, doc.Database, session, read.Uses) : new StylePlan();
                walker = Scan(doc, session, selection, plan.Map, tr);
            }
            catch (System.Exception ex)
            {
                Prompts.Say(ed, $"Lỗi khi chuyển font: {ex.Message}. Đã hủy, bản vẽ không thay đổi.");
                return;
            }

            tr.Commit();
        }

        ed.Regen();
        foreach (var m in walker.Messages.Take(10)) Prompts.Say(ed, m);
        var text = $"Đã chuyển {walker.Changed} đối tượng";
        if (plan.Changed > 0) text += $", đổi font {plan.Changed} kiểu chữ sang {session.TargetFont}";
        if (plan.Created > 0) text += $", dùng {plan.Created} kiểu chữ *_UNI (font {session.TargetFont})";
        Prompts.Say(ed, text + ".");
        if (walker.FieldObjects.Count > 0) Prompts.Say(ed, $"Bỏ qua {walker.FieldObjects.Count} đối tượng có field.");
        Prompts.Say(ed, "");
    }

    /// <summary>Reads (styleMap null) or converts (styleMap set) every text of the scope in tr, or in its own read transaction when tr is null.</summary>
    private static Walker Scan(Document doc, FontConversionSession session, ObjectId[] selection, Dictionary<ObjectId, ObjectId> styleMap, Transaction tr = null)
    {
        if (tr == null)
        {
            using (var readTr = doc.Database.TransactionManager.StartTransaction())
            {
                var result = Scan(doc, session, selection, styleMap, readTr);
                readTr.Commit();
                return result;
            }
        }

        var walker = new Walker(tr, session, styleMap);
        if (session.WholeDrawing)
        {
            var blocks = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId id in blocks)
            {
                var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (btr.IsLayout) walker.Block(btr, force: true);
                else if (session.ConvertBlockDefinitions && !btr.IsAnonymous) walker.Block(btr, force: false);
            }
        }
        else
        {
            foreach (var id in selection) walker.Entity(id);
        }

        return walker;
    }

    /// <summary>
    /// Which text style each converted text ends up with. A style is changed in place only when every text using it was seen
    /// (whole drawing including block definitions) and none of them keeps legacy text; otherwise the converted texts move to
    /// "&lt;style&gt;_UNI" with the Unicode font, so unconverted texts of the old style keep rendering as before.
    /// </summary>
    private sealed class StylePlan
    {
        public Dictionary<ObjectId, ObjectId> Map { get; } = new Dictionary<ObjectId, ObjectId>();
        public int Changed { get; private set; }
        public int Created { get; private set; }

        public static StylePlan Make(Transaction tr, Database db, FontConversionSession session, IEnumerable<(FontTextItem item, ObjectId style, bool skipped)> uses)
        {
            var plan = new StylePlan();
            var sawEverything = session.WholeDrawing && session.ConvertBlockDefinitions;
            foreach (var group in uses.Where(u => !u.style.IsNull && !u.style.IsErased).GroupBy(u => u.style))
            {
                if (!group.Any(u => !u.skipped && session.Convert(u.item) != null)) continue;
                var style = (TextStyleTableRecord)tr.GetObject(group.Key, OpenMode.ForRead);
                if (string.Equals(style.Font.TypeFace, session.TargetFont, StringComparison.OrdinalIgnoreCase))
                {
                    plan.Map[group.Key] = group.Key;
                    continue;
                }

                var keepsLegacy = group.Any(u => (u.skipped || session.Convert(u.item) == null) && u.item.Text.Any(c => c >= 0x80));
                if (sawEverything && !keepsLegacy)
                {
                    style.UpgradeOpen();
                    SetFont(style, style, session.TargetFont);
                    plan.Map[group.Key] = group.Key;
                    plan.Changed++;
                }
                else
                {
                    plan.Map[group.Key] = plan.Unicode(tr, db, style, session.TargetFont);
                }
            }

            return plan;
        }

        /// <summary>"&lt;name&gt;_UNI": reused when the drawing has it, else a copy of style with the Unicode font.</summary>
        private ObjectId Unicode(Transaction tr, Database db, TextStyleTableRecord style, string font)
        {
            var name = style.Name + "_UNI";
            var table = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (table.Has(name)) return table[name];
            var copy = new TextStyleTableRecord
            {
                Name = name,
                TextSize = style.TextSize,
                XScale = style.XScale,
                ObliquingAngle = style.ObliquingAngle,
            };
            SetFont(copy, style, font);
            table.UpgradeOpen();
            var id = table.Add(copy);
            tr.AddNewlyCreatedDBObject(copy, true);
            Created++;
            return id;
        }

        private static void SetFont(TextStyleTableRecord target, TextStyleTableRecord source, string font)
        {
            var current = source.Font;
            // An SHX style has no typeface: clear the file so the TrueType font is used.
            if (target.FileName != null && target.FileName.EndsWith(".shx", StringComparison.OrdinalIgnoreCase)) target.FileName = "";
            target.Font = new FontDescriptor(font, current.Bold, current.Italic, 0, current.PitchAndFamily);
        }
    }

    /// <summary>
    /// Visits DBText, MText, attributes, attribute definitions, dimension overrides, MLeader MText and table cells.
    /// Objects (and table cells) with fields are skipped: writing their text would turn the field into static text.
    /// </summary>
    private sealed class Walker
    {
        private readonly Transaction _tr;
        private readonly FontConversionSession _session;
        private readonly Dictionary<ObjectId, ObjectId> _styleMap;
        private readonly HashSet<ObjectId> _visitedBlocks = new HashSet<ObjectId>();
        private readonly HashSet<ObjectId> _visitedEntities = new HashSet<ObjectId>();
        private readonly Dictionary<ObjectId, (bool upper, string font)> _styleFonts = new Dictionary<ObjectId, (bool, string)>();

        /// <param name="styleMap">Null: only read. Otherwise convert, moving converted texts to the mapped style.</param>
        public Walker(Transaction tr, FontConversionSession session, Dictionary<ObjectId, ObjectId> styleMap)
        {
            _tr = tr;
            _session = session;
            _styleMap = styleMap;
        }

        private bool Writing => _styleMap != null;

        public List<FontTextItem> Items { get; } = new List<FontTextItem>();

        /// <summary>Every text with its style; skipped: in an object with fields, so it keeps its text.</summary>
        public List<(FontTextItem item, ObjectId style, bool skipped)> Uses { get; } = new List<(FontTextItem, ObjectId, bool)>();

        public HashSet<ObjectId> FieldObjects { get; } = new HashSet<ObjectId>();
        public List<string> Messages { get; } = new List<string>();
        public int Changed { get; private set; }

        /// <summary>force: a layout (always visited); otherwise a definition, visited only when block definitions are included.</summary>
        public void Block(BlockTableRecord btr, bool force)
        {
            if (!_visitedBlocks.Add(btr.ObjectId)) return;
            if (!force && (btr.IsFromExternalReference || btr.IsDependent || btr.IsLayout)) return;
            foreach (ObjectId id in btr) Entity(id);
        }

        public void Entity(ObjectId id)
        {
            if (id.IsNull || id.IsErased || !_visitedEntities.Add(id)) return;
            var entity = _tr.GetObject(id, OpenMode.ForRead) as AcEntity;
            try
            {
                switch (entity)
                {
                    case AttributeReference attribute:
                        Attribute(attribute);
                        break;
                    case AttributeDefinition definition:
                        AttributeDefinition(definition);
                        break;
                    case DBText text:
                    {
                        var converted = Text("DBText", text.TextString, false, text.TextStyleId, text.HasFields, id);
                        if (converted != null)
                            Change(text, () =>
                            {
                                text.TextString = converted;
                                Restyle(text.TextStyleId, s => text.TextStyleId = s);
                            });
                        break;
                    }
                    case MText mtext:
                    {
                        var converted = Text("MText", mtext.Contents, true, mtext.TextStyleId, mtext.HasFields, id);
                        if (converted != null)
                            Change(mtext, () =>
                            {
                                mtext.Contents = converted;
                                Restyle(mtext.TextStyleId, s => mtext.TextStyleId = s);
                            });
                        break;
                    }
                    case Dimension dimension:
                    {
                        var dimText = dimension.DimensionText;
                        if (string.IsNullOrEmpty(dimText)) break;
                        var converted = Text("Dimension", dimText, true, dimension.TextStyleId, dimension.HasFields, id);
                        if (converted != null)
                            Change(dimension, () =>
                            {
                                dimension.DimensionText = converted;
                                Restyle(dimension.TextStyleId, s => dimension.TextStyleId = s);
                            });
                        break;
                    }
                    case MLeader leader:
                        if (leader.ContentType != ContentType.MTextContent) break;
                        using (var leaderText = leader.MText)
                        {
                            if (leaderText == null) break;
                            var converted = Text("MLeader", leaderText.Contents, true, leader.TextStyleId, leader.HasFields, id);
                            if (converted != null)
                                Change(leader, () =>
                                {
                                    leaderText.Contents = converted;
                                    Restyle(leader.TextStyleId, s =>
                                    {
                                        leaderText.TextStyleId = s;
                                        leader.TextStyleId = s;
                                    });
                                    leader.MText = leaderText;
                                });
                        }

                        break;
                    case Table table:
                        TableCells(table);
                        break;
                    case BlockReference reference:
                        foreach (ObjectId attributeId in reference.AttributeCollection) Entity(attributeId);
                        if (_session.ConvertBlockDefinitions)
                        {
                            Definition(reference.BlockTableRecord);
                            Definition(reference.DynamicBlockTableRecord);
                        }

                        break;
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                Messages.Add($"Bỏ qua {entity?.GetType().Name} {id.Handle}: {ex.Message}");
            }
        }

        private void Definition(ObjectId id)
        {
            if (id.IsNull || id.IsErased) return;
            Block((BlockTableRecord)_tr.GetObject(id, OpenMode.ForRead), force: false);
        }

        private void Attribute(AttributeReference attribute)
        {
            var id = attribute.ObjectId;
            if (attribute.IsMTextAttribute)
            {
                using (var mtext = attribute.MTextAttribute)
                {
                    if (mtext == null) return;
                    var converted = Text("AttributeReference", mtext.Contents, true, attribute.TextStyleId, attribute.HasFields, id);
                    if (converted != null)
                        Change(attribute, () =>
                        {
                            mtext.Contents = converted;
                            Restyle(attribute.TextStyleId, s =>
                            {
                                mtext.TextStyleId = s;
                                attribute.TextStyleId = s;
                            });
                            attribute.MTextAttribute = mtext;
                        });
                }
            }
            else
            {
                var converted = Text("AttributeReference", attribute.TextString, false, attribute.TextStyleId, attribute.HasFields, id);
                if (converted != null)
                    Change(attribute, () =>
                    {
                        attribute.TextString = converted;
                        Restyle(attribute.TextStyleId, s => attribute.TextStyleId = s);
                    });
            }
        }

        private void AttributeDefinition(AttributeDefinition definition)
        {
            var id = definition.ObjectId;
            var styleId = definition.TextStyleId;
            var fields = definition.HasFields;
            var changes = new List<System.Action>();
            MText mtext = null;
            try
            {
                if (definition.IsMTextAttributeDefinition)
                {
                    mtext = definition.MTextAttributeDefinition;
                    var converted = mtext == null ? null : Text("AttributeDefinition", mtext.Contents, true, styleId, fields, id);
                    if (converted != null)
                        changes.Add(() =>
                        {
                            mtext.Contents = converted;
                            Restyle(styleId, s => mtext.TextStyleId = s);
                            definition.MTextAttributeDefinition = mtext;
                        });
                }
                else
                {
                    var converted = Text("AttributeDefinition", definition.TextString, false, styleId, fields, id);
                    if (converted != null) changes.Add(() => definition.TextString = converted);
                }

                var prompt = Text("AttributeDefinition", definition.Prompt, false, styleId, fields, id);
                if (prompt != null) changes.Add(() => definition.Prompt = prompt);
                if (changes.Count > 0)
                    Change(definition, () =>
                    {
                        changes.ForEach(c => c());
                        Restyle(styleId, s => definition.TextStyleId = s);
                    });
            }
            finally
            {
                mtext?.Dispose();
            }
        }

        private void TableCells(Table table)
        {
            var changes = new List<System.Action>();
            for (var r = 0; r < table.Rows.Count; r++)
            for (var c = 0; c < table.Columns.Count; c++)
            {
                string text;
                ObjectId styleId;
                bool field;
                try
                {
                    text = table.Cells[r, c].TextString;
                    styleId = table.Cells[r, c].TextStyleId ?? ObjectId.Null;
                    field = table.Cells[r, c].Contents.Any(content => content.ContentTypes == CellContentTypes.Field);
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    continue;   // merged or non-text cell
                }

                if (string.IsNullOrEmpty(text)) continue;
                var converted = Text("Table", text, true, styleId, field, table.ObjectId);
                if (converted == null) continue;
                int row = r, column = c;
                changes.Add(() =>
                {
                    try
                    {
                        table.Cells[row, column].TextString = converted;
                        Restyle(styleId, s => table.Cells[row, column].TextStyleId = s);
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        Messages.Add($"Bảng {table.Handle} ô ({row + 1}, {column + 1}): {ex.Message}");
                    }
                });
            }

            if (changes.Count > 0) Change(table, () => changes.ForEach(a => a()));
        }

        /// <summary>
        /// Records the string (unless its object has fields, which is only counted). When converting, returns the new text,
        /// or null when it stays.
        /// </summary>
        private string Text(string kind, string text, bool isMText, ObjectId styleId, bool hasFields, ObjectId owner)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var (upper, font) = StyleFont(styleId);
            var item = new FontTextItem(kind, text, isMText, upper, font);
            if (hasFields)
            {
                FieldObjects.Add(owner);
                Uses.Add((item, styleId, true));
                return null;
            }

            Items.Add(item);
            Uses.Add((item, styleId, false));
            return Writing ? _session.Convert(item) : null;
        }

        private void Restyle(ObjectId styleId, System.Action<ObjectId> set)
        {
            if (_styleMap != null && !styleId.IsNull && _styleMap.TryGetValue(styleId, out var target) && target != styleId) set(target);
        }

        private void Change(DBObject obj, System.Action apply)
        {
            obj.UpgradeOpen();
            apply();
            Changed++;
        }

        /// <summary>Whether the style's font is a TCVN3 all-capitals font, and its typeface (or font file) as a detection hint.</summary>
        private (bool upper, string font) StyleFont(ObjectId styleId)
        {
            if (styleId.IsNull || styleId.IsErased) return (false, null);
            if (_styleFonts.TryGetValue(styleId, out var known)) return known;
            var style = _tr.GetObject(styleId, OpenMode.ForRead) as TextStyleTableRecord;
            var result = (false, (string)null);
            if (style != null)
            {
                var typeface = style.Font.TypeFace;
                var font = string.IsNullOrEmpty(typeface) ? style.FileName : typeface;
                result = (VietFontCodec.IsUpperCaseFont(typeface) || VietFontCodec.IsUpperCaseFont(style.FileName), font);
            }

            _styleFonts[styleId] = result;
            return result;
        }
    }
}
