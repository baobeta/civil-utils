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
            session.Load(Scan(doc, session, selection, write: false).Items);
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
        session.Load(Scan(doc, session, selection, write: false).Items);
        if (session.ChangeCount == 0)
        {
            Prompts.Say(ed, "Không có chữ nào cần chuyển.");
            return;
        }

        Walker walker;
        var styles = 0;
        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            try
            {
                walker = Scan(doc, session, selection, write: true, tr);
                if (session.ReplaceStyleFont && session.CanReplaceStyleFont) styles = ReplaceStyleFonts(tr, walker.Styles, session.TargetFont);
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
        var styleText = styles > 0 ? $", đổi font {styles} kiểu chữ sang {session.TargetFont}" : "";
        Prompts.Say(ed, $"Đã chuyển {walker.Changed} đối tượng{styleText}.\n");
    }

    /// <summary>Reads (write: false) or converts (write: true, inside tr) every text of the scope.</summary>
    private static Walker Scan(Document doc, FontConversionSession session, ObjectId[] selection, bool write, Transaction tr = null)
    {
        if (tr == null)
        {
            using (var readTr = doc.Database.TransactionManager.StartTransaction())
            {
                var result = Scan(doc, session, selection, write, readTr);
                readTr.Commit();
                return result;
            }
        }

        var walker = new Walker(tr, session, write);
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

    /// <summary>Text styles of the converted objects get the Unicode font; returns how many changed.</summary>
    private static int ReplaceStyleFonts(Transaction tr, IEnumerable<ObjectId> styleIds, string font)
    {
        var count = 0;
        foreach (var id in styleIds)
        {
            if (id.IsNull || id.IsErased) continue;
            var style = (TextStyleTableRecord)tr.GetObject(id, OpenMode.ForRead);
            var current = style.Font;
            if (string.Equals(current.TypeFace, font, StringComparison.OrdinalIgnoreCase)) continue;
            style.UpgradeOpen();
            // An SHX style has no typeface: clear the file so the TrueType font is used.
            if (style.FileName != null && style.FileName.EndsWith(".shx", StringComparison.OrdinalIgnoreCase)) style.FileName = "";
            style.Font = new FontDescriptor(font, current.Bold, current.Italic, 0, current.PitchAndFamily);
            count++;
        }

        return count;
    }

    /// <summary>Visits DBText, MText, attributes, attribute definitions, dimension overrides, MLeader MText and table cells.</summary>
    private sealed class Walker
    {
        private readonly Transaction _tr;
        private readonly FontConversionSession _session;
        private readonly bool _write;
        private readonly HashSet<ObjectId> _visitedBlocks = new HashSet<ObjectId>();
        private readonly HashSet<ObjectId> _visitedEntities = new HashSet<ObjectId>();
        private readonly Dictionary<ObjectId, bool> _upperFont = new Dictionary<ObjectId, bool>();

        public Walker(Transaction tr, FontConversionSession session, bool write)
        {
            _tr = tr;
            _session = session;
            _write = write;
        }

        public List<FontTextItem> Items { get; } = new List<FontTextItem>();
        public HashSet<ObjectId> Styles { get; } = new HashSet<ObjectId>();
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
                        if (Text("DBText", text.TextString, false, text.TextStyleId, out var t))
                            Change(text, () => text.TextString = t);
                        break;
                    case MText mtext:
                        if (Text("MText", mtext.Contents, true, mtext.TextStyleId, out var m))
                            Change(mtext, () => mtext.Contents = m);
                        break;
                    case Dimension dimension:
                        var dimText = dimension.DimensionText;
                        if (!string.IsNullOrEmpty(dimText) && Text("Dimension", dimText, true, ObjectId.Null, out var d))
                            Change(dimension, () => dimension.DimensionText = d);
                        break;
                    case MLeader leader:
                        if (leader.ContentType == ContentType.MTextContent)
                        {
                            var leaderText = leader.MText;
                            if (leaderText != null && Text("MLeader", leaderText.Contents, true, leader.TextStyleId, out var l))
                                Change(leader, () =>
                                {
                                    leaderText.Contents = l;
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
            if (attribute.IsMTextAttribute)
            {
                var mtext = attribute.MTextAttribute;
                if (mtext != null && Text("AttributeReference", mtext.Contents, true, attribute.TextStyleId, out var m))
                    Change(attribute, () =>
                    {
                        mtext.Contents = m;
                        attribute.MTextAttribute = mtext;
                    });
            }
            else if (Text("AttributeReference", attribute.TextString, false, attribute.TextStyleId, out var t))
                Change(attribute, () => attribute.TextString = t);
        }

        private void AttributeDefinition(AttributeDefinition definition)
        {
            var changes = new List<Action>();
            if (definition.IsMTextAttributeDefinition)
            {
                var mtext = definition.MTextAttributeDefinition;
                if (mtext != null && Text("AttributeDefinition", mtext.Contents, true, definition.TextStyleId, out var m))
                    changes.Add(() =>
                    {
                        mtext.Contents = m;
                        definition.MTextAttributeDefinition = mtext;
                    });
            }
            else if (Text("AttributeDefinition", definition.TextString, false, definition.TextStyleId, out var t))
                changes.Add(() => definition.TextString = t);

            if (Text("AttributeDefinition", definition.Prompt, false, definition.TextStyleId, out var p)) changes.Add(() => definition.Prompt = p);
            if (changes.Count > 0) Change(definition, () => changes.ForEach(c => c()));
        }

        private void TableCells(Table table)
        {
            var changes = new List<Action>();
            for (var r = 0; r < table.Rows.Count; r++)
            for (var c = 0; c < table.Columns.Count; c++)
            {
                string text;
                try { text = table.Cells[r, c].TextString; }
                catch (Autodesk.AutoCAD.Runtime.Exception) { continue; }   // merged or non-text cell
                if (string.IsNullOrEmpty(text) || !Text("Table", text, true, ObjectId.Null, out var converted)) continue;
                int row = r, column = c;
                changes.Add(() =>
                {
                    try { table.Cells[row, column].TextString = converted; }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex) { Messages.Add($"Bảng {table.Handle} ô ({row + 1}, {column + 1}): {ex.Message}"); }
                });
            }

            if (changes.Count > 0) Change(table, () => changes.ForEach(a => a()));
        }

        /// <summary>Records the string; when writing, returns true with the converted text if it changes.</summary>
        private bool Text(string kind, string text, bool isMText, ObjectId styleId, out string converted)
        {
            converted = null;
            if (string.IsNullOrEmpty(text)) return false;
            var item = new FontTextItem(kind, text, isMText, UpperFont(styleId));
            Items.Add(item);
            if (!_write) return false;
            converted = _session.Convert(item);
            if (converted == null) return false;
            if (!styleId.IsNull) Styles.Add(styleId);
            return true;
        }

        private void Change(DBObject obj, Action apply)
        {
            obj.UpgradeOpen();
            apply();
            Changed++;
        }

        private bool UpperFont(ObjectId styleId)
        {
            if (styleId.IsNull || styleId.IsErased) return false;
            if (_upperFont.TryGetValue(styleId, out var upper)) return upper;
            var style = _tr.GetObject(styleId, OpenMode.ForRead) as TextStyleTableRecord;
            upper = style != null && (VietFontCodec.IsUpperCaseFont(style.Font.TypeFace) || VietFontCodec.IsUpperCaseFont(style.FileName));
            _upperFont[styleId] = upper;
            return upper;
        }
    }
}
