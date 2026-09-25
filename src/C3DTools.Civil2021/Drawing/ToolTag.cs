using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace C3DTools.Civil2021.Drawing;

/// <summary>
/// XData that marks every object a C3DTools command creates, so a rerun can find and replace them.
/// Regapp C3DTOOLS_&lt;TOOL&gt;: (1000 tool marker, 1005 source handle, 1070 kind, 1070 number, up to 8 × 1040, optional 1000 text).
/// </summary>
internal sealed class ToolTag
{
    public const string RegAppPrefix = "C3DTOOLS_";
    public const int MaxValues = 8;

    public ToolTag(string tool)
    {
        if (string.IsNullOrEmpty(tool)) throw new ArgumentNullException(nameof(tool));
        Tool = tool;
    }

    /// <summary>Short upper-case tool name, e.g. "YTC"; also the 1000 marker.</summary>
    public string Tool { get; }

    public string RegApp => RegAppName(Tool);

    public string SourceHandle { get; set; }
    public short Kind { get; set; }
    public int Number { get; set; }
    public List<double> Values { get; } = new List<double>();

    /// <summary>Optional; written only when not null.</summary>
    public string Text { get; set; }

    /// <summary>CTYTC 0.2 wrote the number before the kind; kept so its drawings read and write unchanged.</summary>
    public bool NumberFirst { get; set; }

    public static string RegAppName(string tool) => RegAppPrefix + tool;

    public ResultBuffer ToXData()
    {
        if (Values.Count > MaxValues) throw new InvalidOperationException($"Tối đa {MaxValues} giá trị trong XData.");
        var rb = new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegApp),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, Tool),
            new TypedValue((int)DxfCode.ExtendedDataHandle, SourceHandle));
        var first = NumberFirst ? (short)Number : Kind;
        var second = NumberFirst ? Kind : (short)Number;
        rb.Add(new TypedValue((int)DxfCode.ExtendedDataInteger16, first));
        rb.Add(new TypedValue((int)DxfCode.ExtendedDataInteger16, second));
        foreach (var v in Values) rb.Add(new TypedValue((int)DxfCode.ExtendedDataReal, v));
        if (Text != null) rb.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, Text));
        return rb;
    }

    /// <summary>The tag of this tool on obj, or null. Tolerates shorter tags (missing second short, fewer values).</summary>
    public static ToolTag Read(DBObject obj, string tool, bool numberFirst = false)
    {
        using (var rb = obj.GetXDataForApplication(RegAppName(tool)))
        {
            if (rb == null) return null;
            var values = rb.AsArray();
            if (values.Length < 4 || !Equals(values[1].Value as string, tool)) return null;

            var tag = new ToolTag(tool) { SourceHandle = Convert.ToString(values[2].Value), NumberFirst = numberFirst };
            var first = Convert.ToInt32(values[3].Value);
            short second = 0;
            for (var i = 4; i < values.Length; i++)
            {
                if (values[i].TypeCode == (short)DxfCode.ExtendedDataReal) tag.Values.Add(Convert.ToDouble(values[i].Value));
                else if (values[i].TypeCode == (short)DxfCode.ExtendedDataInteger16 && i == 4) second = Convert.ToInt16(values[i].Value);
                else if (values[i].TypeCode == (short)DxfCode.ExtendedDataAsciiString && tag.Text == null) tag.Text = Convert.ToString(values[i].Value);
            }

            if (numberFirst)
            {
                tag.Number = first;
                tag.Kind = second;
            }
            else
            {
                tag.Kind = (short)first;
                tag.Number = second;
            }

            return tag;
        }
    }
}
