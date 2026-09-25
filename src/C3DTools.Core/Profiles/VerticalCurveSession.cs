using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using C3DTools.Core.Presets;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Profiles;

/// <summary>A read-only line of the CTCONGDUNG grid (VerticalCurveTableBuilder cells).</summary>
public sealed class VerticalCurveRow
{
    public VerticalCurveRow(string[] cells)
    {
        if (cells == null || cells.Length != VerticalCurveTableBuilder.Headers.Length) throw new ArgumentException("Sai số ô.", nameof(cells));
        Name = cells[0];
        Station = cells[1];
        PviElevation = cells[2];
        GradeIn = cells[3];
        GradeOut = cells[4];
        A = cells[5];
        R = cells[6];
        K = cells[7];
        T = cells[8];
        E = cells[9];
        StartStation = cells[10];
        EndStation = cells[11];
        HighLow = cells[12];
        Warning = cells[13];
    }

    public string Name { get; }
    public string Station { get; }
    public string PviElevation { get; }
    public string GradeIn { get; }
    public string GradeOut { get; }
    public string A { get; }
    public string R { get; }
    public string K { get; }
    public string T { get; }
    public string E { get; }
    public string StartStation { get; }
    public string EndStation { get; }

    /// <summary>"station / elevation" of the high or low point, or empty.</summary>
    public string HighLow { get; }

    public string Warning { get; }
    public bool HasWarning => Warning.Length > 0;
}

/// <summary>
/// State of the CTCONGDUNG dialog: the profile view, which design profile, V, the PVIs with their elements and
/// warnings (read-only), and the outputs Khung / Bảng / CSV / Excel. The host reads every profile of the view's
/// alignment once (SetProfiles); choosing another profile or V recomputes at once.
/// </summary>
public sealed class VerticalCurveSession : INotifyPropertyChanged
{
    private static readonly double[] FallbackSpeeds = { 20, 30, 40, 60, 80, 100, 120 };

    private readonly VerticalRules _rules;
    private readonly int _stationDecimals;
    private string _sourceText;
    private bool _hasSource, _writeBox = true, _writeTable, _writeCsv, _writeXlsx;
    private double _designSpeed;
    private int _profileIndex = -1;
    private List<string> _profileNames = new List<string>();
    private List<IReadOnlyList<ProfileSegment>> _profileSegments = new List<IReadOnlyList<ProfileSegment>>();
    private List<VerticalCurve> _curves = new List<VerticalCurve>();
    private List<List<VerticalCurveIssue>> _issues = new List<List<VerticalCurveIssue>>();
    private string _loadError;

    public VerticalCurveSession(ProjectPreset preset)
    {
        _rules = preset?.VerticalRules ?? new VerticalRules();
        _stationDecimals = preset?.StationDecimals ?? 2;
        _designSpeed = preset?.DesignSpeed ?? 60;
        var speeds = new[] { _rules.MinRadiusCrest, _rules.MinRadiusSag, _rules.MinLength, _rules.MaxGrade }
            .Where(t => t != null).SelectMany(t => t).Where(r => r != null).Select(r => r.DesignSpeed).ToList();
        AvailableSpeeds = (speeds.Count > 0 ? speeds : FallbackSpeeds.ToList())
            .Concat(new[] { _designSpeed }).Distinct().OrderBy(v => v).ToList();
        Table = VerticalCurveTableBuilder.Build(_curves, null, _stationDecimals);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public string SourceText => _hasSource ? _sourceText : "chưa chọn";
    public bool HasSource => _hasSource;

    public IReadOnlyList<string> ProfileNames => _profileNames;

    /// <summary>Index into ProfileNames; -1 when the alignment has no design profile.</summary>
    public int ProfileIndex
    {
        get => _profileIndex;
        set
        {
            if (value < 0 || value >= _profileNames.Count || value == _profileIndex) return;
            _profileIndex = value;
            Raise(nameof(ProfileIndex));
            Raise(nameof(Profile));
            Recalculate();
        }
    }

    /// <summary>Name of the chosen profile, or null.</summary>
    public string Profile => _profileIndex >= 0 ? _profileNames[_profileIndex] : null;

    public IReadOnlyList<double> AvailableSpeeds { get; }

    public double DesignSpeed
    {
        get => _designSpeed;
        set
        {
            if (_designSpeed == value) return;
            _designSpeed = value;
            Raise(nameof(DesignSpeed));
            Raise(nameof(RminText));
            Recalculate();
        }
    }

    /// <summary>The limits used for V, or why nothing is checked.</summary>
    public string RminText
    {
        get
        {
            var parts = new List<string>();
            var missing = new List<string>();
            void Add(List<SpeedValueRule> table, string name, string unit)
            {
                var v = VerticalCurveRuleChecker.Find(table, _designSpeed, name, missing);
                if (v.HasValue) parts.Add(name + " " + NumberFormat.Trimmed(v.Value, 2) + unit);
            }

            Add(_rules.MinRadiusCrest, "Rmin lồi", " m");
            Add(_rules.MinRadiusSag, "Rmin lõm", " m");
            Add(_rules.MinLength, "Lmin", " m");
            Add(_rules.MaxGrade, "imax", "%");
            if (parts.Count == 0 && missing.Count == 0) return "Preset chưa có bảng cong đứng – không kiểm tra R, L, i";
            var text = string.Join(" · ", parts);
            if (missing.Count > 0)
                text += (text.Length > 0 ? " · " : "") + "chưa có " + string.Join(", ", missing) + " cho V = " + NumberFormat.Trimmed(_designSpeed, 0) + " km/h";
            return text;
        }
    }

    /// <summary>"Khung": the box above each PVI in the profile view.</summary>
    public bool WriteBox { get => _writeBox; set => SetOutput(ref _writeBox, value, nameof(WriteBox)); }

    /// <summary>"Bảng": AutoCAD Table at a picked point.</summary>
    public bool WriteTable { get => _writeTable; set => SetOutput(ref _writeTable, value, nameof(WriteTable)); }

    /// <summary>"CSV": &lt;drawing&gt;_CONGDUNG.csv.</summary>
    public bool WriteCsv { get => _writeCsv; set => SetOutput(ref _writeCsv, value, nameof(WriteCsv)); }

    /// <summary>"Excel": &lt;drawing&gt;_CONGDUNG.xlsx.</summary>
    public bool WriteXlsx { get => _writeXlsx; set => SetOutput(ref _writeXlsx, value, nameof(WriteXlsx)); }

    public IReadOnlyList<VerticalCurve> Curves => _curves;
    public ObservableCollection<VerticalCurveRow> Rows { get; } = new ObservableCollection<VerticalCurveRow>();
    public TableData Table { get; private set; }

    /// <summary>Warnings of the curve at this index (0-based, as Curves).</summary>
    public IReadOnlyList<VerticalCurveIssue> IssuesAt(int index) => _issues[index];

    public int WarningCount => _issues.Count(i => i.Count > 0);

    public bool CanApply => _hasSource && Profile != null && _curves.Count > 0 && (_writeBox || _writeTable || _writeCsv || _writeXlsx);

    public string SummaryText
    {
        get
        {
            if (!_hasSource) return "Chưa chọn trắc dọc";
            if (Profile == null) return "Tuyến không có trắc dọc thiết kế";
            if (_loadError != null) return _loadError;
            if (_curves.Count == 0) return "Trắc dọc " + Profile + " không có đỉnh";
            if (!(_writeBox || _writeTable || _writeCsv || _writeXlsx)) return "Chọn ít nhất một đầu ra";
            var text = _curves.Count.ToString(CultureInfo.InvariantCulture) + " đỉnh, "
                + _curves.Count(c => c.HasCurve).ToString(CultureInfo.InvariantCulture) + " đường cong đứng";
            var warnings = WarningCount;
            return warnings > 0 ? text + ", " + warnings.ToString(CultureInfo.InvariantCulture) + " đỉnh có cảnh báo" : text;
        }
    }

    public void SetSource(string description)
    {
        _sourceText = description ?? "";
        _hasSource = true;
        Raise(nameof(SourceText));
        Raise(nameof(HasSource));
        Changed();
    }

    /// <summary>
    /// The design profiles of the view's alignment with their entities (null segments = could not be read).
    /// The chosen profile stays chosen when it is still there; otherwise the preferred one, else the first.
    /// </summary>
    public void SetProfiles(IEnumerable<KeyValuePair<string, IReadOnlyList<ProfileSegment>>> profiles, string preferred = null)
    {
        var current = Profile ?? preferred;
        var list = (profiles ?? Enumerable.Empty<KeyValuePair<string, IReadOnlyList<ProfileSegment>>>())
            .Where(p => !string.IsNullOrEmpty(p.Key)).ToList();
        _profileNames = list.Select(p => p.Key).ToList();
        _profileSegments = list.Select(p => p.Value).ToList();
        var index = current == null ? -1 : _profileNames.IndexOf(current);
        _profileIndex = index >= 0 ? index : _profileNames.Count > 0 ? 0 : -1;
        Raise(nameof(ProfileNames));
        Raise(nameof(ProfileIndex));
        Raise(nameof(Profile));
        Recalculate();
    }

    private void Recalculate()
    {
        _loadError = null;
        var segments = _profileIndex >= 0 ? _profileSegments[_profileIndex] : null;
        if (_profileIndex >= 0 && segments == null) _loadError = "Không đọc được trắc dọc " + Profile;
        try
        {
            _curves = VerticalCurve.FromSegments(segments);
        }
        catch (ArgumentException ex)
        {
            _curves = new List<VerticalCurve>();
            _loadError = "Trắc dọc " + Profile + ": " + ex.Message;
        }

        _issues = _curves.Select(c => VerticalCurveRuleChecker.Check(c, _designSpeed, _rules)).ToList();
        Table = VerticalCurveTableBuilder.Build(_curves, Warning, _stationDecimals);
        Rows.Clear();
        foreach (var row in Table.Rows) Rows.Add(new VerticalCurveRow(row));
        Raise(nameof(Curves));
        Raise(nameof(Table));
        Raise(nameof(WarningCount));
        Changed();
    }

    private string Warning(VerticalCurve c)
    {
        var index = _curves.IndexOf(c);
        return index < 0 ? "" : string.Join(" ", _issues[index].Select(i => i.Message));
    }

    private void SetOutput(ref bool field, bool value, string name)
    {
        if (field == value) return;
        field = value;
        Raise(name);
        Changed();
    }

    private void Changed()
    {
        Raise(nameof(CanApply));
        Raise(nameof(SummaryText));
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
