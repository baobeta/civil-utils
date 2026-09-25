using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using C3DTools.Core.Curves;
using C3DTools.Core.Presets;
using C3DTools.Core.Profiles;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Sections;

/// <summary>What the host read for one section view: its sample line's name and station and the two chosen section lines.</summary>
public sealed class SectionInput
{
    public SectionInput(string name, double station, SectionProfile ground, SectionProfile design)
    {
        Name = name ?? "";
        Station = station;
        Ground = ground;
        Design = design;
    }

    public string Name { get; }
    public double Station { get; }

    /// <summary>Null when the view's sample line has no section of the chosen surface.</summary>
    public SectionProfile Ground { get; }

    /// <summary>Null for none.</summary>
    public SectionProfile Design { get; }
}

/// <summary>
/// State of the CTTRACNGANG dialog: the section views, which section is the ground (tự nhiên) and which the design
/// (thiết kế), the rows (order, on/off, decimals), text height, outputs Bảng / CSV / Excel, and the last built tables.
/// </summary>
public sealed class SectionTableSession : INotifyPropertyChanged
{
    public const string NoSection = "(Không có)";

    private readonly SectionTableOptions _options;
    private readonly int _stationDecimals;
    private readonly List<string> _presetMessages = new List<string>();
    private string _sourceText, _textHeightText;
    private int _viewCount, _groundIndex, _designIndex, _selectedRow = -1;
    private bool _writeTable = true, _writeCsv, _writeXlsx, _wholeGroup, _stale = true;
    private List<string> _groundNames = new List<string> { NoSection }, _designNames = new List<string> { NoSection };

    public SectionTableSession(ProjectPreset preset)
    {
        _options = preset?.SectionTable ?? new SectionTableOptions();
        _stationDecimals = preset?.StationDecimals ?? 2;
        _textHeightText = NumberFormat.Trimmed(_options.TextHeight > 0 ? _options.TextHeight : 2.5, 3);

        var used = new HashSet<string>();
        foreach (var spec in _options.Rows ?? new List<TableRowSpec>())
        {
            if (spec == null) continue;
            var key = SectionTableBuilder.Canonical(spec.Key);
            if (key == null)
            {
                _presetMessages.Add($"Preset: bỏ qua dòng \"{spec.Key}\" ({spec.Label}) của bảng trắc ngang, khoá chưa hỗ trợ.");
                continue;
            }

            if (!used.Add(key)) continue;
            AddRow(new ProfileTableRowOption(key, string.IsNullOrEmpty(spec.Label) ? key : spec.Label, Math.Max(0, Math.Min(6, spec.Decimals)), true));
        }

        foreach (var known in SectionTableBuilder.KnownRows().Where(k => !used.Contains(k.Key)))
            AddRow(new ProfileTableRowOption(known.Key, known.Label, known.Decimals, false));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public string SourceText => _viewCount > 0 ? _sourceText : "chưa chọn";
    public int ViewCount => _viewCount;
    public bool HasSource => _viewCount > 0;

    /// <summary>Preset rows that were left out.</summary>
    public IReadOnlyList<string> PresetMessages => _presetMessages;

    public IReadOnlyList<string> GroundSectionNames => _groundNames;
    public IReadOnlyList<string> DesignSectionNames => _designNames;

    public int GroundSectionIndex
    {
        get => _groundIndex;
        set => SetIndex(ref _groundIndex, value, _groundNames.Count, nameof(GroundSectionIndex), nameof(GroundSection));
    }

    public int DesignSectionIndex
    {
        get => _designIndex;
        set => SetIndex(ref _designIndex, value, _designNames.Count, nameof(DesignSectionIndex), nameof(DesignSection));
    }

    /// <summary>The section source (surface) name for the ground line; null for none.</summary>
    public string GroundSection => _groundIndex > 0 ? _groundNames[_groundIndex] : null;

    /// <summary>The section source name for the design line; null for none.</summary>
    public string DesignSection => _designIndex > 0 ? _designNames[_designIndex] : null;

    /// <summary>"Cả nhóm": the picked views stand for every view of their section view group.</summary>
    public bool WholeGroup
    {
        get => _wholeGroup;
        set
        {
            if (_wholeGroup == value) return;
            _wholeGroup = value;
            Raise(nameof(WholeGroup));
            Changed();
        }
    }

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
    public bool RotateText => _options.RotateOffsetText;

    public ObservableCollection<ProfileTableRowOption> Rows { get; } = new ObservableCollection<ProfileTableRowOption>();

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

    /// <summary>"Bảng": lines and text under each section view.</summary>
    public bool WriteTable { get => _writeTable; set => SetOutput(ref _writeTable, value, nameof(WriteTable)); }

    /// <summary>"CSV": &lt;drawing&gt;_TRACNGANG.csv.</summary>
    public bool WriteCsv { get => _writeCsv; set => SetOutput(ref _writeCsv, value, nameof(WriteCsv)); }

    /// <summary>"Excel": &lt;drawing&gt;_TRACNGANG.xlsx.</summary>
    public bool WriteXlsx { get => _writeXlsx; set => SetOutput(ref _writeXlsx, value, nameof(WriteXlsx)); }

    public bool IsStale => _stale;

    /// <summary>The last built tables, in station order (empty before the first build).</summary>
    public IReadOnlyList<SectionTableModel> Models { get; private set; } = new SectionTableModel[0];

    /// <summary>One row per section of Models.</summary>
    public TableData Export => SectionTableBuilder.Export(Models, _stationDecimals, Dec(SectionTableBuilder.CutArea, Dec(SectionTableBuilder.FillArea, 2)),
        Dec(SectionTableBuilder.DesignElevation, 2));

    public bool RowsValid => Rows.Any(r => r.Include) && Rows.Where(r => r.Include).All(r => r.IsDecimalsValid);

    public bool CanApply => HasSource && GroundSection != null && IsTextHeightValid && RowsValid && (_writeTable || _writeCsv || _writeXlsx);

    public string SummaryText
    {
        get
        {
            if (!HasSource) return "Chưa chọn trắc ngang";
            if (GroundSection == null) return "Chọn mặt cắt tự nhiên";
            if (!IsTextHeightValid) return "Chiều cao chữ phải là số lớn hơn 0";
            if (!Rows.Any(r => r.Include)) return "Chọn ít nhất một dòng";
            if (!RowsValid) return "Số lẻ phải là số nguyên từ 0 đến 6";
            if (!(_writeTable || _writeCsv || _writeXlsx)) return "Chọn ít nhất một đầu ra";
            if (_stale || Models.Count == 0) return _viewCount.ToString(CultureInfo.InvariantCulture) + " trắc ngang; bấm Xem trước để vẽ bảng";
            var cut = Models.Sum(m => m.Areas.Cut);
            var fill = Models.Sum(m => m.Areas.Fill);
            return Models.Count.ToString(CultureInfo.InvariantCulture) + " trắc ngang, tổng diện tích đào " + ProfileTableBuilder.F(cut, 2)
                + ", đắp " + ProfileTableBuilder.F(fill, 2);
        }
    }

    /// <summary>The picked section views (count and a description).</summary>
    public void SetSource(string description, int viewCount)
    {
        _sourceText = description ?? "";
        _viewCount = Math.Max(0, viewCount);
        Raise(nameof(SourceText));
        Raise(nameof(ViewCount));
        Raise(nameof(HasSource));
        Stale(force: true);
    }

    /// <summary>
    /// The section sources of the views' sample lines. groundNames: surfaces first (the host orders them);
    /// designNames: corridor surfaces / corridors first. Each combo keeps its choice, else the remembered one, else the first.
    /// </summary>
    public void SetSections(IEnumerable<string> groundNames, IEnumerable<string> designNames, string preferredGround = null, string preferredDesign = null)
    {
        _groundNames = Names(groundNames);
        _designNames = Names(designNames);
        _groundIndex = Choose(_groundNames, GroundSection ?? preferredGround, null);
        _designIndex = Choose(_designNames, DesignSection ?? preferredDesign, GroundSection);
        Raise(nameof(GroundSectionNames));
        Raise(nameof(DesignSectionNames));
        Raise(nameof(GroundSectionIndex));
        Raise(nameof(DesignSectionIndex));
        Raise(nameof(GroundSection));
        Raise(nameof(DesignSection));
        Stale(force: true);
    }

    public void MoveRow(int index, int direction)
    {
        var target = index + Math.Sign(direction);
        if (index < 0 || index >= Rows.Count || target < 0 || target >= Rows.Count) return;
        Rows.Move(index, target);
        _selectedRow = target;
        Raise(nameof(SelectedRowIndex));
        Stale();
    }

    public List<TableRowSpec> RowSpecs() =>
        Rows.Where(r => r.Include && r.IsDecimalsValid).Select(r => new TableRowSpec(r.Key, r.Label, r.Decimals)).ToList();

    /// <summary>Builds one table per section in station order; the host draws or exports Models.</summary>
    public IReadOnlyList<SectionTableModel> Build(IEnumerable<SectionInput> sections)
    {
        var specs = RowSpecs();
        Models = (sections ?? Enumerable.Empty<SectionInput>()).Where(s => s != null).OrderBy(s => s.Station)
            .Select(s => SectionTableBuilder.Build(s.Name, s.Station, s.Ground, s.Design, specs)).ToList();
        _stale = false;
        Raise(nameof(Models));
        Raise(nameof(IsStale));
        Changed();
        return Models;
    }

    private int Dec(string key, int fallback)
    {
        var row = Rows.FirstOrDefault(r => r.Key == key && r.Include && r.IsDecimalsValid);
        return row == null ? fallback : row.Decimals;
    }

    private void AddRow(ProfileTableRowOption row)
    {
        row.PropertyChanged += (s, e) => Stale();
        Rows.Add(row);
    }

    private static List<string> Names(IEnumerable<string> names)
    {
        var list = new List<string> { NoSection };
        list.AddRange((names ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n)).Distinct());
        return list;
    }

    /// <summary>wanted when listed; else the first name that is not avoid; else 0 (none).</summary>
    private static int Choose(List<string> names, string wanted, string avoid)
    {
        var index = string.IsNullOrEmpty(wanted) ? -1 : names.IndexOf(wanted);
        if (index > 0) return index;
        for (var i = 1; i < names.Count; i++)
        {
            if (names[i] != avoid) return i;
        }

        return 0;
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
