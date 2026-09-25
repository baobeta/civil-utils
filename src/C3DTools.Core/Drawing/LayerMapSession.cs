using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using C3DTools.Core.Presets;

namespace C3DTools.Core.Drawing;

/// <summary>One row of the CTLAYER grid: a drawing layer and where its objects go (Target empty: stay).</summary>
public sealed class LayerMapRow : INotifyPropertyChanged
{
    private readonly Func<string, LayerMapRule> _presetTarget;
    private string _target = "", _colorText = "7", _linetype = "Continuous";
    private bool _includeBlocks;

    internal LayerMapRow(LayerUsage usage, LayerMapRule rule, Func<string, LayerMapRule> presetTarget)
    {
        Name = usage.Name;
        Count = usage.Count;
        BlockCount = usage.BlockCount;
        _presetTarget = presetTarget;
        if (rule != null)
        {
            _target = rule.Layer.Trim();
            TakeStyle(rule);
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public string Name { get; }

    /// <summary>Objects in model space and the layouts.</summary>
    public int Count { get; }

    /// <summary>Objects inside block definitions.</summary>
    public int BlockCount { get; }

    /// <summary>What moves: Count, plus BlockCount when "Áp dụng cho block" is on.</summary>
    public int ObjectCount => Count + (_includeBlocks ? BlockCount : 0);

    public string Target
    {
        get => _target;
        set
        {
            value ??= "";
            if (_target == value) return;
            _target = value;
            var rule = _presetTarget?.Invoke(value.Trim());
            if (rule != null) TakeStyle(rule);
            Raise(nameof(Target));
            Raise(nameof(IsTargetValid));
            Raise(nameof(IsValid));
            Raise(nameof(WillMove));
        }
    }

    /// <summary>ACI colour (1–255) of the target layer when it has to be created.</summary>
    public string ColorText
    {
        get => _colorText;
        set
        {
            value ??= "";
            if (_colorText == value) return;
            _colorText = value;
            Raise(nameof(ColorText));
            Raise(nameof(IsColorValid));
            Raise(nameof(IsValid));
        }
    }

    /// <summary>Linetype of the target layer when it has to be created (Continuous if the drawing lacks it).</summary>
    public string Linetype
    {
        get => _linetype;
        set
        {
            value ??= "";
            if (_linetype == value) return;
            _linetype = value;
            Raise(nameof(Linetype));
        }
    }

    public short Color => TryColor(_colorText, out var c) ? c : (short)7;

    public bool IsTargetValid => _target.Trim().Length == 0 || LayerMapSession.IsValidLayerName(_target);
    public bool IsColorValid => TryColor(_colorText, out _);
    public bool IsValid => IsTargetValid && IsColorValid;

    public bool WillMove
    {
        get
        {
            var target = _target.Trim();
            return target.Length > 0 && !string.Equals(target, Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal bool IncludeBlocks
    {
        set
        {
            if (_includeBlocks == value) return;
            _includeBlocks = value;
            Raise(nameof(ObjectCount));
        }
    }

    private void TakeStyle(LayerMapRule rule)
    {
        ColorText = rule.Color.ToString(CultureInfo.InvariantCulture);
        Linetype = LayerMapSession.NormalizeLinetype(rule.Linetype);
    }

    private static bool TryColor(string text, out short color)
    {
        color = 0;
        return short.TryParse((text ?? "").Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out color) && color >= 1 && color <= 255;
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>State of the CTLAYER dialog: the drawing's layers, their targets from the preset LayerMap, and the options.</summary>
public sealed class LayerMapSession : INotifyPropertyChanged
{
    private static readonly char[] InvalidNameChars = { '<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', ',', '=', '`' };

    private readonly List<string> _warnings = new List<string>();
    private List<LayerMapRule> _rules;
    private bool _applyToBlocks, _purgeEmpty;
    private LayerMapRow _selectedRow;

    public LayerMapSession(IEnumerable<LayerMapRule> rules)
    {
        _rules = Usable(rules);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>Problems found in the preset rules (shown on the command line).</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    public ObservableCollection<LayerMapRow> Rows { get; } = new ObservableCollection<LayerMapRow>();

    /// <summary>The preset's target layers, for the grid's combo.</summary>
    public IReadOnlyList<string> TargetNames =>
        _rules.Select(r => r.Layer.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>"Áp dụng cho block": also objects inside block definitions (not layouts, xrefs or anonymous blocks).</summary>
    public bool ApplyToBlocks
    {
        get => _applyToBlocks;
        set
        {
            if (_applyToBlocks == value) return;
            _applyToBlocks = value;
            foreach (var row in Rows) row.IncludeBlocks = value;
            Raise(nameof(ApplyToBlocks));
            Changed();
        }
    }

    /// <summary>"Xoá layer rỗng": purge unused layers afterwards (never 0, Defpoints or the current layer).</summary>
    public bool PurgeEmpty
    {
        get => _purgeEmpty;
        set
        {
            if (_purgeEmpty == value) return;
            _purgeEmpty = value;
            Raise(nameof(PurgeEmpty));
            Changed();
        }
    }

    public LayerMapRow SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (_selectedRow == value) return;
            _selectedRow = value;
            Raise(nameof(SelectedRow));
        }
    }

    public bool IsValid => Rows.All(r => r.IsValid);

    public bool CanApply => IsValid && (_purgeEmpty || Rows.Any(r => r.WillMove));

    public string SummaryText
    {
        get
        {
            if (!IsValid) return "Tên layer hoặc màu không hợp lệ (ô tô đỏ)";
            var moving = Rows.Where(r => r.WillMove).ToList();
            var text = moving.Count == 0
                ? "Không có layer nào cần chuyển"
                : moving.Count.ToString(CultureInfo.InvariantCulture) + " layer sẽ chuyển ("
                  + moving.Sum(r => r.ObjectCount).ToString(CultureInfo.InvariantCulture) + " đối tượng)";
            return _purgeEmpty ? text + "; xoá layer rỗng" : text;
        }
    }

    /// <summary>AutoCAD symbol name rules: not empty, none of &lt; &gt; / \ " : ; ? * | , = `.</summary>
    public static bool IsValidLayerName(string name)
    {
        var n = (name ?? "").Trim();
        return n.Length > 0 && n.Length <= 255 && n.IndexOfAny(InvalidNameChars) < 0;
    }

    /// <summary>One row per layer; targets, colours and linetypes from the first matching preset rule.</summary>
    public void Load(IEnumerable<LayerUsage> layers)
    {
        foreach (var row in Rows) row.PropertyChanged -= OnRowChanged;
        Rows.Clear();
        SelectedRow = null;
        foreach (var usage in layers ?? Enumerable.Empty<LayerUsage>())
        {
            var row = new LayerMapRow(usage, LayerMapper.FirstMatch(_rules, usage.Name), PresetTarget) { IncludeBlocks = _applyToBlocks };
            row.PropertyChanged += OnRowChanged;
            Rows.Add(row);
        }

        Changed();
    }

    /// <summary>Selects the row of this layer; false (selection unchanged) when there is none.</summary>
    public bool Select(string layerName)
    {
        var row = Rows.FirstOrDefault(r => string.Equals(r.Name, layerName, StringComparison.OrdinalIgnoreCase));
        if (row == null) return false;
        SelectedRow = row;
        return true;
    }

    /// <summary>What Áp dụng does: every row whose target differs from its layer.</summary>
    public List<LayerMove> Moves() =>
        Rows.Where(r => r.WillMove)
            .Select(r => new LayerMove(r.Name, r.Target.Trim(), r.Color, r.Linetype, r.ObjectCount))
            .ToList();

    /// <summary>
    /// "Lưu vào preset": an exact-name rule for each row the preset does not already give (edited or cleared rows),
    /// first so they win, followed by the preset rules (an old exact rule for the same layer is dropped).
    /// </summary>
    public List<LayerMapRule> MergedRules()
    {
        var own = new List<LayerMapRule>();
        foreach (var row in Rows)
        {
            var rule = LayerMapper.FirstMatch(_rules, row.Name);
            var target = row.Target.Trim();
            if (target.Length == 0)
            {
                if (rule == null || string.Equals(rule.Layer.Trim(), row.Name, StringComparison.OrdinalIgnoreCase)) continue;
                target = row.Name;
            }
            else if (rule != null && string.Equals(rule.Layer.Trim(), target, StringComparison.OrdinalIgnoreCase)
                     && rule.Color == row.Color && string.Equals(NormalizeLinetype(rule.Linetype), NormalizeLinetype(row.Linetype), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            own.Add(new LayerMapRule { Pattern = row.Name, Layer = target, Color = row.Color, Linetype = row.Linetype });
        }

        var result = new List<LayerMapRule>(own);
        result.AddRange(_rules.Where(r => !own.Any(o => string.Equals(o.Pattern, r.Pattern.Trim(), StringComparison.OrdinalIgnoreCase))));
        return result;
    }

    /// <summary>After saving: the saved rules become the preset the rows are compared with.</summary>
    public void UseRules(IEnumerable<LayerMapRule> rules)
    {
        _rules = Usable(rules);
        Raise(nameof(TargetNames));
    }

    /// <summary>Empty or missing linetype means Continuous.</summary>
    public static string NormalizeLinetype(string linetype) =>
        string.IsNullOrWhiteSpace(linetype) ? "Continuous" : linetype.Trim();

    /// <summary>Rules with a pattern and a target; a colour outside 1–255 becomes 7 with a warning.</summary>
    private List<LayerMapRule> Usable(IEnumerable<LayerMapRule> rules)
    {
        var result = new List<LayerMapRule>();
        foreach (var r in rules ?? Enumerable.Empty<LayerMapRule>())
        {
            if (r == null || string.IsNullOrWhiteSpace(r.Pattern) || string.IsNullOrWhiteSpace(r.Layer)) continue;
            if (r.Color >= 1 && r.Color <= 255)
            {
                result.Add(r);
                continue;
            }

            _warnings.Add($"Preset: màu {r.Color.ToString(CultureInfo.InvariantCulture)} của quy tắc {r.Pattern} → {r.Layer} không hợp lệ (1–255); dùng màu 7.");
            result.Add(new LayerMapRule { Pattern = r.Pattern, Layer = r.Layer, Color = 7, Linetype = r.Linetype });
        }

        return result;
    }

    private LayerMapRule PresetTarget(string layer) =>
        _rules.FirstOrDefault(r => string.Equals(r.Layer.Trim(), layer, StringComparison.OrdinalIgnoreCase));

    private void OnRowChanged(object sender, PropertyChangedEventArgs e) => Changed();

    private void Changed()
    {
        Raise(nameof(IsValid));
        Raise(nameof(CanApply));
        Raise(nameof(SummaryText));
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
