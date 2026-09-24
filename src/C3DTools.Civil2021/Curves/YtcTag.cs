using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using AcEntity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace C3DTools.Civil2021.Curves;

/// <summary>
/// XData that marks every object CTYTC creates: regapp C3DTOOLS_YTC, (1000 "YTC", 1005 source handle,
/// 1070 curve number, 1040 Wb, 1040 Wl, 1040 R, 1040 L1, 1040 L2). R/L1/L2 let a rerun on a polyline reload the grid.
/// </summary>
internal sealed class YtcTag
{
    public const string RegApp = "C3DTOOLS_YTC";
    private const string Marker = "YTC";

    public string SourceHandle { get; set; }
    public int Number { get; set; }
    public double Wb { get; set; }
    public double Wl { get; set; }
    public double Radius { get; set; }
    public double SpiralIn { get; set; }
    public double SpiralOut { get; set; }

    public ResultBuffer ToXData() => new ResultBuffer(
        new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegApp),
        new TypedValue((int)DxfCode.ExtendedDataAsciiString, Marker),
        new TypedValue((int)DxfCode.ExtendedDataHandle, SourceHandle),
        new TypedValue((int)DxfCode.ExtendedDataInteger16, (short)Number),
        new TypedValue((int)DxfCode.ExtendedDataReal, Wb),
        new TypedValue((int)DxfCode.ExtendedDataReal, Wl),
        new TypedValue((int)DxfCode.ExtendedDataReal, Radius),
        new TypedValue((int)DxfCode.ExtendedDataReal, SpiralIn),
        new TypedValue((int)DxfCode.ExtendedDataReal, SpiralOut));

    public static YtcTag Read(DBObject obj)
    {
        using (var rb = obj.GetXDataForApplication(RegApp))
        {
            if (rb == null) return null;
            var values = rb.AsArray();
            if (values.Length < 4 || !Equals(values[1].Value as string, Marker)) return null;

            var tag = new YtcTag { SourceHandle = Convert.ToString(values[2].Value), Number = Convert.ToInt32(values[3].Value) };
            var reals = new List<double>();
            for (var i = 4; i < values.Length; i++)
                if (values[i].TypeCode == (short)DxfCode.ExtendedDataReal) reals.Add(Convert.ToDouble(values[i].Value));
            if (reals.Count > 0) tag.Wb = reals[0];
            if (reals.Count > 1) tag.Wl = reals[1];
            if (reals.Count > 2) tag.Radius = reals[2];
            if (reals.Count > 3) tag.SpiralIn = reals[3];
            if (reals.Count > 4) tag.SpiralOut = reals[4];
            return tag;
        }
    }

    public static void EnsureRegApp(Transaction tr, Database db)
    {
        var table = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
        if (table.Has(RegApp)) return;
        table.UpgradeOpen();
        var record = new RegAppTableRecord { Name = RegApp };
        table.Add(record);
        tr.AddNewlyCreatedDBObject(record, true);
    }

    /// <summary>Model-space objects tagged for this source, by curve number (boxes first, as they carry Wb/Wl).</summary>
    public static IEnumerable<(ObjectId id, YtcTag tag)> FindTagged(Transaction tr, Database db, string sourceHandle)
    {
        var modelSpace = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
        foreach (ObjectId id in modelSpace)
        {
            if (id.IsErased) continue;
            var obj = tr.GetObject(id, OpenMode.ForRead) as AcEntity;
            if (obj == null) continue;
            var tag = Read(obj);
            if (tag != null && string.Equals(tag.SourceHandle, sourceHandle, StringComparison.OrdinalIgnoreCase))
                yield return (id, tag);
        }
    }

    /// <summary>Wb/Wl/R/L per curve number from the MText boxes of a previous run.</summary>
    public static Dictionary<int, YtcTag> ReadBoxes(Transaction tr, Database db, string sourceHandle)
    {
        var result = new Dictionary<int, YtcTag>();
        foreach (var (id, tag) in FindTagged(tr, db, sourceHandle))
        {
            if (tag.Number <= 0 || result.ContainsKey(tag.Number)) continue;
            if (id.ObjectClass.IsDerivedFrom(Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(MText))))
                result[tag.Number] = tag;
        }

        return result;
    }
}
