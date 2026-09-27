using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using C3DTools.Core.Curves;
using C3DTools.Core.Presets;

namespace C3DTools.Core.Stations;

/// <summary>A line of the CTDANHCOC preview: station, name now, name after.</summary>
public sealed class StakeRenameLine
{
    public StakeRenameLine(string station, string oldName, string newName)
    {
        Station = station;
        OldName = oldName;
        NewName = newName;
    }

    public string Station { get; }
    public string OldName { get; }
    public string NewName { get; }
    public bool Changed => !string.Equals(OldName, NewName, StringComparison.Ordinal);
}

/// <summary>
/// State of the CTDANHCOC dialog ("Đánh lại toàn bộ tên cọc"), with AND Design's options. The preview follows every
/// change at once: renaming needs nothing from the drawing.
/// </summary>
public sealed class StakeRenameSession : INotifyPropertyChanged
{
    private static readonly char[] PrefixSeparators = { ';', ',' };

    private readonly int _stationDecimals;
    private IReadOnlyList<RouteStake> _stakes = new RouteStake[0];
    private string _sourceText = "";
    private int _fromIndex, _toIndex;
    private string _keepText = "", _detailPrefix = "C", _firstDetailText = "1", _firstPiText = "1";
    private bool _writeLabels = true;
    private bool _renameCurveKeys = true, _nameByStation, _noHundreds, _continuousH = true, _restartPerKm, _noRestartFrom100 = true;

    public StakeRenameSession(ProjectPreset preset)
    {
        _stationDecimals = preset?.StationDecimals ?? 2;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public string SourceText => _stakes.Count > 0 ? _sourceText : "chưa chọn";
    public bool HasStakes => _stakes.Count > 0;

    /// <summary>"Từ cọc" / "Tới cọc" choices: "C1 (Km0+020.00)".</summary>
    public IReadOnlyList<string> StakeChoices { get; private set; } = new string[0];

    public int FromIndex { get => _fromIndex; set => SetIndex(ref _fromIndex, value, nameof(FromIndex)); }
    public int ToIndex { get => _toIndex; set => SetIndex(ref _toIndex, value, nameof(ToIndex)); }

    /// <summary>"Để lại các cọc có tiếp đầu": prefixes separated by ';'.</summary>
    public string KeepPrefixesText { get => _keepText; set => SetText(ref _keepText, value, nameof(KeepPrefixesText)); }

    /// <summary>"Đánh lại cọc cắm cong, siêu cao".</summary>
    public bool RenameCurveKeys { get => _renameCurveKeys; set => SetFlag(ref _renameCurveKeys, value, nameof(RenameCurveKeys)); }

    /// <summary>"Số thứ tự đỉnh đầu".</summary>
    public string FirstPiNumberText { get => _firstPiText; set => SetText(ref _firstPiText, value, nameof(FirstPiNumberText)); }

    /// <summary>"Tên cọc theo kiểu lý trình".</summary>
    public bool NameByStation { get => _nameByStation; set => SetFlag(ref _nameByStation, value, nameof(NameByStation)); }

    /// <summary>"Tiếp đầu của cọc".</summary>
    public string DetailPrefix { get => _detailPrefix; set => SetText(ref _detailPrefix, value, nameof(DetailPrefix)); }

    /// <summary>"Số thứ tự cọc đầu".</summary>
    public string FirstDetailNumberText { get => _firstDetailText; set => SetText(ref _firstDetailText, value, nameof(FirstDetailNumberText)); }

    /// <summary>"Không tạo cọc H".</summary>
    public bool NoHundreds { get => _noHundreds; set => SetFlag(ref _noHundreds, value, nameof(NoHundreds)); }

    /// <summary>"Cọc H liên tục": C numbers run on past H stakes.</summary>
    public bool ContinuousThroughH { get => _continuousH; set => SetFlag(ref _continuousH, value, nameof(ContinuousThroughH)); }

    /// <summary>"Thứ tự cọc quay lại theo KM".</summary>
    public bool RestartPerKm { get => _restartPerKm; set => SetFlag(ref _restartPerKm, value, nameof(RestartPerKm)); }

    /// <summary>"Không đánh số quay lại khi TT&gt;=100".</summary>
    public bool NoRestartFrom100 { get => _noRestartFrom100; set => SetFlag(ref _noRestartFrom100, value, nameof(NoRestartFrom100)); }

    public bool IsFirstPiValid => PositiveInt(_firstPiText) > 0;
    public bool IsFirstDetailValid => PositiveInt(_firstDetailText) > 0;
    public bool IsRangeValid => _stakes.Count > 0 && _fromIndex <= _toIndex;

    public ObservableCollection<StakeRenameLine> PreviewRows { get; } = new ObservableCollection<StakeRenameLine>();

    /// <summary>The unique names to write, one per stake (same order as the stakes given to SetStakes).</summary>
    public IReadOnlyList<string> NewNames { get; private set; } = new string[0];
    public int ChangedCount { get; private set; }

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

    private bool _stationOnlyAtKm = true;

    /// <summary>"Chỉ tại cọc Km": no station at C, H and curve stakes.</summary>
    public bool StationOnlyAtKm
    {
        get => _stationOnlyAtKm;
        set
        {
            if (_stationOnlyAtKm == value) return;
            _stationOnlyAtKm = value;
            Raise(nameof(StationOnlyAtKm));
        }
    }

    /// <summary>One choice instead of two dependent boxes: 0 no station, 1 at Km stakes only (default), 2 at every stake.</summary>
    public int StationModeIndex
    {
        get => !_labelStations ? 0 : _stationOnlyAtKm ? 1 : 2;
        set
        {
            if (value < 0 || value > 2 || value == StationModeIndex) return;
            _labelStations = value != 0;
            _stationOnlyAtKm = value != 2;
            Raise(nameof(StationModeIndex));
            Raise(nameof(LabelStations));
            Raise(nameof(StationOnlyAtKm));
        }
    }

    public StakeLabelOptions LabelOptions => new StakeLabelOptions
    {
        AlternateSides = _alternateSides, WithStation = _labelStations, StationOnlyAtKm = _stationOnlyAtKm, StationDecimals = _stationDecimals,
    };

    private bool _skipHundredPositions = true, _plainCurveNames;

    /// <summary>"Tên cọc cong không dấu": TD1, ND1 instead of TĐ1, NĐ1.</summary>
    public bool PlainCurveNames { get => _plainCurveNames; set => SetFlag(ref _plainCurveNames, value, nameof(PlainCurveNames)); }

    /// <summary>"Cọc C bỏ qua vị trí cọc H": an H stake takes no C number (C4, H1, C5). Off: C4, H1, C6.</summary>
    public bool SkipHundredPositions { get => _skipHundredPositions; set => SetFlag(ref _skipHundredPositions, value, nameof(SkipHundredPositions)); }

    /// <summary>"Ghi tên cọc lên bình đồ": tick, name and station text at every stake of the group.</summary>
    public bool WriteLabels
    {
        get => _writeLabels;
        set
        {
            if (_writeLabels == value) return;
            _writeLabels = value;
            Raise(nameof(WriteLabels));
            Raise(nameof(CanApply));
            Raise(nameof(SummaryText));
        }
    }

    /// <summary>With WriteLabels, applying is useful even when no name changes: the names get drawn.</summary>
    public bool CanApply => IsRangeValid && IsFirstPiValid && IsFirstDetailValid && (ChangedCount > 0 || _writeLabels);

    public string SummaryText
    {
        get
        {
            if (_stakes.Count == 0) return "Chưa chọn nhóm cọc";
            if (!IsRangeValid) return "Cọc đầu phải đứng trước cọc cuối";
            if (!IsFirstPiValid || !IsFirstDetailValid) return "Số thứ tự phải là số nguyên dương";
            if (ChangedCount > 0) return $"{ChangedCount} / {_stakes.Count} cọc đổi tên";
            return _writeLabels ? "Không có tên nào thay đổi; Áp dụng sẽ ghi tên cọc lên bình đồ" : "Không có tên nào thay đổi";
        }
    }

    /// <summary>The naming options as the dialog shows them now.</summary>
    public StakeNamingOptions Options => new StakeNamingOptions
    {
        DetailPrefix = _detailPrefix.Trim(),
        FirstDetailNumber = Math.Max(1, PositiveInt(_firstDetailText)),
        RestartPerKm = _restartPerKm,
        NoRestartFrom100 = _noRestartFrom100,
        DetailContinuousThroughH = _continuousH,
        CreateHundreds = !_noHundreds,
        CountHundredPositions = !_skipHundredPositions,
        PlainCurveNames = _plainCurveNames,
        NameByStation = _nameByStation,
        RenameCurveKeys = _renameCurveKeys,
        FirstPiNumber = Math.Max(1, PositiveInt(_firstPiText)),
        KeepPrefixes = _keepText.Split(PrefixSeparators, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 0).ToList(),
        StationDecimals = _stationDecimals,
    };

    /// <summary>The group's stakes, classified and in station order. The range becomes all of them.</summary>
    public void SetStakes(string description, IReadOnlyList<RouteStake> stakes)
    {
        _sourceText = description ?? "";
        _stakes = (stakes ?? new RouteStake[0]).OrderBy(s => s.Station).ToList();
        StakeChoices = _stakes.Select(s => (s.Name.Length > 0 ? s.Name : "?") + " (" + StationFormatter.Format(s.Station, _stationDecimals) + ")").ToList();
        _fromIndex = 0;
        _toIndex = Math.Max(0, _stakes.Count - 1);
        Raise(nameof(SourceText));
        Raise(nameof(HasStakes));
        Raise(nameof(StakeChoices));
        Raise(nameof(FromIndex));
        Raise(nameof(ToIndex));
        Recompute();
    }

    public IReadOnlyList<RouteStake> Stakes => _stakes;

    private void Recompute()
    {
        PreviewRows.Clear();
        ChangedCount = 0;
        if (IsRangeValid && IsFirstPiValid && IsFirstDetailValid)
        {
            var names = StakeNamer.Name(_stakes, Options, _fromIndex, _toIndex);
            NewNames = StakeNamer.UniqueLabels(names, _stakes.Select(s => s.Station).ToList());
            for (var i = 0; i < _stakes.Count; i++)
            {
                var line = new StakeRenameLine(StationFormatter.Format(_stakes[i].Station, _stationDecimals), _stakes[i].Name, NewNames[i]);
                if (line.Changed) ChangedCount++;
                PreviewRows.Add(line);
            }
        }
        else
        {
            NewNames = _stakes.Select(s => s.Name).ToList();
        }

        Raise(nameof(NewNames));
        Raise(nameof(ChangedCount));
        Raise(nameof(IsRangeValid));
        Raise(nameof(IsFirstPiValid));
        Raise(nameof(IsFirstDetailValid));
        Raise(nameof(CanApply));
        Raise(nameof(SummaryText));
    }

    private static int PositiveInt(string text) =>
        int.TryParse((text ?? "").Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private void SetIndex(ref int field, int value, string name)
    {
        if (value < 0 || value >= Math.Max(1, _stakes.Count) || field == value) return;
        field = value;
        Raise(name);
        Recompute();
    }

    private void SetText(ref string field, string value, string name)
    {
        value ??= "";
        if (field == value) return;
        field = value;
        Raise(name);
        Recompute();
    }

    private void SetFlag(ref bool field, bool value, string name)
    {
        if (field == value) return;
        field = value;
        Raise(name);
        Recompute();
    }

    private void Raise(string name) => C3DTools.Core.Ui.NotifyGuard.Raise(this, PropertyChanged, name);
}
