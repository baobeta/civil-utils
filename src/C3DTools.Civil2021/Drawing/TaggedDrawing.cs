using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using AcEntity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace C3DTools.Civil2021.Drawing;

/// <summary>
/// The open transaction a command writes into: model space, its layers and its XData tag (regapp C3DTOOLS_&lt;TOOL&gt;).
/// Every object added is tagged with the source handle, so the next run for the same source can erase it.
/// </summary>
internal class TaggedDrawing
{
    private readonly BlockTableRecord _modelSpace;
    private readonly bool _numberFirst;

    public TaggedDrawing(Transaction tr, Database db, string tool, string tagHandle, bool numberFirst = false)
    {
        Transaction = tr;
        Database = db;
        Tool = tool;
        TagHandle = tagHandle;
        _numberFirst = numberFirst;
        EnsureRegApp(tr, db, ToolTag.RegAppName(tool));
        _modelSpace = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
    }

    public Transaction Transaction { get; }
    public Database Database { get; }
    public string Tool { get; }
    public string TagHandle { get; }

    /// <summary>Adds the entity to model space on the layer and tags it for the next run.</summary>
    public void Add(AcEntity entity, string layer, ToolTag tag)
    {
        // No SetDatabaseDefaults: like the LISP's entmake, colour and linetype stay ByLayer whatever CECOLOR is.
        entity.LayerId = LayerId(layer);
        _modelSpace.AppendEntity(entity);
        Transaction.AddNewlyCreatedDBObject(entity, true);
        Tag(entity, tag);
    }

    public void Tag(DBObject obj, ToolTag tag)
    {
        tag.SourceHandle = TagHandle;
        using (var rb = tag.ToXData()) obj.XData = rb;
    }

    /// <summary>
    /// Erases what the previous run for this source created. keep: never erased (the source itself).
    /// shouldErase: decides per object from its id and tag (null = all). beforeErase: called with the object opened for write.
    /// </summary>
    public void EraseTagged(ISet<ObjectId> keep, Func<ObjectId, ToolTag, bool> shouldErase, Action<DBObject> beforeErase = null)
    {
        var found = FindTagged(Transaction, Database, Tool, TagHandle, _numberFirst).ToList();
        foreach (var (id, tag) in found)
        {
            if (keep != null && keep.Contains(id)) continue;
            if (shouldErase != null && !shouldErase(id, tag)) continue;
            var obj = Transaction.GetObject(id, OpenMode.ForWrite);
            beforeErase?.Invoke(obj);
            obj.Erase();
        }
    }

    public ObjectId LayerId(string name)
    {
        var table = (LayerTable)Transaction.GetObject(Database.LayerTableId, OpenMode.ForRead);
        return table[name];
    }

    /// <summary>Creates the layer with this ACI colour if the drawing does not have it; an existing layer is left as it is.</summary>
    public void EnsureLayer(string name, short colorIndex)
    {
        var table = (LayerTable)Transaction.GetObject(Database.LayerTableId, OpenMode.ForRead);
        if (table.Has(name)) return;
        table.UpgradeOpen();
        var layer = new LayerTableRecord { Name = name, Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex) };
        table.Add(layer);
        Transaction.AddNewlyCreatedDBObject(layer, true);
    }

    public static void EnsureRegApp(Transaction tr, Database db, string regApp)
    {
        var table = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
        if (table.Has(regApp)) return;
        table.UpgradeOpen();
        var record = new RegAppTableRecord { Name = regApp };
        table.Add(record);
        tr.AddNewlyCreatedDBObject(record, true);
    }

    /// <summary>Model-space objects tagged by this tool for this source.</summary>
    public static IEnumerable<(ObjectId id, ToolTag tag)> FindTagged(Transaction tr, Database db, string tool, string sourceHandle, bool numberFirst = false)
    {
        var modelSpace = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
        foreach (ObjectId id in modelSpace)
        {
            if (id.IsErased) continue;
            var obj = tr.GetObject(id, OpenMode.ForRead) as AcEntity;
            if (obj == null) continue;
            var tag = ToolTag.Read(obj, tool, numberFirst);
            if (tag != null && string.Equals(tag.SourceHandle, sourceHandle, StringComparison.OrdinalIgnoreCase))
                yield return (id, tag);
        }
    }
}
