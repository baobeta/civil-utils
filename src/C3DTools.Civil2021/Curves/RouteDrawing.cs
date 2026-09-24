using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using AcEntity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace C3DTools.Civil2021.Curves;

/// <summary>The open transaction a CTYTC run writes into: model space, the YTC layers, and the XData tag.</summary>
internal sealed class RouteDrawing
{
    public const string CurveLayer = "YTC_CONG";
    public const string StakeLayer = "YTC_COC";
    public const string BoxLayer = "YTC_BANG";

    private readonly BlockTableRecord _modelSpace;

    public RouteDrawing(Transaction tr, Database db, string tagHandle)
    {
        Transaction = tr;
        Database = db;
        TagHandle = tagHandle;
        YtcTag.EnsureRegApp(tr, db);
        EnsureLayer(CurveLayer, 3);
        EnsureLayer(StakeLayer, 1);
        EnsureLayer(BoxLayer, 3);
        _modelSpace = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
    }

    public Transaction Transaction { get; }
    public Database Database { get; }
    public string TagHandle { get; }

    /// <summary>Adds the entity to model space on the layer and tags it for the next run.</summary>
    public void Add(AcEntity entity, string layer, YtcTag tag)
    {
        // No SetDatabaseDefaults: like the LISP's entmake, colour and linetype stay ByLayer whatever CECOLOR is.
        entity.LayerId = LayerId(layer);
        _modelSpace.AppendEntity(entity);
        Transaction.AddNewlyCreatedDBObject(entity, true);
        Tag(entity, tag);
    }

    public void Tag(DBObject obj, YtcTag tag)
    {
        tag.SourceHandle = TagHandle;
        using (var rb = tag.ToXData()) obj.XData = rb;
    }

    /// <summary>Erases what the previous run for this source created. keep: never erased (the source itself, alignments to keep).</summary>
    public int EraseTagged(ISet<ObjectId> keep, bool eraseAlignments)
    {
        var alignmentClass = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Autodesk.Civil.DatabaseServices.Alignment));
        var ids = YtcTag.FindTagged(Transaction, Database, TagHandle).Select(t => t.id).ToList();
        var count = 0;
        foreach (var id in ids)
        {
            if (keep.Contains(id)) continue;
            if (!eraseAlignments && id.ObjectClass.IsDerivedFrom(alignmentClass)) continue;
            var obj = Transaction.GetObject(id, OpenMode.ForWrite);
            obj.Erase();
            count++;
        }

        return count;
    }

    public ObjectId LayerId(string name)
    {
        var table = (LayerTable)Transaction.GetObject(Database.LayerTableId, OpenMode.ForRead);
        return table[name];
    }

    private void EnsureLayer(string name, short colorIndex)
    {
        var table = (LayerTable)Transaction.GetObject(Database.LayerTableId, OpenMode.ForRead);
        if (table.Has(name)) return;
        table.UpgradeOpen();
        var layer = new LayerTableRecord { Name = name, Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex) };
        table.Add(layer);
        Transaction.AddNewlyCreatedDBObject(layer, true);
    }
}
