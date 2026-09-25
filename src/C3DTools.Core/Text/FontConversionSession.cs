using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using C3DTools.Core.Presets;

namespace C3DTools.Core.Text;

/// <summary>One text string read from the drawing. Kind: "DBText", "MText", "AttributeReference", …</summary>
public sealed class FontTextItem
{
    public FontTextItem(string kind, string text, bool isMText, bool upperCaseFont)
    {
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        Text = text ?? "";
        IsMText = isMText;
        UpperCaseFont = upperCaseFont;
    }

    public string Kind { get; }
    public string Text { get; }

    /// <summary>MText contents (control codes kept, \f replaced), not plain text.</summary>
    public bool IsMText { get; }

    /// <summary>The text style's font is a TCVN3 all-capitals font (.VnTimeH).</summary>
    public bool UpperCaseFont { get; }
}

/// <summary>A before/after line of the CTFONT preview grid.</summary>
public sealed class FontPreviewRow
{
    public FontPreviewRow(string kind, string before, string after)
    {
        Kind = kind;
        Before = before;
        After = after;
    }

    public string Kind { get; }
    public string Before { get; }
    public string After { get; }
}

/// <summary>State of the CTFONT dialog: scope, source/target encoding, and what the last scan would change.</summary>
public sealed class FontConversionSession : INotifyPropertyChanged
{
    public const int PreviewLimit = 20;

    private static readonly VietEncoding?[] Sources = { null, VietEncoding.Unicode, VietEncoding.Tcvn3, VietEncoding.Vni };
    private static readonly VietEncoding[] Targets = { VietEncoding.Unicode, VietEncoding.Tcvn3, VietEncoding.Vni };

    private readonly List<FontTextItem> _items = new List<FontTextItem>();
    private int _sourceIndex, _targetIndex, _changeCount;
    private bool _wholeDrawing, _replaceStyleFont = true, _convertBlockDefinitions, _stale;
    private string _detectedText = "";
    private VietEncoding _preferred = VietEncoding.Unicode;

    public FontConversionSession(FontConversionOptions options)
    {
        var font = options?.TargetFont;
        TargetFont = string.IsNullOrWhiteSpace(font) ? "Arial" : font.Trim();
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public IReadOnlyList<string> SourceNames { get; } = new[] { "Tự nhận dạng", "Unicode", "TCVN3 (ABC)", "VNI Windows" };
    public IReadOnlyList<string> TargetNames { get; } = new[] { "Unicode", "TCVN3 (ABC)", "VNI Windows" };

    /// <summary>Font of converted text styles and of \f codes when the target is Unicode (preset FontConversion.TargetFont).</summary>
    public string TargetFont { get; }

    public int SourceIndex
    {
        get => _sourceIndex;
        set
        {
            if (value < 0 || value >= Sources.Length || value == _sourceIndex) return;
            _sourceIndex = value;
            Raise(nameof(SourceIndex));
            Raise(nameof(Source));
            Recompute();
        }
    }

    public int TargetIndex
    {
        get => _targetIndex;
        set
        {
            if (value < 0 || value >= Targets.Length || value == _targetIndex) return;
            _targetIndex = value;
            Raise(nameof(TargetIndex));
            Raise(nameof(Target));
            Raise(nameof(CanReplaceStyleFont));
            Recompute();
        }
    }

    /// <summary>Null: detect per string.</summary>
    public VietEncoding? Source => Sources[_sourceIndex];
    public VietEncoding Target => Targets[_targetIndex];

    /// <summary>False: the selection. True: every layout of the drawing.</summary>
    public bool WholeDrawing { get => _wholeDrawing; set => SetScope(ref _wholeDrawing, value, nameof(WholeDrawing)); }

    /// <summary>Also the texts and attribute definitions inside block definitions.</summary>
    public bool ConvertBlockDefinitions { get => _convertBlockDefinitions; set => SetScope(ref _convertBlockDefinitions, value, nameof(ConvertBlockDefinitions)); }

    /// <summary>Set the font of the text styles of converted objects to TargetFont (Unicode target only).</summary>
    public bool ReplaceStyleFont
    {
        get => _replaceStyleFont;
        set
        {
            if (_replaceStyleFont == value) return;
            _replaceStyleFont = value;
            Raise(nameof(ReplaceStyleFont));
        }
    }

    public bool CanReplaceStyleFont => Target == VietEncoding.Unicode;

    /// <summary>The scope changed since the last Load: preview and counts are out of date.</summary>
    public bool IsStale => _stale;

    public IReadOnlyList<FontTextItem> Items => _items;
    public ObservableCollection<FontPreviewRow> Preview { get; } = new ObservableCollection<FontPreviewRow>();
    public int ChangeCount => _changeCount;

    /// <summary>One line per object kind: "DBText: TCVN3 12, Unicode 3".</summary>
    public string DetectedText => _detectedText;

    public bool CanApply => _stale || _changeCount > 0;

    public string SummaryText =>
        _stale ? "Bấm Xem trước để quét lại"
        : _changeCount == 0 ? "Không có chuỗi nào cần chuyển"
        : _changeCount.ToString(CultureInfo.InvariantCulture) + " chuỗi sẽ được chuyển";

    /// <summary>Replaces the scanned strings and recomputes counts and preview.</summary>
    public void Load(IEnumerable<FontTextItem> items)
    {
        _items.Clear();
        if (items != null) _items.AddRange(items.Where(i => i != null));
        _stale = false;
        _preferred = Dominant(_items);
        Raise(nameof(IsStale));
        Recompute();
    }

    /// <summary>The legacy encoding most scanned strings clearly use (breaks ties such as "Cát" / "Cỏt"), else Unicode.</summary>
    private static VietEncoding Dominant(IEnumerable<FontTextItem> items)
    {
        var legacy = items.Select(i => VietFontCodec.Detect(i.Text)).Where(e => e != VietEncoding.Unicode)
            .GroupBy(e => e).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).FirstOrDefault();
        return legacy?.Key ?? VietEncoding.Unicode;
    }

    /// <summary>The converted text, or null when the item stays as it is.</summary>
    public string Convert(FontTextItem item)
    {
        if (item == null || item.Text.Length == 0) return null;
        var from = Source ?? VietFontCodec.Detect(item.Text, _preferred);
        if (from == Target) return null;
        var result = item.IsMText
            ? VietFontCodec.ConvertMText(item.Text, from, Target, Target == VietEncoding.Unicode ? TargetFont : null, item.UpperCaseFont)
            : VietFontCodec.Convert(item.Text, from, Target, item.UpperCaseFont);
        return result == item.Text ? null : result;
    }

    private void Recompute()
    {
        Preview.Clear();
        _changeCount = 0;
        if (!_stale)
        {
            foreach (var item in _items)
            {
                var after = Convert(item);
                if (after == null) continue;
                _changeCount++;
                if (Preview.Count < PreviewLimit) Preview.Add(new FontPreviewRow(item.Kind, item.Text, after));
            }
        }

        _detectedText = Detected();
        Raise(nameof(ChangeCount));
        Raise(nameof(DetectedText));
        Raise(nameof(SummaryText));
        Raise(nameof(CanApply));
    }

    private string Detected()
    {
        var lines = new List<string>();
        foreach (var kind in _items.Where(i => i.Text.Any(c => c >= 0x80)).GroupBy(i => i.Kind))
        {
            var counts = kind.GroupBy(i => Source ?? VietFontCodec.Detect(i.Text, _preferred)).ToDictionary(g => g.Key, g => g.Count());
            var parts = new[] { (VietEncoding.Tcvn3, "TCVN3"), (VietEncoding.Vni, "VNI"), (VietEncoding.Unicode, "Unicode") }
                .Where(p => counts.ContainsKey(p.Item1))
                .Select(p => p.Item2 + " " + counts[p.Item1].ToString(CultureInfo.InvariantCulture));
            lines.Add(kind.Key + ": " + string.Join(", ", parts));
        }

        return lines.Count == 0 ? "Không có chữ tiếng Việt" : string.Join("\n", lines);
    }

    private void SetScope(ref bool field, bool value, string name)
    {
        if (field == value) return;
        field = value;
        Raise(name);
        if (_stale) return;
        _stale = true;
        Raise(nameof(IsStale));
        Recompute();
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
