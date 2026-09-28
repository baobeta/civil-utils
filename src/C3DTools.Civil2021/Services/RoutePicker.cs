using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Ui;

namespace C3DTools.Civil2021.Services;

/// <summary>
/// The active route of a drawing: an Xrecord in the Named Object Dictionary holding the alignment's handle, so it is
/// saved with the drawing.
/// </summary>
internal static class ActiveRouteStore
{
    public const string Key = "C3DTOOLS_ACTIVE_ROUTE";

    /// <summary>The stored handle, or null. Never throws: a missing or damaged record means no active route.</summary>
    public static string Read(Transaction tr, Database db)
    {
        try
        {
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!nod.Contains(Key)) return null;
            if (!(tr.GetObject(nod.GetAt(Key), OpenMode.ForRead) is Xrecord record)) return null;
            using (var data = record.Data)
            {
                var values = data?.AsArray();
                return values != null && values.Length > 0 ? values[0].Value as string : null;
            }
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    public static void Write(Transaction tr, Database db, string handle)
    {
        var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
        Xrecord record;
        if (nod.Contains(Key))
        {
            record = (Xrecord)tr.GetObject(nod.GetAt(Key), OpenMode.ForWrite);
        }
        else
        {
            record = new Xrecord();
            nod.SetAt(Key, record);
            tr.AddNewlyCreatedDBObject(record, true);
        }

        using (var data = new ResultBuffer(new TypedValue((int)DxfCode.Text, handle)))
            record.Data = data;
    }
}

/// <summary>"Tuyến hiện hành": which alignment a command starts with, and remembering the one the user picks.</summary>
internal static class RoutePicker
{
    /// <summary>
    /// The alignment to start with (ActiveRoute.Resolve): selected before the command, else the drawing's active
    /// route, else its only alignment; ObjectId.Null when the user has to pick. message: what to tell the user, or null.
    /// </summary>
    public static ObjectId Resolve(Document doc, out string message)
    {
        message = null;
        var ed = doc.Editor;
        var preselected = Preselected(ed);
        try
        {
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var civil = CivilDocument.GetCivilDocument(doc.Database);
                var alignments = civil.GetAlignmentIds().Cast<ObjectId>()
                    .Select(id => tr.GetObject(id, OpenMode.ForRead) as Alignment)
                    .Where(a => a != null)
                    .ToList();
                var choice = ActiveRoute.Resolve(
                    preselected.IsNull ? null : preselected.Handle.ToString(),
                    ActiveRouteStore.Read(tr, doc.Database),
                    alignments.Select(a => a.Handle.ToString()));
                tr.Commit();
                if (!choice.Found) return ObjectId.Null;

                var chosen = alignments.First(a => string.Equals(a.Handle.ToString(), choice.Handle, StringComparison.OrdinalIgnoreCase));
                message = ActiveRoute.Describe(choice.Reason, chosen.Name);
                ToolWindow.Trace($"tuyến: {choice.Reason} {chosen.Name}");
                return chosen.ObjectId;
            }
        }
        catch (System.Exception ex)
        {
            ToolWindow.LogError("RoutePicker.Resolve", ex);
            return preselected;
        }
    }

    /// <summary>Makes the alignment the drawing's active route. A failure only means the next command asks again.</summary>
    public static void Use(Document doc, ObjectId alignmentId)
    {
        if (alignmentId.IsNull) return;
        try
        {
            using (doc.LockDocument())
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                if (!(tr.GetObject(alignmentId, OpenMode.ForRead) is Alignment alignment)) return;
                var handle = alignment.Handle.ToString();
                if (!string.Equals(ActiveRouteStore.Read(tr, doc.Database), handle, StringComparison.OrdinalIgnoreCase))
                    ActiveRouteStore.Write(tr, doc.Database, handle);
                tr.Commit();
            }
        }
        catch (System.Exception ex)
        {
            ToolWindow.LogError("RoutePicker.Use", ex);
        }
    }

    /// <summary>The first alignment selected before the command; the selection is consumed.</summary>
    private static ObjectId Preselected(Editor ed)
    {
        var implied = ed.SelectImplied();
        if (implied.Status != PromptStatus.OK || implied.Value == null) return ObjectId.Null;
        ed.SetImpliedSelection(new ObjectId[0]);
        var alignmentClass = RXObject.GetClass(typeof(Alignment));
        return implied.Value.GetObjectIds().FirstOrDefault(id => id.ObjectClass.IsDerivedFrom(alignmentClass));
    }
}
