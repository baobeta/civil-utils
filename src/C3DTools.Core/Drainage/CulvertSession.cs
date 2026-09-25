using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using C3DTools.Core.Presets;
using C3DTools.Core.Stations;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Drainage;

/// <summary>A pipe network of the drawing in the CTBANGCONG checklist.</summary>
public sealed class CulvertNetworkItem : INotifyPropertyChanged
{
    private bool _isChecked;

    public CulvertNetworkItem(string name, bool isChecked)
    {
        Name = name ?? "";
        _isChecked = isChecked;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public string Name { get; }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }
}

/// <summary>A line of the CTBANGCONG grid: Tên, Loại and Ghi chú are editable, the rest is what the drawing gave.</summary>
public sealed class CulvertRow : INotifyPropertyChanged
{
    private readonly CulvertSession _session;
    private string _autoName = "", _warning = "";
    private int _number;

    internal CulvertRow(CulvertSession session, CulvertRecord record)
    {
        _session = session;
        Record = record;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>What the table is built from; edits go straight into it.</summary>
    public CulvertRecord Record { get; }

    public int Number => _number;

    /// <summary>The printed name. Setting it to "" (or to the automatic name) goes back to automatic numbering.</summary>
    public string Label
    {
        get => Record.Name.Length > 0 ? Record.Name : _autoName;
        set
        {
            var name = (value ?? "").Trim();
            if (name == _autoName && Record.Name.Length == 0) return;
            if (name == Record.Name) return;
            Record.Name = name;
            _session.Edited(this, CulvertSession.EditName, name);
        }
    }

    public string Kind
    {
        get => Record.Kind;
        set
        {
            value = (value ?? "").Trim();
            if (value == Record.Kind) return;
            Record.Kind = value;
            Raise(nameof(Kind));
            _session.Edited(this, CulvertSession.EditKind, value);
        }
    }

    public string Note
    {
        get => Record.Note;
        set
        {
            value ??= "";
            if (value == Record.Note) return;
            Record.Note = value;
            Raise(nameof(Note));
            _session.Edited(this, CulvertSession.EditNote, value);
        }
    }

    public string PipeName => Record.PipeName;
    public string NetworkName => Record.NetworkName;
    public string Station => StationFormatter.Format(Record.Station, _session.StationDecimals);
    public string Side => Record.SideText;
    public string Skew => double.IsNaN(Record.SkewDeg) ? "" : NumberFormat.Fixed(Record.SkewDeg, 1) + "°";
    public string Size => Record.SizeText;
    public string Length => CulvertTableBuilder.Number(Record.Length, _session.Options.LengthDecimals);
    public string InvertUpstream => CulvertTableBuilder.Number(Record.InvertUpstream, _session.Options.ElevationDecimals);
    public string InvertDownstream => CulvertTableBuilder.Number(Record.InvertDownstream, _session.Options.ElevationDecimals);
    public string Slope => CulvertTableBuilder.Number(Record.SlopePercent, _session.Options.SlopeDecimals);
    public string Cover => Record.CoverMin.HasValue ? CulvertTableBuilder.Number(Record.CoverMin.Value, _session.Options.ElevationDecimals) : "";
    public string Warning => _warning;
    public bool HasWarning => _warning.Length > 0;

    internal void Update(int number, string autoName, string warning)
    {
        var label = Label;
        var hadWarning = _warning;
        _number = number;
        _autoName = autoName;
        _warning = warning ?? "";
        Raise(nameof(Number));
        if (Label != label) Raise(nameof(Label));
        if (_warning != hadWarning)
        {
            Raise(nameof(Warning));
            Raise(nameof(HasWarning));
        }
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>State of the CTBANGCONG dialog: alignment, networks, surface for ground, outputs and the editable schedule.</summary>
public sealed class CulvertSession : INotifyPropertyChanged
{
    public const string NoSurface = "(Không dùng mặt phủ)";

    internal const int EditName = 0, EditKind = 1, EditNote = 2;

    private readonly ProjectPreset _preset;
    private readonly Dictionary<string, string>[] _edits =
    {
        new Dictionary<string, string>(), new Dictionary<string, string>(), new Dictionary<string, string>(),
    };

    private string _alignmentText;
    private bool _hasAlignment, _stale = true, _writeTable = true, _writeCsv, _writeXlsx, _renumbering;
    private int _surfaceIndex;
    private List<string> _surfaceNames = new List<string> { NoSurface };
    private List<string> _kindNames = new List<string>(Culverts.Kinds);

    public CulvertSession(ProjectPreset preset)
    {
        _preset = preset ?? new ProjectPreset();
        Options = _preset.Culvert ?? new CulvertOptions();
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public CulvertOptions Options { get; }
    public int StationDecimals => _preset.StationDecimals;

    /// <summary>The preset has PipeRules: the Cảnh báo column is filled.</summary>
    public bool HasRules => _preset.PipeRules != null;

    public string AlignmentText => _hasAlignment ? _alignmentText : "chưa chọn";
    public bool HasAlignment => _hasAlignment;

    public ObservableCollection<CulvertNetworkItem> Networks { get; } = new ObservableCollection<CulvertNetworkItem>();

    public IReadOnlyList<string> SelectedNetworks => Networks.Where(n => n.IsChecked).Select(n => n.Name).ToList();

    /// <summary>NoSurface first, then the drawing's surfaces. Ground at an end without a structure comes from the chosen one.</summary>
    public IReadOnlyList<string> SurfaceNames => _surfaceNames;

    public int SurfaceIndex
    {
        get => _surfaceIndex;
        set
        {
            if (value < 0 || value >= _surfaceNames.Count || value == _surfaceIndex) return;
            _surfaceIndex = value;
            Raise(nameof(SurfaceIndex));
            Raise(nameof(Surface));
            Stale();
        }
    }

    public string Surface => _surfaceIndex > 0 ? _surfaceNames[_surfaceIndex] : null;

    /// <summary>The choices of the Loại column: the standard kinds plus any kind read or typed.</summary>
    public IReadOnlyList<string> KindNames => _kindNames;

    /// <summary>"Bảng": AutoCAD Table at a picked point.</summary>
    public bool WriteTable { get => _writeTable; set => SetOutput(ref _writeTable, value, nameof(WriteTable)); }

    /// <summary>"CSV": &lt;drawing&gt;_BANGCONG.csv.</summary>
    public bool WriteCsv { get => _writeCsv; set => SetOutput(ref _writeCsv, value, nameof(WriteCsv)); }

    /// <summary>"Excel": &lt;drawing&gt;_BANGCONG.xlsx.</summary>
    public bool WriteXlsx { get => _writeXlsx; set => SetOutput(ref _writeXlsx, value, nameof(WriteXlsx)); }

    /// <summary>The alignment, networks or surface changed since the last SetRecords.</summary>
    public bool IsStale => _stale;

    public ObservableCollection<CulvertRow> Rows { get; } = new ObservableCollection<CulvertRow>();

    public int WarningCount => Rows.Count(r => r.HasWarning);

    public bool CanApply => _hasAlignment && Networks.Any(n => n.IsChecked) && (_writeTable || _writeCsv || _writeXlsx);

    public string SummaryText
    {
        get
        {
            if (!_hasAlignment) return "Chưa chọn tuyến";
            if (Networks.Count == 0) return "Bản vẽ không có mạng cống";
            if (!Networks.Any(n => n.IsChecked)) return "Chọn ít nhất một mạng cống";
            if (!(_writeTable || _writeCsv || _writeXlsx)) return "Chọn ít nhất một đầu ra";
            if (_stale) return "Bấm Xem trước để cập nhật bảng";
            var text = Rows.Count.ToString(CultureInfo.InvariantCulture) + " cống";
            var warnings = WarningCount;
            if (warnings > 0) text += ", " + warnings.ToString(CultureInfo.InvariantCulture) + " cống có cảnh báo";
            return text;
        }
    }

    public void SetAlignment(string description)
    {
        _alignmentText = description ?? "";
        _hasAlignment = true;
        Raise(nameof(AlignmentText));
        Raise(nameof(HasAlignment));
        Stale();
    }

    /// <summary>The drawing's networks; those in checkedNames are ticked, or all when none of checkedNames is there.</summary>
    public void SetNetworks(IEnumerable<string> names, IEnumerable<string> checkedNames)
    {
        foreach (var n in Networks) n.PropertyChanged -= OnNetworkChanged;
        Networks.Clear();
        var list = (names ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
        var wanted = new HashSet<string>(checkedNames ?? Enumerable.Empty<string>());
        var any = list.Any(wanted.Contains);
        foreach (var name in list)
        {
            var item = new CulvertNetworkItem(name, !any || wanted.Contains(name));
            item.PropertyChanged += OnNetworkChanged;
            Networks.Add(item);
        }

        Raise(nameof(SelectedNetworks));
        Stale();
    }

    /// <summary>The drawing's surfaces; remembered is chosen when it is there.</summary>
    public void SetSurfaces(IEnumerable<string> names, string remembered)
    {
        var current = string.IsNullOrEmpty(remembered) ? Surface : remembered;
        _surfaceNames = new List<string> { NoSurface };
        _surfaceNames.AddRange((names ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n)));
        var index = current == null ? 0 : _surfaceNames.FindIndex(1, n => n == current);
        _surfaceIndex = index < 0 ? 0 : index;
        Raise(nameof(SurfaceNames));
        Raise(nameof(SurfaceIndex));
        Raise(nameof(Surface));
        Stale();
    }

    /// <summary>The culverts read from the drawing: sorted, named, with the user's earlier edits (by Key) put back.</summary>
    public void SetRecords(IEnumerable<CulvertRecord> records)
    {
        Rows.Clear();
        foreach (var r in CulvertTableBuilder.Sorted(records))
        {
            if (string.IsNullOrEmpty(r.Note)) r.Note = CulvertTableBuilder.DefaultNote(r);
            var key = r.Key ?? "";
            if (_edits[EditName].TryGetValue(key, out var name)) r.Name = name;
            if (_edits[EditKind].TryGetValue(key, out var kind)) r.Kind = kind;
            if (_edits[EditNote].TryGetValue(key, out var note)) r.Note = note;
            r.Name ??= "";
            r.Kind ??= "";
            r.Note ??= "";
            AddKind(r.Kind);
            Rows.Add(new CulvertRow(this, r));
        }

        Renumber();
        _stale = false;
        Raise(nameof(IsStale));
        Changed();
    }

    /// <summary>The schedule of the rows as edited.</summary>
    public TableData BuildTable() => CulvertTableBuilder.Build(Rows.Select(r => r.Record), _preset);

    internal void Edited(CulvertRow row, int field, string value)
    {
        _edits[field][row.Record.Key ?? ""] = value;
        if (field == EditKind) AddKind(value);
        if (field == EditName) Renumber();
        Changed();
    }

    private void Renumber()
    {
        if (_renumbering) return;
        _renumbering = true;
        try
        {
            var records = Rows.Select(r => r.Record).ToList();
            var names = CulvertTableBuilder.Names(records);
            for (var i = 0; i < Rows.Count; i++)
                Rows[i].Update(i + 1, names[i], CulvertTableBuilder.Warning(records[i], names[i], _preset.PipeRules));
        }
        finally
        {
            _renumbering = false;
        }
    }

    private void AddKind(string kind)
    {
        if (string.IsNullOrEmpty(kind) || _kindNames.Contains(kind)) return;
        _kindNames = new List<string>(_kindNames) { kind };
        Raise(nameof(KindNames));
    }

    private void OnNetworkChanged(object sender, PropertyChangedEventArgs e)
    {
        Raise(nameof(SelectedNetworks));
        Stale();
    }

    private void SetOutput(ref bool field, bool value, string name)
    {
        if (field == value) return;
        field = value;
        Raise(name);
        Changed();
    }

    private void Stale()
    {
        if (!_stale)
        {
            _stale = true;
            Raise(nameof(IsStale));
        }

        Changed();
    }

    private void Changed()
    {
        Raise(nameof(CanApply));
        Raise(nameof(SummaryText));
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
