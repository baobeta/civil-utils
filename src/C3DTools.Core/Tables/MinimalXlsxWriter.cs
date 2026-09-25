using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

namespace C3DTools.Core.Tables;

/// <summary>
/// A one-sheet .xlsx without any library: the six OpenXML parts in a zip. Row 1 is the header (bold, frozen),
/// every cell has a thin border, text is an inline string and plain InvariantCulture numbers are numeric cells so Excel can sum them.
/// </summary>
public static class MinimalXlsxWriter
{
    private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const int MaxSheetName = 31;
    private const double MinWidth = 8, MaxWidth = 60;
    private const int BodyStyle = 1, HeaderStyle = 2;

    public static void Write(TableData table, Stream stream, string sheetName)
    {
        if (table == null) throw new ArgumentNullException(nameof(table));
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        Part(zip, "[Content_Types].xml", ContentTypes);
        Part(zip, "_rels/.rels", PackageRels);
        Part(zip, "xl/workbook.xml", w => Workbook(w, SheetName(sheetName)));
        Part(zip, "xl/_rels/workbook.xml.rels", WorkbookRels);
        Part(zip, "xl/styles.xml", Styles);
        Part(zip, "xl/worksheets/sheet1.xml", w => Sheet(w, table));
    }

    /// <summary>
    /// A number Excel should see as a number: optional sign, digits, optional '.' decimals, InvariantCulture.
    /// No thousands separators, exponent, spaces or NaN, so "1+234.56", "1,5" and "007A" stay text.
    /// </summary>
    public static bool TryNumber(string text, out double value)
    {
        value = 0;
        if (string.IsNullOrEmpty(text)) return false;
        return double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)
               && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>Excel's rules: at most 31 chars, none of : \ / ? * [ ], no leading or trailing apostrophe, not empty.</summary>
    public static string SheetName(string name)
    {
        var sb = new StringBuilder();
        foreach (var ch in name ?? "") sb.Append(":\\/?*[]".IndexOf(ch) >= 0 ? '_' : ch);
        var result = XmlSafe(sb.ToString()).Trim('\'');
        if (result.Length > MaxSheetName) result = result.Substring(0, MaxSheetName).TrimEnd('\'');
        return result.Trim().Length == 0 ? "Sheet1" : result;
    }

    /// <summary>0 → A, 25 → Z, 26 → AA.</summary>
    public static string ColumnName(int index)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        var name = "";
        for (var n = index + 1; n > 0; n = (n - 1) / 26) name = (char)('A' + (n - 1) % 26) + name;
        return name;
    }

    private static void Part(ZipArchive zip, string name, Action<XmlWriter> write)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var s = entry.Open();
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false };
        using var w = XmlWriter.Create(s, settings);
        w.WriteStartDocument(true);
        write(w);
        w.WriteEndDocument();
    }

    private static void ContentTypes(XmlWriter w)
    {
        const string ns = "http://schemas.openxmlformats.org/package/2006/content-types";
        w.WriteStartElement("Types", ns);
        Default(w, ns, "rels", "application/vnd.openxmlformats-package.relationships+xml");
        Default(w, ns, "xml", "application/xml");
        Override(w, ns, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
        Override(w, ns, "/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
        Override(w, ns, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
        w.WriteEndElement();
    }

    private static void Default(XmlWriter w, string ns, string extension, string type)
    {
        w.WriteStartElement("Default", ns);
        w.WriteAttributeString("Extension", extension);
        w.WriteAttributeString("ContentType", type);
        w.WriteEndElement();
    }

    private static void Override(XmlWriter w, string ns, string part, string type)
    {
        w.WriteStartElement("Override", ns);
        w.WriteAttributeString("PartName", part);
        w.WriteAttributeString("ContentType", type);
        w.WriteEndElement();
    }

    private static void PackageRels(XmlWriter w)
    {
        w.WriteStartElement("Relationships", PackageRelNs);
        Relationship(w, "rId1", RelNs + "/officeDocument", "xl/workbook.xml");
        w.WriteEndElement();
    }

    private static void WorkbookRels(XmlWriter w)
    {
        w.WriteStartElement("Relationships", PackageRelNs);
        Relationship(w, "rId1", RelNs + "/worksheet", "worksheets/sheet1.xml");
        Relationship(w, "rId2", RelNs + "/styles", "styles.xml");
        w.WriteEndElement();
    }

    private static void Relationship(XmlWriter w, string id, string type, string target)
    {
        w.WriteStartElement("Relationship", PackageRelNs);
        w.WriteAttributeString("Id", id);
        w.WriteAttributeString("Type", type);
        w.WriteAttributeString("Target", target);
        w.WriteEndElement();
    }

    private static void Workbook(XmlWriter w, string sheetName)
    {
        w.WriteStartElement("workbook", MainNs);
        w.WriteAttributeString("xmlns", "r", null, RelNs);
        w.WriteStartElement("sheets", MainNs);
        w.WriteStartElement("sheet", MainNs);
        w.WriteAttributeString("name", sheetName);
        w.WriteAttributeString("sheetId", "1");
        w.WriteAttributeString("id", RelNs, "rId1");
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
    }

    /// <summary>Fonts: 0 regular, 1 bold. Borders: 0 none, 1 thin. Cell styles: 0 default, 1 body (thin border), 2 header (bold, thin border).</summary>
    private static void Styles(XmlWriter w)
    {
        w.WriteStartElement("styleSheet", MainNs);

        w.WriteStartElement("fonts", MainNs);
        w.WriteAttributeString("count", "2");
        Font(w, false);
        Font(w, true);
        w.WriteEndElement();

        // Excel expects the two built-in fills.
        w.WriteStartElement("fills", MainNs);
        w.WriteAttributeString("count", "2");
        foreach (var pattern in new[] { "none", "gray125" })
        {
            w.WriteStartElement("fill", MainNs);
            w.WriteStartElement("patternFill", MainNs);
            w.WriteAttributeString("patternType", pattern);
            w.WriteEndElement();
            w.WriteEndElement();
        }
        w.WriteEndElement();

        w.WriteStartElement("borders", MainNs);
        w.WriteAttributeString("count", "2");
        Border(w, null);
        Border(w, "thin");
        w.WriteEndElement();

        w.WriteStartElement("cellStyleXfs", MainNs);
        w.WriteAttributeString("count", "1");
        Xf(w, 0, 0, false);
        w.WriteEndElement();

        w.WriteStartElement("cellXfs", MainNs);
        w.WriteAttributeString("count", "3");
        Xf(w, 0, 0, true);
        Xf(w, 0, 1, true);
        Xf(w, 1, 1, true);
        w.WriteEndElement();

        w.WriteStartElement("cellStyles", MainNs);
        w.WriteAttributeString("count", "1");
        w.WriteStartElement("cellStyle", MainNs);
        w.WriteAttributeString("name", "Normal");
        w.WriteAttributeString("xfId", "0");
        w.WriteAttributeString("builtinId", "0");
        w.WriteEndElement();
        w.WriteEndElement();

        w.WriteEndElement();
    }

    private static void Font(XmlWriter w, bool bold)
    {
        w.WriteStartElement("font", MainNs);
        if (bold) w.WriteElementString("b", MainNs, "");
        Val(w, "sz", "11");
        Val(w, "name", "Calibri");
        Val(w, "family", "2");
        w.WriteEndElement();
    }

    private static void Border(XmlWriter w, string style)
    {
        w.WriteStartElement("border", MainNs);
        foreach (var side in new[] { "left", "right", "top", "bottom", "diagonal" })
        {
            w.WriteStartElement(side, MainNs);
            if (style != null && side != "diagonal")
            {
                w.WriteAttributeString("style", style);
                w.WriteStartElement("color", MainNs);
                w.WriteAttributeString("indexed", "64");
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void Xf(XmlWriter w, int fontId, int borderId, bool withXfId)
    {
        w.WriteStartElement("xf", MainNs);
        w.WriteAttributeString("numFmtId", "0");
        w.WriteAttributeString("fontId", fontId.ToString(CultureInfo.InvariantCulture));
        w.WriteAttributeString("fillId", "0");
        w.WriteAttributeString("borderId", borderId.ToString(CultureInfo.InvariantCulture));
        if (withXfId)
        {
            w.WriteAttributeString("xfId", "0");
            if (fontId != 0) w.WriteAttributeString("applyFont", "1");
            if (borderId != 0) w.WriteAttributeString("applyBorder", "1");
        }
        w.WriteEndElement();
    }

    private static void Val(XmlWriter w, string name, string value)
    {
        w.WriteStartElement(name, MainNs);
        w.WriteAttributeString("val", value);
        w.WriteEndElement();
    }

    private static void Sheet(XmlWriter w, TableData table)
    {
        var columns = table.Headers.Count;
        w.WriteStartElement("worksheet", MainNs);

        w.WriteStartElement("dimension", MainNs);
        w.WriteAttributeString("ref", "A1:" + ColumnName(columns - 1) + (table.Rows.Count + 1).ToString(CultureInfo.InvariantCulture));
        w.WriteEndElement();

        w.WriteStartElement("sheetViews", MainNs);
        w.WriteStartElement("sheetView", MainNs);
        w.WriteAttributeString("workbookViewId", "0");
        w.WriteStartElement("pane", MainNs);
        w.WriteAttributeString("ySplit", "1");
        w.WriteAttributeString("topLeftCell", "A2");
        w.WriteAttributeString("activePane", "bottomLeft");
        w.WriteAttributeString("state", "frozen");
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();

        w.WriteStartElement("cols", MainNs);
        for (var c = 0; c < columns; c++)
        {
            var longest = table.Rows.Select(r => (r[c] ?? "").Length).Concat(new[] { (table.Headers[c] ?? "").Length }).Max();
            var width = Math.Max(MinWidth, Math.Min(MaxWidth, longest + 2));
            var index = (c + 1).ToString(CultureInfo.InvariantCulture);
            w.WriteStartElement("col", MainNs);
            w.WriteAttributeString("min", index);
            w.WriteAttributeString("max", index);
            w.WriteAttributeString("width", width.ToString("R", CultureInfo.InvariantCulture));
            w.WriteAttributeString("customWidth", "1");
            w.WriteEndElement();
        }
        w.WriteEndElement();

        w.WriteStartElement("sheetData", MainNs);
        Row(w, 1, table.Headers.ToArray(), HeaderStyle, numbers: false);
        for (var r = 0; r < table.Rows.Count; r++) Row(w, r + 2, table.Rows[r], BodyStyle, numbers: true);
        w.WriteEndElement();

        w.WriteEndElement();
    }

    private static void Row(XmlWriter w, int number, string[] cells, int style, bool numbers)
    {
        var row = number.ToString(CultureInfo.InvariantCulture);
        w.WriteStartElement("row", MainNs);
        w.WriteAttributeString("r", row);
        for (var c = 0; c < cells.Length; c++)
        {
            var text = cells[c] ?? "";
            w.WriteStartElement("c", MainNs);
            w.WriteAttributeString("r", ColumnName(c) + row);
            w.WriteAttributeString("s", style.ToString(CultureInfo.InvariantCulture));
            if (numbers && TryNumber(text, out var value))
            {
                w.WriteAttributeString("t", "n");
                w.WriteElementString("v", MainNs, value.ToString("R", CultureInfo.InvariantCulture));
            }
            else
            {
                w.WriteAttributeString("t", "inlineStr");
                w.WriteStartElement("is", MainNs);
                w.WriteStartElement("t", MainNs);
                w.WriteAttributeString("xml", "space", null, "preserve");
                w.WriteString(XmlSafe(text));
                w.WriteEndElement();
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    /// <summary>Drops characters XML 1.0 cannot hold (control characters, lone surrogates).</summary>
    private static string XmlSafe(string text)
    {
        if (text.All(XmlConvert.IsXmlChar)) return text;
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (XmlConvert.IsXmlChar(text[i])) sb.Append(text[i]);
            else if (i + 1 < text.Length && XmlConvert.IsXmlSurrogatePair(text[i + 1], text[i]))
            {
                sb.Append(text[i]).Append(text[i + 1]);
                i++;
            }
        }
        return sb.ToString();
    }
}
