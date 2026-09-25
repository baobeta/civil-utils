using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using C3DTools.Civil2021.Drawing;

namespace C3DTools.Civil2021.Curves;

/// <summary>What a tagged object is, so a rerun can replace only some of them ("Chỉ cắm cọc + khung": boxes and stakes).</summary>
internal enum YtcKind : short { Unknown = 0, Curve = 1, Box = 2, Stake = 3, Alignment = 4, Table = 5 }

/// <summary>
/// XData that marks every object CTYTC creates: regapp C3DTOOLS_YTC, (1000 "YTC", 1005 source handle,
/// 1070 curve number, 1070 kind, 1040 Wb, 1040 Wl, 1040 R, 1040 L1, 1040 L2). R/L1/L2 let a rerun on a polyline reload the grid.
/// A ToolTag of tool "YTC" in the 0.2 layout (number before kind).
/// </summary>
internal sealed class YtcTag
{
    public const string Tool = "YTC";
    public const string RegApp = ToolTag.RegAppPrefix + Tool;

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

    public ToolTag ToToolTag()
    {
        var tag = new ToolTag(Tool) { SourceHandle = SourceHandle, Number = Number, Kind = (short)Kind, NumberFirst = true };
        tag.Values.AddRange(new[] { Wb, Wl, Radius, SpiralIn, SpiralOut });
        return tag;
    }

    public ResultBuffer ToXData() => ToToolTag().ToXData();

    public static YtcTag Read(DBObject obj) => From(ToolTag.Read(obj, Tool, numberFirst: true));

    public static void EnsureRegApp(Transaction tr, Database db) => TaggedDrawing.EnsureRegApp(tr, db, RegApp);

    /// <summary>Model-space objects tagged for this source.</summary>
    public static IEnumerable<(ObjectId id, YtcTag tag)> FindTagged(Transaction tr, Database db, string sourceHandle) =>
        TaggedDrawing.FindTagged(tr, db, Tool, sourceHandle, numberFirst: true).Select(f => (f.id, From(f.tag)));

    /// <summary>Wb/Wl/R/L per curve number from any object of a previous run that carries them (curves, boxes or stakes).</summary>
    public static Dictionary<int, YtcTag> ReadCurveInputs(Transaction tr, Database db, string sourceHandle)
    {
        var result = new Dictionary<int, YtcTag>();
        foreach (var (_, tag) in FindTagged(tr, db, sourceHandle))
            if (tag.Number > 0 && tag.Radius > 0 && !result.ContainsKey(tag.Number)) result[tag.Number] = tag;
        return result;
    }

    internal static YtcTag From(ToolTag t)
    {
        if (t == null) return null;
        var v = t.Values;
        return new YtcTag
        {
            SourceHandle = t.SourceHandle,
            Number = t.Number,
            Kind = (YtcKind)t.Kind,
            Wb = v.Count > 0 ? v[0] : 0,
            Wl = v.Count > 1 ? v[1] : 0,
            Radius = v.Count > 2 ? v[2] : 0,
            SpiralIn = v.Count > 3 ? v[3] : 0,
            SpiralOut = v.Count > 4 ? v[4] : 0,
        };
    }
}
