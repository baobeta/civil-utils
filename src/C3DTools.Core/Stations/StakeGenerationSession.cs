using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using C3DTools.Core.Curves;
using C3DTools.Core.Presets;

namespace C3DTools.Core.Stations;

/// <summary>A line of a stake preview grid: name, station, kind and what happens to it.</summary>
public sealed class StakePreviewLine
{
    public StakePreviewLine(string name, string station, string kind, string status)
    {
        Name = name;
        Station = station;
        Kind = kind;
        Status = status;
    }

    public string Name { get; }
    public string Station { get; }
    public string Kind { get; }
    public string Status { get; }
}

/// <summary>State of the CTPHATCOC dialog ("Phát sinh cọc"): range, spacings, insert list, sample line group and the preview.</summary>
public sealed class StakeGenerationSession : INotifyPropertyChanged
{
    public const string NewGroup = "(Nhóm mới)";
    private static readonly char[] Separators = { ';', ' ', '\t', '\r', '\n' };

    private readonly int _stationDecimals;
    private string _sourceText = "";
    private bool _hasSource, _insertMode, _subStakeStyle, _stale = true, _writeLabels = true;
    private double _start, _end;
    private string _fromText = "", _toText = "", _straightText = "20", _curveText = "10", _halfWidthText = "60", _insertText = "", _newGroupName = "";
    private string _detailStartText = "20";
    private bool _detailStartEdited;
    private List<string> _groupNames = new List<string> { NewGroup };
    private int _groupIndex;
    private List<double> _insertStations = new List<double>();
    private bool _insertValid = true;

    public StakeGenerationSession(ProjectPreset preset)
    {
        _stationDecimals = preset?.StationDecimals ?? 2;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public string SourceText => _hasSource ? _sourceText : "chưa chọn";
    public bool HasSource => _hasSource;
    public double StartStation => _start;
    public double EndStation => _end;

    /// <summary>"Chèn" (true) or "Phát sinh" (false).</summary>
    public bool InsertMode { get => _insertMode; set => SetOption(ref _insertMode, value, nameof(InsertMode), nameof(GenerateMode)); }
    public bool GenerateMode { get => !_insertMode; set => InsertMode = !value; }

    /// <summary>"Từ khoảng dồn": "Km0+000", "0+000" or "0".</summary>
    public string FromText { get => _fromText; set => SetText(ref _fromText, value, nameof(FromText), nameof(From), nameof(IsRangeValid)); }
    public string ToText { get => _toText; set => SetText(ref _toText, value, nameof(ToText), nameof(To), nameof(IsRangeValid)); }
    public double From => Snap(ParseStation(_fromText));
    public double To => Snap(ParseStation(_toText));
    public bool IsRangeValid => !double.IsNaN(From) && !double.IsNaN(To) && To > From
        && From >= _start - StakePlanner.Tolerance && To <= _end + StakePlanner.Tolerance;

    public string StraightSpacingText
    {
        get => _straightText;
        set
        {
            value ??= "";
            if (_straightText == value) return;
            _straightText = value;
            // Until the user types a start station it follows the spacing: 20 → 20, 100 → 50.
            if (!_detailStartEdited && !double.IsNaN(StraightSpacing))
                _detailStartText = Tables.NumberFormat.Trimmed(StakePlanner.DefaultDetailStart(StraightSpacing), 3);
            foreach (var n in new[] { nameof(StraightSpacingText), nameof(StraightSpacing), nameof(IsSpacingValid), nameof(DetailStartText), nameof(DetailStart), nameof(IsDetailStartValid) })
                Raise(n);
            Stale();
        }
    }

    /// <summary>"Cọc C bắt đầu từ lý trình": station of the first C stake ("Km0+020", "0+050" or metres).</summary>
    public string DetailStartText
    {
        get => _detailStartText;
        set
        {
            value ??= "";
            if (_detailStartText == value) return;
            _detailStartText = value;
            _detailStartEdited = value.Trim().Length > 0;
            if (!_detailStartEdited && !double.IsNaN(StraightSpacing))
                _detailStartText = Tables.NumberFormat.Trimmed(StakePlanner.DefaultDetailStart(StraightSpacing), 3);
            Raise(nameof(DetailStartText));
            Raise(nameof(DetailStart));
            Raise(nameof(IsDetailStartValid));
            Stale();
        }
    }

    public double DetailStart => ParseStation(_detailStartText);
    public bool IsDetailStartValid => !double.IsNaN(DetailStart) && DetailStart >= 0;
    public string CurveSpacingText { get => _curveText; set => SetText(ref _curveText, value, nameof(CurveSpacingText), nameof(CurveSpacing), nameof(IsSpacingValid)); }
    public double StraightSpacing => Positive(_straightText);
    public double CurveSpacing => Positive(_curveText);
    public bool IsSpacingValid => !double.IsNaN(StraightSpacing) && !double.IsNaN(CurveSpacing);

    /// <summary>"Bề rộng nửa dải xác định trắc ngang": sample line length each side of the alignment.</summary>
    public string HalfWidthText { get => _halfWidthText; set => SetText(ref _halfWidthText, value, nameof(HalfWidthText), nameof(HalfWidth), nameof(IsHalfWidthValid)); }
    public double HalfWidth => Positive(_halfWidthText);
    public bool IsHalfWidthValid => !double.IsNaN(HalfWidth);

    /// <summary>"Chèn": stations separated by ';', spaces or new lines.</summary>
    public string InsertStationsText
    {
        get => _insertText;
        set
        {
            value ??= "";
            if (_insertText == value) return;
            _insertText = value;
            ParseInsert();
            Raise(nameof(InsertStationsText));
            Stale();
        }
    }

    public IReadOnlyList<double> InsertStations => _insertStations;
    public bool IsInsertValid => _insertValid;

    /// <summary>"Kiểu cọc phụ": inserted stakes are named after the stake before them (C5a).</summary>
    public bool SubStakeStyle { get => _subStakeStyle; set => SetOption(ref _subStakeStyle, value, nameof(SubStakeStyle)); }

    private bool _alternateSides, _labelStations = true;

    /// <summary>"Tên cọc xen kẽ trái phải".</summary>
    public bool AlternateSides
    {
        get => _alternateSides;
        set
        {
            if (_alternateSides == value) return;
            _alternateSides = value;
            Raise(nameof(AlternateSides));
        }
    }

    /// <summary>"Ghi lý trình ở đầu kia".</summary>
    public bool LabelStations
    {
        get => _labelStations;
        set
        {
            if (_labelStations == value) return;
            _labelStations = value;
            Raise(nameof(LabelStations));
        }
    }

    public StakeLabelOptions LabelOptions =>
        new StakeLabelOptions { AlternateSides = _alternateSides, WithStation = _labelStations, StationDecimals = _stationDecimals };

    /// <summary>"Ghi tên cọc lên bình đồ": tick, name and station text at every stake.</summary>
    public bool WriteLabels
    {
        get => _writeLabels;
        set
        {
            if (_writeLabels == value) return;
            _writeLabels = value;
            Raise(nameof(WriteLabels));
        }
    }

    /// <summary>NewGroup first, then the alignment's sample line groups.</summary>
    public IReadOnlyList<string> GroupNames => _groupNames;

    public int GroupIndex
    {
        get => _groupIndex;
        set
        {
            if (value < 0 || value >= _groupNames.Count || value == _groupIndex) return;
            _groupIndex = value;
            Raise(nameof(GroupIndex));
            Raise(nameof(IsNewGroup));
            Raise(nameof(Group));
            Stale();
        }
    }

    public bool IsNewGroup => _groupIndex == 0;

    /// <summary>The existing group chosen; null for a new group.</summary>
    public string Group => _groupIndex > 0 ? _groupNames[_groupIndex] : null;

    public string NewGroupName { get => _newGroupName; set => SetText(ref _newGroupName, value, nameof(NewGroupName)); }

    public bool IsStale => _stale;
    public ObservableCollection<StakePreviewLine> PreviewRows { get; } = new ObservableCollection<StakePreviewLine>();

    /// <summary>The stakes of the last preview, in station order, with unique labels.</summary>
    public IReadOnlyList<RouteStake> Planned { get; private set; } = new RouteStake[0];
    public IReadOnlyList<string> PlannedLabels { get; private set; } = new string[0];
    public int NewCount { get; private set; }
    public int RemovedCount { get; private set; }

    public bool CanApply => _hasSource && IsSpacingValid && IsHalfWidthValid
        && (_insertMode ? _insertValid && _insertStations.Count > 0 && !IsNewGroup : IsRangeValid && IsDetailStartValid)
        && (!IsNewGroup || _newGroupName.Trim().Length > 0);

    public string SummaryText
    {
        get
        {
            if (!_hasSource) return "Chưa chọn tuyến";
            if (!_insertMode && !IsRangeValid) return "Khoảng lý trình không hợp lệ hoặc nằm ngoài tuyến";
            if (!IsSpacingValid) return "Khoảng cách cọc phải là số lớn hơn 0";
            if (!_insertMode && !IsDetailStartValid) return "Lý trình bắt đầu cọc C không hợp lệ";
            if (!IsHalfWidthValid) return "Bề rộng nửa dải phải là số lớn hơn 0";
            if (_insertMode && IsNewGroup) return "Chèn cọc cần chọn nhóm cọc (Sample Line Group) đã có";
            if (_insertMode && (!_insertValid || _insertStations.Count == 0)) return "Nhập lý trình cọc cần chèn (trong phạm vi tuyến)";
            if (IsNewGroup && _newGroupName.Trim().Length == 0) return "Nhập tên nhóm cọc mới";
            if (_stale) return "Bấm Xem trước để xem danh sách cọc";
            return $"{Planned.Count} cọc: {NewCount} mới, {RemovedCount} bỏ";
        }
    }

    /// <summary>The picked alignment, its range and its sample line groups. The range fields are reset to the whole alignment.</summary>
    public void SetSource(string description, double start, double end, IEnumerable<string> groupNames)
    {
        _sourceText = description ?? "";
        _start = start;
        _end = end;
        _hasSource = true;
        var current = Group;
        _groupNames = new List<string> { NewGroup };
        _groupNames.AddRange((groupNames ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n)));
        var index = current == null ? (_groupNames.Count > 1 ? 1 : 0) : _groupNames.IndexOf(current);
        _groupIndex = index < 0 ? 0 : index;
        _fromText = StationFormatter.Format(start, _stationDecimals);
        _toText = StationFormatter.Format(end, _stationDecimals);
        ParseInsert();
        foreach (var name in new[] { nameof(SourceText), nameof(HasSource), nameof(GroupNames), nameof(GroupIndex), nameof(IsNewGroup), nameof(Group),
                     nameof(FromText), nameof(ToText), nameof(From), nameof(To), nameof(IsRangeValid) })
            Raise(name);
        Stale();
    }

    /// <summary>"…" next to Từ/Tới khoảng dồn: a station picked on the alignment.</summary>
    public void SetFrom(double station) => FromText = StationFormatter.Format(station, _stationDecimals);
    public void SetTo(double station) => ToText = StationFormatter.Format(station, _stationDecimals);

    /// <summary>"Chỉ điểm…" in Chèn mode: adds a picked station to the list.</summary>
    public void AddInsertStation(double station)
    {
        var text = StationFormatter.Format(station, _stationDecimals);
        InsertStationsText = _insertText.Trim().Length == 0 ? text : _insertText.TrimEnd() + "; " + text;
    }

    /// <summary>
    /// The stakes after Áp dụng. existing: the group's stakes now (classified, empty for a new group); zones and keys:
    /// the alignment's curves. Generate mode renames every stake with the default rules (CTDANHCOC for other rules).
    /// </summary>
    public List<RouteStake> Plan(IReadOnlyList<RouteStake> existing, IEnumerable<StationZone> zones, IEnumerable<RouteStake> keys)
    {
        if (!_hasSource) throw new InvalidOperationException("Chưa chọn tuyến.");
        existing ??= new RouteStake[0];
        if (_insertMode) return StakePlanner.Insert(existing, _insertStations, _subStakeStyle, _stationDecimals, out _);

        var generated = StakePlanner.Generate(From, To, StraightSpacing, CurveSpacing, zones, keys, DetailStart);
        var merged = StakePlanner.Replace(existing, generated, From, To);
        var names = StakeNamer.Name(merged, new StakeNamingOptions { StationDecimals = _stationDecimals });
        return merged.Select((s, i) => s.WithName(names[i])).ToList();
    }

    /// <summary>Shows planned against existing: "mới" where no stake was, "đổi tên", "giữ".</summary>
    public void SetPreview(IReadOnlyList<RouteStake> planned, IReadOnlyList<RouteStake> existing)
    {
        planned ??= new RouteStake[0];
        existing ??= new RouteStake[0];
        Planned = planned;
        PlannedLabels = StakeNamer.UniqueLabels(planned.Select(s => s.Name).ToList(), planned.Select(s => s.Station).ToList());
        NewCount = 0;
        PreviewRows.Clear();
        for (var i = 0; i < planned.Count; i++)
        {
            var s = planned[i];
            var old = existing.FirstOrDefault(e => Math.Abs(e.Station - s.Station) <= StakePlanner.Tolerance);
            var status = old == null ? "mới" : old.Name == PlannedLabels[i] ? "giữ" : "đổi tên (" + old.Name + ")";
            if (old == null) NewCount++;
            PreviewRows.Add(new StakePreviewLine(PlannedLabels[i], StationFormatter.Format(s.Station, _stationDecimals), KindText(s), status));
        }

        RemovedCount = existing.Count(e => !planned.Any(p => Math.Abs(p.Station - e.Station) <= StakePlanner.Tolerance));
        _stale = false;
        Raise(nameof(Planned));
        Raise(nameof(IsStale));
        Changed();
    }

    public static string KindText(RouteStake s) => s.Role switch
    {
        StakeRole.Km => "Km",
        StakeRole.Hundred => "H",
        StakeRole.CurveKey => "Cọc chủ yếu",
        _ => "Chi tiết",
    };

    private void ParseInsert()
    {
        _insertStations = new List<double>();
        _insertValid = true;
        foreach (var token in _insertText.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var station = Snap(ParseStation(token));
            if (double.IsNaN(station) || (_hasSource && (station < _start - StakePlanner.Tolerance || station > _end + StakePlanner.Tolerance)))
            {
                _insertValid = false;
                continue;
            }

            _insertStations.Add(station);
        }

        Raise(nameof(InsertStations));
        Raise(nameof(IsInsertValid));
    }

    /// <summary>NaN when the text is not a station.</summary>
    public static double ParseStation(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return double.NaN;
        var t = text.Trim();
        if (t.IndexOf('+') >= 0) return StationFormatter.TryParse(t.Replace(',', '.'), out var s) ? s : double.NaN;
        return NumberInput.TryParse(t, out var v) ? v : double.NaN;
    }

    /// <summary>
    /// The range fields show stations rounded to the preset decimals, so "Km4+561.23" can lie past an end at 4561.226.
    /// A station within half a display unit of an alignment end is that end.
    /// </summary>
    private double Snap(double station)
    {
        if (double.IsNaN(station) || !_hasSource) return station;
        var half = 0.5 * Math.Pow(10, -_stationDecimals) + 1e-9;
        if (Math.Abs(station - _end) <= half) return _end;
        return Math.Abs(station - _start) <= half ? _start : station;
    }

    private static double Positive(string text) => NumberInput.TryParse(text, out var v) && v > 0 ? v : double.NaN;

    private void SetText(ref string field, string value, params string[] names)
    {
        value ??= "";
        if (field == value) return;
        field = value;
        foreach (var n in names) Raise(n);
        Stale();
    }

    private void SetOption(ref bool field, bool value, params string[] names)
    {
        if (field == value) return;
        field = value;
        foreach (var n in names) Raise(n);
        Stale();
    }

    private void Stale()
    {
        _stale = true;
        Raise(nameof(IsStale));
        Changed();
    }

    private void Changed()
    {
        Raise(nameof(CanApply));
        Raise(nameof(SummaryText));
    }

    private void Raise(string name) => C3DTools.Core.Ui.NotifyGuard.Raise(this, PropertyChanged, name);
}
