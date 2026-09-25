using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using C3DTools.Core.Curves;
using C3DTools.Core.Presets;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Stations;

/// <summary>A line of the CTTOADO preview grid.</summary>
public sealed class StakePreviewRow
{
    public StakePreviewRow(string[] cells, bool hasZ)
    {
        Number = cells[0];
        Name = cells[1];
        Station = cells[2];
        X = cells[3];
        Y = cells[4];
        Z = hasZ ? cells[5] : "";
    }

    public string Number { get; }
    public string Name { get; }
    public string Station { get; }
    public string X { get; }
    public string Y { get; }
    public string Z { get; }
}

/// <summary>State of the CTTOADO dialog: the alignment, which stakes, the surface for Z, the outputs and the preview table.</summary>
public sealed class StakeTableSession : INotifyPropertyChanged
{
    public const string NoSurface = "(Không lấy Z)";

    private static readonly char[] Separators = { ';', ' ', '\t', '\r', '\n' };

    private readonly StakeTableOptions _options;
    private readonly int _stationDecimals;
    private string _sourceText, _intervalText = "20", _extraText = "";
    private double _start, _end;
    private bool _hasSource, _includeCurveStakes = true, _includeGeometryPoints, _stale = true;
    private bool _writeTable = true, _writeCsv, _writeXlsx, _writeCogo;
    private int _surfaceIndex, _missingZ;
    private List<string> _surfaceNames = new List<string> { NoSurface };
    private List<double> _extras = new List<double>();
    private bool _extrasValid = true;

    public StakeTableSession(ProjectPreset preset)
    {
        _options = preset?.StakeTable ?? new StakeTableOptions();
        _stationDecimals = preset?.StationDecimals ?? 2;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public string SourceText => _hasSource ? _sourceText : "chưa chọn";
    public bool HasSource => _hasSource;
    public double StartStation => _start;
    public double EndStation => _end;

    /// <summary>"Khoảng cách cọc" in metres; "12,5" and "12.5" both accepted.</summary>
    public string IntervalText
    {
        get => _intervalText;
        set
        {
            value ??= "";
            if (_intervalText == value) return;
            _intervalText = value;
            Raise(nameof(IntervalText));
            Raise(nameof(Interval));
            Raise(nameof(IsIntervalValid));
            Stale();
        }
    }

    /// <summary>NaN while IntervalText is not a number &gt; 0.</summary>
    public double Interval => NumberInput.TryParse(_intervalText, out var v) && v > 0 ? v : double.NaN;
    public bool IsIntervalValid => !double.IsNaN(Interval);

    /// <summary>NĐ/TĐ/P/TC/NC of the alignment's curves.</summary>
    public bool IncludeCurveStakes { get => _includeCurveStakes; set => SetOption(ref _includeCurveStakes, value, nameof(IncludeCurveStakes)); }

    /// <summary>Start/end of every alignment segment (named as detail stakes).</summary>
    public bool IncludeGeometryPoints { get => _includeGeometryPoints; set => SetOption(ref _includeGeometryPoints, value, nameof(IncludeGeometryPoints)); }

    /// <summary>"Cọc thêm": stations separated by ';', spaces or new lines, as "Km0+125.5", "0+125,5" or "125.5".</summary>
    public string ExtraStationsText
    {
        get => _extraText;
        set
        {
            value ??= "";
            if (_extraText == value) return;
            _extraText = value;
            ParseExtras();
            Raise(nameof(ExtraStationsText));
            Stale();
        }
    }

    public IReadOnlyList<double> ExtraStations => _extras;
    public bool IsExtrasValid => _extrasValid;

    /// <summary>NoSurface first, then the drawing's TIN surfaces.</summary>
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

    /// <summary>The surface Z is read from; null for none.</summary>
    public string Surface => _surfaceIndex > 0 ? _surfaceNames[_surfaceIndex] : null;

    /// <summary>"Bảng": AutoCAD Table at a picked point.</summary>
    public bool WriteTable { get => _writeTable; set => SetOutput(ref _writeTable, value, nameof(WriteTable)); }

    /// <summary>"CSV": &lt;drawing&gt;_TOADO.csv.</summary>
    public bool WriteCsv { get => _writeCsv; set => SetOutput(ref _writeCsv, value, nameof(WriteCsv)); }

    /// <summary>"Excel": &lt;drawing&gt;_TOADO.xlsx.</summary>
    public bool WriteXlsx { get => _writeXlsx; set => SetOutput(ref _writeXlsx, value, nameof(WriteXlsx)); }

    /// <summary>"Điểm COGO": one COGO point per stake.</summary>
    public bool WriteCogo { get => _writeCogo; set => SetOutput(ref _writeCogo, value, nameof(WriteCogo)); }

    /// <summary>The options changed since the last SetPreview.</summary>
    public bool IsStale => _stale;

    /// <summary>The last computed table (null before the first preview).</summary>
    public TableData Table { get; private set; }

    public bool HasZ { get; private set; }

    public ObservableCollection<StakePreviewRow> PreviewRows { get; } = new ObservableCollection<StakePreviewRow>();

    public bool CanApply => _hasSource && IsIntervalValid && _extrasValid && (_writeTable || _writeCsv || _writeXlsx || _writeCogo);

    public string SummaryText
    {
        get
        {
            if (!_hasSource) return "Chưa chọn tuyến";
            if (!IsIntervalValid) return "Khoảng cách cọc phải là số lớn hơn 0";
            if (!_extrasValid) return "Cọc thêm: lý trình không hợp lệ hoặc nằm ngoài tuyến";
            if (!(_writeTable || _writeCsv || _writeXlsx || _writeCogo)) return "Chọn ít nhất một đầu ra";
            if (_stale || Table == null) return "Bấm Xem trước để cập nhật bảng";
            var text = PreviewRows.Count.ToString(CultureInfo.InvariantCulture) + " cọc";
            if (Surface == null) return text;
            text += ", Z từ mặt phủ " + Surface;
            return _missingZ > 0 ? text + " (" + _missingZ.ToString(CultureInfo.InvariantCulture) + " cọc ngoài mặt phủ)" : text;
        }
    }

    /// <summary>The picked alignment and its station range.</summary>
    public void SetSource(string description, double start, double end)
    {
        _sourceText = description ?? "";
        _start = start;
        _end = end;
        _hasSource = true;
        ParseExtras();
        Raise(nameof(SourceText));
        Raise(nameof(HasSource));
        Stale(force: true);
    }

    /// <summary>The drawing's surfaces; the chosen one stays chosen if it is still there.</summary>
    public void SetSurfaces(IEnumerable<string> names)
    {
        var current = Surface;
        _surfaceNames = new List<string> { NoSurface };
        _surfaceNames.AddRange((names ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n)));
        var index = current == null ? 0 : _surfaceNames.FindIndex(1, n => n == current);
        _surfaceIndex = index < 0 ? 0 : index;
        Raise(nameof(SurfaceNames));
        Raise(nameof(SurfaceIndex));
        Raise(nameof(Surface));
        if (Surface != current) Stale();
    }

    /// <summary>The stations to tabulate with the current options (StakeStationList).</summary>
    public List<StakeStation> Stations(IEnumerable<StakeStation> curveStakes, IEnumerable<double> geometryStations)
    {
        if (!_hasSource) throw new InvalidOperationException("Chưa chọn tuyến.");
        var extras = new List<double>(_extras);
        if (_includeGeometryPoints && geometryStations != null) extras.AddRange(geometryStations);
        return StakeStationList.Build(_start, _end, Interval, _includeCurveStakes ? curveStakes : null, extras);
    }

    /// <summary>The located stakes: builds the table with the preset decimals and fills the grid.</summary>
    public void SetPreview(IEnumerable<StakePoint> points)
    {
        var list = (points ?? Enumerable.Empty<StakePoint>()).ToList();
        Table = StakeCoordinateTable.Build(list, _options, _stationDecimals);
        HasZ = Table.Headers.Count == 6;
        _missingZ = Surface == null ? 0 : list.Count(p => !p.Z.HasValue);
        PreviewRows.Clear();
        foreach (var row in Table.Rows) PreviewRows.Add(new StakePreviewRow(row, HasZ));
        _stale = false;
        Raise(nameof(Table));
        Raise(nameof(HasZ));
        Raise(nameof(IsStale));
        Changed();
    }

    private void ParseExtras()
    {
        _extras = new List<double>();
        _extrasValid = true;
        foreach (var token in _extraText.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryStation(token, out var station)
                || (_hasSource && (station < _start - StakeStationList.Tolerance || station > _end + StakeStationList.Tolerance)))
            {
                _extrasValid = false;
                continue;
            }

            _extras.Add(station);
        }

        Raise(nameof(ExtraStations));
        Raise(nameof(IsExtrasValid));
    }

    private static bool TryStation(string token, out double station)
    {
        if (token.IndexOf('+') >= 0) return StationFormatter.TryParse(token.Replace(',', '.'), out station);
        return NumberInput.TryParse(token, out station);
    }

    private void SetOption(ref bool field, bool value, string name)
    {
        if (field == value) return;
        field = value;
        Raise(name);
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

        Changed();
    }

    private void Changed()
    {
        Raise(nameof(CanApply));
        Raise(nameof(SummaryText));
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
