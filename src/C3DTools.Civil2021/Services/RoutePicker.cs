using System;
using System.Collections;
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
/// The active route of a drawing: an Xrecord in the Named Object Dictionary holding the alignment's handle, so it
/// is saved with the drawing. The handle is valid only in this drawing; if the record is carried into another
/// drawing the rule ignores a handle that matches no alignment there.
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

/// <summary>
/// "Tuyến hiện hành": which alignment a command starts with, session memory of the chosen alignment, and
/// persisting that choice to the drawing only when a command actually applies (writes) its results.
/// Contract: call <see cref="Remember"/> after a successful alignment load; call <see cref="Save"/> inside the
/// existing write transaction just before its Commit; never open a transaction just for the route.
/// </summary>
internal static class RoutePicker
{
    /// <summary>
    /// The alignment to start with (ActiveRoute.Resolve): selected before the command, else the remembered or
    /// stored handle, else the drawing's only alignment; ObjectId.Null when the user must pick.
    /// message: what to tell the user, or null.
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
                var ids = civil.GetAlignmentIds().Cast<ObjectId>()
                    .Where(id => !id.IsErased)
                    .ToList();
                var handles = ids.Select(id => id.Handle.ToString()).ToList();
                var stored = ReadRemembered(doc) ?? ActiveRouteStore.Read(tr, doc.Database);
                var choice = ActiveRoute.Resolve(
                    preselected.IsNull ? null : preselected.Handle.ToString(),
                    stored,
                    handles);
                if (!choice.Found)
                {
                    tr.Commit();
                    return ObjectId.Null;
                }

                var chosenId = ids.First(id => string.Equals(id.Handle.ToString(), choice.Handle, StringComparison.OrdinalIgnoreCase));
                var name = ((Alignment)tr.GetObject(chosenId, OpenMode.ForRead)).Name;
                tr.Commit();
                message = ActiveRoute.Describe(choice.Reason, name);
                ToolWindow.Trace($"tuyến: {choice.Reason} {name}");
                return chosenId;
            }
        }
        catch (System.Exception ex)
        {
            ToolWindow.LogError("RoutePicker.Resolve", ex);
            return preselected;
        }
    }

    /// <summary>
    /// Remembers the alignment for this session (doc.UserData); no transaction, no drawing change.
    /// Call after a successful alignment load. Does nothing for a null id. Never throws.
    /// </summary>
    public static void Remember(Document doc, ObjectId alignmentId)
    {
        if (alignmentId.IsNull) return;
        try
        {
            doc.UserData[ActiveRouteStore.Key] = alignmentId.Handle.ToString();
        }
        catch (System.Exception ex)
        {
            ToolWindow.LogError("RoutePicker.Remember", ex);
        }
    }

    /// <summary>
    /// Persists the remembered handle to the drawing's Named Object Dictionary inside the caller's own write
    /// transaction (the document must already be locked). Call just before the transaction's Commit.
    /// Returns true when the drawing now holds the remembered handle (including when it already did).
    /// Returns false when nothing was remembered or when writing fails (logs the error). Never throws.
    /// </summary>
    public static bool Save(Transaction tr, Document doc)
    {
        try
        {
            var handle = ReadRemembered(doc);
            if (handle == null) return false;
            if (!string.Equals(ActiveRouteStore.Read(tr, doc.Database), handle, StringComparison.OrdinalIgnoreCase))
                ActiveRouteStore.Write(tr, doc.Database, handle);
            return true;
        }
        catch (System.Exception ex)
        {
            ToolWindow.LogError("RoutePicker.Save", ex);
            return false;
        }
    }

    /// <summary>The handle remembered in doc.UserData for this session, or null.</summary>
    private static string ReadRemembered(Document doc)
    {
        try
        {
            return (doc.UserData as Hashtable)?[ActiveRouteStore.Key] as string;
        }
        catch (System.Exception)
        {
            return null;
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
