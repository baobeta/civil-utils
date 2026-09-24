using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using AcEntity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace C3DTools.Civil2021.Curves;

/// <summary>What a tagged object is, so a rerun can replace only some of them ("Chỉ cắm cọc + khung": boxes and stakes).</summary>
internal enum YtcKind : short { Unknown = 0, Curve = 1, Box = 2, Stake = 3, Alignment = 4 }

/// <summary>
/// XData that marks every object CTYTC creates: regapp C3DTOOLS_YTC, (1000 "YTC", 1005 source handle,
/// 1070 curve number, 1070 kind, 1040 Wb, 1040 Wl, 1040 R, 1040 L1, 1040 L2). R/L1/L2 let a rerun on a polyline reload the grid.
/// </summary>
internal sealed class YtcTag
{
    public const string RegApp = "C3DTOOLS_YTC";
    private const string Marker = "YTC";

    public string SourceHandle { get; set; }
    public int Number { get; set; }
    public YtcKind Kind { get; set; }
    public double Wb { get; set; }
    public double Wl { get; set; }
    public double Radius { get; set; }
    public double SpiralIn { get; set; }
    public double SpiralOut { get; set; }

    /// <summary>Tag for an object that belongs to one curve: carries the curve's inputs so a rerun can reload them.</summary>
    public static YtcTag For(C3DTools.Core.Curves.DesignedCurve curve, YtcKind kind) => new YtcTag
    {
        Number = curve.Number,
        Kind = kind,
        Wb = curve.Input.Wb,
        Wl = curve.Input.Wl,
        Radius = curve.Input.Radius,
        SpiralIn = curve.Input.SpiralIn,
        SpiralOut = curve.Input.SpiralOut,
    };

    public ResultBuffer ToXData() => new ResultBuffer(
        new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegApp),
        new TypedValue((int)DxfCode.ExtendedDataAsciiString, Marker),
        new TypedValue((int)DxfCode.ExtendedDataHandle, SourceHandle),
        new TypedValue((int)DxfCode.ExtendedDataInteger16, (short)Number),
        new TypedValue((int)DxfCode.ExtendedDataInteger16, (short)Kind),
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
            {
                if (values[i].TypeCode == (short)DxfCode.ExtendedDataReal) reals.Add(Convert.ToDouble(values[i].Value));
                else if (values[i].TypeCode == (short)DxfCode.ExtendedDataInteger16 && i == 4) tag.Kind = (YtcKind)Convert.ToInt16(values[i].Value);
            }

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

    /// <summary>Model-space objects tagged for this source.</summary>
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

    /// <summary>Wb/Wl/R/L per curve number from any object of a previous run that carries them (curves, boxes or stakes).</summary>
    public static Dictionary<int, YtcTag> ReadCurveInputs(Transaction tr, Database db, string sourceHandle)
    {
        var result = new Dictionary<int, YtcTag>();
        foreach (var (_, tag) in FindTagged(tr, db, sourceHandle))
            if (tag.Number > 0 && tag.Radius > 0 && !result.ContainsKey(tag.Number)) result[tag.Number] = tag;
        return result;
    }
}
