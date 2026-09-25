using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using C3DTools.Civil2021.Drawing;
using Alignment = Autodesk.Civil.DatabaseServices.Alignment;
using AcEntity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace C3DTools.Civil2021.Curves;

/// <summary>The open transaction a CTYTC run writes into: model space, the YTC layers, and the XData tag.</summary>
internal sealed class RouteDrawing : TaggedDrawing
{
    public const string CurveLayer = "YTC_CONG";
    public const string StakeLayer = "YTC_COC";
    public const string BoxLayer = "YTC_BANG";

    public RouteDrawing(Transaction tr, Database db, string tagHandle)
        : base(tr, db, YtcTag.Tool, tagHandle, numberFirst: true)
    {
        EnsureLayer(CurveLayer, 3);
        EnsureLayer(StakeLayer, 1);
        EnsureLayer(BoxLayer, 3);
    }

    /// <summary>Adds the entity to model space on the layer and tags it for the next run.</summary>
    public void Add(AcEntity entity, string layer, YtcTag tag)
    {
        tag.SourceHandle = TagHandle;
        Add(entity, layer, tag.ToToolTag());
    }

    public void Tag(DBObject obj, YtcTag tag)
    {
        tag.SourceHandle = TagHandle;
        Tag(obj, tag.ToToolTag());
    }

    /// <summary>
    /// Erases what the previous run for this source created. keep: never erased (the source itself).
    /// kinds: only these kinds are erased; null = every kind. Alignments are erased only when eraseAlignments is set.
    /// </summary>
    public void EraseTagged(ISet<ObjectId> keep, ISet<YtcKind> kinds, bool eraseAlignments, Action<string> warn)
    {
        var alignmentClass = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Alignment));
        EraseTagged(keep,
            (id, tag) => (kinds == null || kinds.Contains((YtcKind)tag.Kind))
                && (eraseAlignments || !id.ObjectClass.IsDerivedFrom(alignmentClass)),
            obj =>
            {
                if (obj.ObjectId.ObjectClass.IsDerivedFrom(alignmentClass))
                    warn($"Alignment cũ {((Alignment)obj).Name} sẽ bị xóa; profile/corridor dựa trên nó sẽ mất.");
            });
    }
}
