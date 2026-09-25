using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using C3DTools.Core.Curves;
using C3DTools.Core.Presets;
using C3DTools.Core.Stations;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Profiles;

public enum ProfileStationMode
{
    /// <summary>Interval stakes plus the alignment's curve stakes (NĐ/TĐ/P/TC/NC), as CTTOADO.</summary>
    AllStakes,

    /// <summary>Interval stakes only.</summary>
    Interval,

    /// <summary>The stations of the alignment's sample lines.</summary>
    SampleLines,
}

/// <summary>One line of the rows checklist: include it, its label and its decimals.</summary>
public sealed class ProfileTableRowOption : INotifyPropertyChanged
{
    private bool _include;
    private string _decimalsText;

    public ProfileTableRowOption(string key, string label, int decimals, bool include)
    {
        Key = key;
        Label = label ?? "";
        _decimalsText = decimals.ToString(CultureInfo.InvariantCulture);
        _include = include;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public string Key { get; }
    public string Label { get; }

    public bool Include
    {
        get => _include;
        set
        {
            if (_include == value) return;
            _include = value;
            Raise(nameof(Include));
        }
    }

    /// <summary>0–6.</summary>
    public string DecimalsText
    {
        get => _decimalsText;
        set
        {
            value ??= "";
            if (_decimalsText == value) return;
            _decimalsText = value;
            Raise(nameof(DecimalsText));
            Raise(nameof(IsDecimalsValid));
        }
    }

    public bool IsDecimalsValid => Decimals >= 0;

    /// <summary>-1 while DecimalsText is not a whole number 0–6.</summary>
    public int Decimals =>
        int.TryParse(_decimalsText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var d) && d <= 6 ? d : -1;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// State of the CTTRACDOC dialog: the profile view, surface and design profile, which stations, the rows (order,
/// on/off, decimals), text height, outputs Bảng / CSV / Excel, and the last built table.
/// </summary>
public sealed class ProfileTableSession : INotifyPropertyChanged
{
    public const string NoProfile = "(Không có)";

    private readonly ProfileTableOptions _options;
    private readonly int _stationDecimals;
    private readonly List<string> _presetMessages = new List<string>();
    private string _sourceText, _intervalText = "20", _textHeightText;
    private double _start, _end;
    private bool _hasSource, _writeTable = true, _writeCsv, _writeXlsx, _stale = true;
    private ProfileStationMode _mode = ProfileStationMode.AllStakes;
    private List<string> _surfaceNames = new List<string> { NoProfile }, _designNames = new List<string> { NoProfile };
    private int _surfaceIndex, _designIndex, _selectedRow = -1;

    public ProfileTableSession(ProjectPreset preset)
    {
        _options = preset?.ProfileTable ?? new ProfileTableOptions();
        _stationDecimals = preset?.StationDecimals ?? 2;
        _textHeightText = NumberFormat.Trimmed(_options.TextHeight > 0 ? _options.TextHeight : 2.5, 3);

        var used = new HashSet<string>();
        foreach (var spec in _options.Rows ?? new List<TableRowSpec>())
        {
            if (spec == null) continue;
            var key = ProfileTableBuilder.Canonical(spec.Key);
            if (key == null)
            {
                _presetMessages.Add($"Preset: bỏ qua dòng \"{spec.Key}\" ({spec.Label}) của bảng trắc dọc, khoá chưa hỗ trợ.");
                continue;
            }

            if (!used.Add(key)) continue;
            AddRow(new ProfileTableRowOption(key, string.IsNullOrEmpty(spec.Label) ? key : spec.Label, Math.Max(0, Math.Min(6, spec.Decimals)), true));
        }

        foreach (var known in ProfileTableBuilder.KnownRows().Where(k => !used.Contains(k.Key)))
            AddRow(new ProfileTableRowOption(known.Key, known.Label, known.Decimals, false));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public string SourceText => _hasSource ? _sourceText : "chưa chọn";
    public bool HasSource => _hasSource;
    public double StartStation => _start;
    public double EndStation => _end;

    /// <summary>Preset rows that were left out.</summary>
    public IReadOnlyList<string> PresetMessages => _presetMessages;

    public IReadOnlyList<string> SurfaceProfileNames => _surfaceNames;
    public IReadOnlyList<string> DesignProfileNames => _designNames;

    public int SurfaceProfileIndex
    {
        get => _surfaceIndex;
        set => SetIndex(ref _surfaceIndex, value, _surfaceNames.Count, nameof(SurfaceProfileIndex), nameof(SurfaceProfile));
    }

    public int DesignProfileIndex
    {
        get => _designIndex;
        set => SetIndex(ref _designIndex, value, _designNames.Count, nameof(DesignProfileIndex), nameof(DesignProfile));
    }

    /// <summary>Null for none.</summary>
    public string SurfaceProfile => _surfaceIndex > 0 ? _surfaceNames[_surfaceIndex] : null;

    /// <summary>Null for none.</summary>
    public string DesignProfile => _designIndex > 0 ? _designNames[_designIndex] : null;

    public ProfileStationMode StationMode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            Raise(nameof(StationMode));
            Raise(nameof(IsAllStakes));
            Raise(nameof(IsInterval));
            Raise(nameof(IsSampleLines));
            Raise(nameof(IsIntervalValid));
            Stale();
        }
    }

    // Radio buttons: setting one to true picks its mode.
    public bool IsAllStakes { get => _mode == ProfileStationMode.AllStakes; set { if (value) StationMode = ProfileStationMode.AllStakes; } }
    public bool IsInterval { get => _mode == ProfileStationMode.Interval; set { if (value) StationMode = ProfileStationMode.Interval; } }
    public bool IsSampleLines { get => _mode == ProfileStationMode.SampleLines; set { if (value) StationMode = ProfileStationMode.SampleLines; } }

    /// <summary>"Khoảng cách cọc" in metres (all stakes / interval); "12,5" and "12.5" both accepted.</summary>
    public string IntervalText
    {
        get => _intervalText;
        set
        {
            value ??= "";
            if (_intervalText == value) return;
            _intervalText = value;
            Raise(nameof(IntervalText));
            Raise(nameof(IsIntervalValid));
            Stale();
        }
    }

    /// <summary>NaN while IntervalText is not a number &gt; 0.</summary>
    public double Interval => NumberInput.TryParse(_intervalText, out var v) && v > 0 ? v : double.NaN;

    /// <summary>Always true for sample lines, which do not use the interval.</summary>
    public bool IsIntervalValid => _mode == ProfileStationMode.SampleLines || !double.IsNaN(Interval);

    public string TextHeightText
    {
        get => _textHeightText;
        set
        {
            value ??= "";
            if (_textHeightText == value) return;
            _textHeightText = value;
            Raise(nameof(TextHeightText));
            Raise(nameof(IsTextHeightValid));
            Changed();
        }
    }

    /// <summary>NaN while TextHeightText is not a number &gt; 0.</summary>
    public double TextHeight => NumberInput.TryParse(_textHeightText, out var v) && v > 0 ? v : double.NaN;
    public bool IsTextHeightValid => !double.IsNaN(TextHeight);

    public double RowHeight => _options.RowHeight > 0 ? _options.RowHeight : 8;
    public bool RotateStationText => _options.RotateStationText;

    /// <summary>The checklist, top to bottom.</summary>
    public ObservableCollection<ProfileTableRowOption> Rows { get; } = new ObservableCollection<ProfileTableRowOption>();

    /// <summary>The row the Lên/Xuống buttons move; -1 for none.</summary>
    public int SelectedRowIndex
    {
        get => _selectedRow;
        set
        {
            if (value < -1 || value >= Rows.Count || value == _selectedRow) return;
            _selectedRow = value;
            Raise(nameof(SelectedRowIndex));
        }
    }

    /// <summary>"Bảng": lines and text under the profile view.</summary>
    public bool WriteTable { get => _writeTable; set => SetOutput(ref _writeTable, value, nameof(WriteTable)); }

    /// <summary>"CSV": &lt;drawing&gt;_TRACDOC.csv.</summary>
    public bool WriteCsv { get => _writeCsv; set => SetOutput(ref _writeCsv, value, nameof(WriteCsv)); }

    /// <summary>"Excel": &lt;drawing&gt;_TRACDOC.xlsx.</summary>
    public bool WriteXlsx { get => _writeXlsx; set => SetOutput(ref _writeXlsx, value, nameof(WriteXlsx)); }

    public bool IsStale => _stale;

    /// <summary>The last built table (null before the first preview).</summary>
    public ProfileTableModel Model { get; private set; }

    public bool RowsValid => Rows.Any(r => r.Include) && Rows.Where(r => r.Include).All(r => r.IsDecimalsValid);

    public bool CanApply => _hasSource && IsIntervalValid && IsTextHeightValid && RowsValid
                            && (SurfaceProfile != null || DesignProfile != null) && (_writeTable || _writeCsv || _writeXlsx);

    public string SummaryText
    {
        get
        {
            if (!_hasSource) return "Chưa chọn trắc dọc";
            if (SurfaceProfile == null && DesignProfile == null) return "Chọn trắc dọc tự nhiên hoặc thiết kế";
            if (!IsIntervalValid) return "Khoảng cách cọc phải là số lớn hơn 0";
            if (!IsTextHeightValid) return "Chiều cao chữ phải là số lớn hơn 0";
            if (!Rows.Any(r => r.Include)) return "Chọn ít nhất một dòng";
            if (!RowsValid) return "Số lẻ phải là số nguyên từ 0 đến 6";
            if (!(_writeTable || _writeCsv || _writeXlsx)) return "Chọn ít nhất một đầu ra";
            if (_stale || Model == null) return "Bấm Xem trước để cập nhật bảng";
            return Model.Stations.Count.ToString(CultureInfo.InvariantCulture) + " cọc, "
                + Model.Rows.Count.ToString(CultureInfo.InvariantCulture) + " dòng";
        }
    }

    /// <summary>The picked profile view and its station range.</summary>
    public void SetSource(string description, double start, double end)
    {
        _sourceText = description ?? "";
        _start = start;
        _end = end;
        _hasSource = true;
        Raise(nameof(SourceText));
        Raise(nameof(HasSource));
        Stale(force: true);
    }

    /// <summary>
    /// The alignment's profiles: surface (EG) and design names. Each combo keeps its choice when still there, else
    /// takes the remembered one, else the first name.
    /// </summary>
    public void SetProfiles(IEnumerable<string> surfaceNames, IEnumerable<string> designNames, string preferredSurface = null, string preferredDesign = null)
    {
        _surfaceNames = Names(surfaceNames);
        _designNames = Names(designNames);
        _surfaceIndex = Choose(_surfaceNames, SurfaceProfileOr(preferredSurface));
        _designIndex = Choose(_designNames, DesignProfileOr(preferredDesign));
        Raise(nameof(SurfaceProfileNames));
        Raise(nameof(DesignProfileNames));
        Raise(nameof(SurfaceProfileIndex));
        Raise(nameof(DesignProfileIndex));
        Raise(nameof(SurfaceProfile));
        Raise(nameof(DesignProfile));
        Stale(force: true);
    }

    /// <summary>Moves the row up (-1) or down (+1); the selection follows it.</summary>
    public void MoveRow(int index, int direction)
    {
        var target = index + Math.Sign(direction);
        if (index < 0 || index >= Rows.Count || target < 0 || target >= Rows.Count) return;
        Rows.Move(index, target);
        _selectedRow = target;
        Raise(nameof(SelectedRowIndex));
        Stale();
    }

    /// <summary>The included rows, in order, with their decimals.</summary>
    public List<TableRowSpec> RowSpecs() =>
        Rows.Where(r => r.Include && r.IsDecimalsValid).Select(r => new TableRowSpec(r.Key, r.Label, r.Decimals)).ToList();

    /// <summary>The stations for the current mode. curveStakes: NĐ/TĐ/P/TC/NC (all stakes); sampleLines: named sample line stations.</summary>
    public List<StakeStation> Stations(IEnumerable<StakeStation> curveStakes, IEnumerable<StakeStation> sampleLines)
    {
        if (!_hasSource) throw new InvalidOperationException("Chưa chọn trắc dọc.");
        if (_mode == ProfileStationMode.SampleLines)
        {
            var list = new List<StakeStation>();
            foreach (var s in (sampleLines ?? Enumerable.Empty<StakeStation>()).Where(s => s != null
                         && s.Station >= _start - StakeStationList.Tolerance && s.Station <= _end + StakeStationList.Tolerance).OrderBy(s => s.Station))
            {
                if (list.Count > 0 && s.Station - list[list.Count - 1].Station <= StakeStationList.Tolerance) continue;
                list.Add(s);
            }

            return list;
        }

        return StakeStationList.Build(_start, _end, Interval, _mode == ProfileStationMode.AllStakes ? curveStakes : null, null);
    }

    /// <summary>Builds the table from what the host read at the stations; the host draws or exports Model.</summary>
    public ProfileTableModel Build(IReadOnlyList<StakeStation> stations, IReadOnlyList<double?> ground, IReadOnlyList<double?> design,
        IEnumerable<ProfileSegment> designSegments)
    {
        Model = ProfileTableBuilder.Build(stations, ground, design, designSegments, RowSpecs(), _stationDecimals);
        _stale = false;
        Raise(nameof(Model));
        Raise(nameof(IsStale));
        Changed();
        return Model;
    }

    private void AddRow(ProfileTableRowOption row)
    {
        row.PropertyChanged += (s, e) => Stale();
        Rows.Add(row);
    }

    private string SurfaceProfileOr(string preferred) => SurfaceProfile ?? preferred;
    private string DesignProfileOr(string preferred) => DesignProfile ?? preferred;

    private static List<string> Names(IEnumerable<string> names)
    {
        var list = new List<string> { NoProfile };
        list.AddRange((names ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n)).Distinct());
        return list;
    }

    private static int Choose(List<string> names, string wanted)
    {
        var index = string.IsNullOrEmpty(wanted) ? -1 : names.IndexOf(wanted);
        if (index > 0) return index;
        return names.Count > 1 ? 1 : 0;
    }

    private void SetIndex(ref int field, int value, int count, string name, string valueName)
    {
        if (value < 0 || value >= count || value == field) return;
        field = value;
        Raise(name);
        Raise(valueName);
        Stale();
    }

    private void SetOutput(ref bool field, bool value, string name)
    {
        if (field == value) return;
        field = value;
        Raise(name);
        Changed();
    }

    private void Stale(bool force = false)
    {
        if (!_stale || force)
        {
            _stale = true;
            Raise(nameof(IsStale));
        }

        Raise(nameof(RowsValid));
        Changed();
    }

    private void Changed()
    {
        Raise(nameof(CanApply));
        Raise(nameof(SummaryText));
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
