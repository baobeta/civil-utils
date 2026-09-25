using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using C3DTools.Core.Presets;
using C3DTools.Core.Stations;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Curves;

public enum CurveRowSeverity { None, Warning, Error }

/// <summary>State and behaviour of the CTYTC grid dialog. The WPF window only binds to it.</summary>
public sealed class CurveDesignSession : INotifyPropertyChanged
{
    private static readonly double[] FallbackSpeeds = { 20, 30, 40, 60, 80, 100, 120 };
    private const double FallbackRadius = 100;   // YTC.lsp: rtt 100

    private readonly ProjectPreset _preset;
    private readonly CurveRules _rules;
    private IReadOnlyList<PlanPoint> _pis = new PlanPoint[0];
    private readonly List<CurveInput> _inputs = new List<CurveInput>();
    private double _designSpeed;
    private double _startStation;
    private bool _readOnlyGeometry, _createAlignment, _drawCurves = true, _drawBoxes = true, _drawStakes = true, _writeCsv = true, _writeXlsx, _writeTable;
    private double _textHeight;

    public CurveDesignSession(ProjectPreset preset)
    {
        _preset = preset ?? throw new ArgumentNullException(nameof(preset));
        _rules = preset.CurveRules;
        _designSpeed = preset.DesignSpeed ?? 60;
        _textHeight = preset.CurveBox?.TextHeight ?? 2.5;
        var speeds = _rules != null && _rules.MinRadius.Count > 0
            ? _rules.MinRadius.Select(r => r.DesignSpeed)
            : FallbackSpeeds;
        AvailableSpeeds = speeds.Concat(new[] { _designSpeed }).Distinct().OrderBy(v => v).ToList();
    }

    public event PropertyChangedEventHandler PropertyChanged;

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

    public double StartStation
    {
        get => _startStation;
        set
        {
            if (_startStation == value) return;
            _startStation = value;
            Raise(nameof(StartStation));
            Recalculate();
        }
    }

    public IReadOnlyList<double> AvailableSpeeds { get; }

    public string RminText
    {
        get
        {
            if (_rules == null) return "Chưa có bảng TCVN – không kiểm tra R, L";
            var rule = SpeedRule();
            return rule == null
                ? $"Preset chưa có Rmin cho vận tốc {NumberFormat.Trimmed(_designSpeed, 0)} km/h."
                : $"Rmin giới hạn {NumberFormat.Trimmed(rule.MinRadius, 2)} m · thông thường {NumberFormat.Trimmed(rule.NormalRadius, 2)} m";
        }
    }

    /// <summary>YTCA mode: R/L come from an existing alignment and are not editable. State only; the view enforces it.</summary>
    public bool ReadOnlyGeometry { get => _readOnlyGeometry; set => Set(ref _readOnlyGeometry, value, nameof(ReadOnlyGeometry)); }
    public double TextHeight { get => _textHeight; set => Set(ref _textHeight, value, nameof(TextHeight)); }
    public bool DrawCurves { get => _drawCurves; set => Set(ref _drawCurves, value, nameof(DrawCurves)); }
    public bool CreateAlignment { get => _createAlignment; set => Set(ref _createAlignment, value, nameof(CreateAlignment)); }
    public bool DrawBoxes { get => _drawBoxes; set => Set(ref _drawBoxes, value, nameof(DrawBoxes)); }
    public bool DrawStakes { get => _drawStakes; set => Set(ref _drawStakes, value, nameof(DrawStakes)); }
    public bool WriteCsv { get => _writeCsv; set => Set(ref _writeCsv, value, nameof(WriteCsv)); }
    /// <summary>"Excel": &lt;drawing&gt;_YEUTOCONG.xlsx next to the CSV.</summary>
    public bool WriteXlsx { get => _writeXlsx; set => Set(ref _writeXlsx, value, nameof(WriteXlsx)); }
    /// <summary>Curve summary AutoCAD Table (CTYTCBANG) next to the route's last PI.</summary>
    public bool WriteTable { get => _writeTable; set => Set(ref _writeTable, value, nameof(WriteTable)); }

    public ObservableCollection<CurveRow> Rows { get; } = new ObservableCollection<CurveRow>();
    public RouteDesign Design { get; private set; }
    public IReadOnlyList<PlanPoint> Pis => _pis;

    /// <summary>One per interior PI, including collinear PIs that have no row.</summary>
    public IReadOnlyList<CurveInput> Inputs => _inputs;

    public bool CanApply => Design != null && Design.CanApply && Rows.All(r => !r.HasInputError);
    public double EndStation => Design?.EndStation ?? _startStation;

    public string SummaryText =>
        $"{Design?.Curves.Count(c => c.Elements != null) ?? 0} đường cong, chiều dài tuyến = {NumberFormat.Trimmed(EndStation - _startStation, 2)} m";

    internal int StationDecimals => _preset.StationDecimals;
    internal int AngleSecondDecimals => _preset.CurveBox?.AngleSecondDecimals ?? 0;

    /// <summary>pis must already be de-duplicated. existingInputs: null, or one per interior PI (null entries get defaults).</summary>
    public void Load(IReadOnlyList<PlanPoint> pis, IReadOnlyList<CurveInput> existingInputs)
    {
        if (pis == null) throw new ArgumentNullException(nameof(pis));
        if (pis.Count < 2) throw new ArgumentException("Tuyến cần ít nhất 2 đỉnh.", nameof(pis));
        if (existingInputs != null && existingInputs.Count != pis.Count - 2)
            throw new ArgumentException($"Cần {pis.Count - 2} bộ thông số cong.", nameof(existingInputs));

        _pis = pis.ToList();
        _inputs.Clear();
        CurveInput previous = null;
        for (var i = 1; i < pis.Count - 1; i++)
        {
            var existing = existingInputs?[i - 1];
            var input = existing != null
                ? existing.Clone()
                : DefaultFor(previous, Suggestion(), Deflection(pis[i - 1], pis[i], pis[i + 1]));
            _inputs.Add(input);
            previous = input;
        }

        Design = RouteDesigner.Design(_pis, _startStation, _inputs, _designSpeed, _rules);
        Rows.Clear();
        foreach (var curve in Design.Curves) Rows.Add(new CurveRow(this, curve.Number - 1, curve.PiIndex));
        Recalculate();
    }

    public void CopyDown(int rowIndex)
    {
        var source = Rows[rowIndex].Input;
        foreach (var row in Rows.Skip(rowIndex + 1))
        {
            var target = row.Input;
            target.Radius = source.Radius;
            target.SpiralIn = source.SpiralIn;
            target.SpiralOut = source.SpiralOut;
            target.Wb = source.Wb;
            target.Wl = source.Wl;
            row.ClearInputErrors();
        }

        Recalculate();
    }

    public void SuggestAll()
    {
        if (_rules != null)
        {
            var speedRule = SpeedRule();
            foreach (var row in Rows)
            {
                var input = row.Input;
                if (speedRule != null) input.Radius = speedRule.NormalRadius;
                var spiral = CurveRuleChecker.Find(_rules.MinSpiral, input.Radius, _designSpeed);
                if (spiral != null) input.SpiralIn = input.SpiralOut = spiral.Value;
                input.Wb = CurveRuleChecker.Find(_rules.Widening, input.Radius, _designSpeed)?.Value ?? input.Wb;
                row.ClearInputErrors();
            }
        }

        Recalculate();
    }

    public void Recalculate()
    {
        if (_pis.Count < 2) return;
        Design = RouteDesigner.Design(_pis, _startStation, _inputs, _designSpeed, _rules);
        foreach (var row in Rows) row.Refresh();
        Raise(nameof(Design));
        Raise(nameof(CanApply));
        Raise(nameof(EndStation));
        Raise(nameof(SummaryText));
    }

    internal DesignedCurve CurveFor(CurveRow row) => Design.Curves[row.Index];
    internal CurveInput InputFor(CurveRow row) => _inputs[row.PiIndex - 1];

    /// <summary>
    /// Values for a newly loaded row that has no existing curve.
    /// previous: the row above (null for the first row); suggestion: TCVN values for V and this PI.
    /// </summary>
    private static CurveInput DefaultFor(CurveInput previous, CurveInput suggestion, double deltaRadians)
    {
        // YTC.lsp copies the previous PI; the first PI gets the TCVN suggestion (Rmin thông thường, or rtt 100).
        return previous != null ? previous.Clone() : suggestion;
    }

    private CurveInput Suggestion() => new CurveInput { Radius = SpeedRule()?.NormalRadius ?? FallbackRadius };

    private SpeedRadiusRule SpeedRule() => _rules?.MinRadius.FirstOrDefault(r => r.DesignSpeed == _designSpeed);

    private static double Deflection(PlanPoint a, PlanPoint b, PlanPoint c)
    {
        double x1 = b.X - a.X, y1 = b.Y - a.Y, x2 = c.X - b.X, y2 = c.Y - b.Y;
        return Math.Abs(Math.Atan2(x1 * y2 - y1 * x2, x1 * x2 + y1 * y2));
    }

    private void Set<T>(ref T field, T value, string name)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Raise(name);
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>One numbered curve (Đn) in the grid.</summary>
public sealed class CurveRow : INotifyPropertyChanged
{
    private static readonly string[] Derived =
    {
        nameof(AText), nameof(Turn), nameof(RadiusText), nameof(SpiralInText), nameof(SpiralOutText),
        nameof(WbText), nameof(WlText), nameof(T1Text), nameof(T2Text), nameof(PText), nameof(KText),
        nameof(StationsText), nameof(IssueText), nameof(Severity), nameof(HasInputError),
    };

    private readonly CurveDesignSession _session;
    /// <summary>Rejected text per field, shown back to the user until a valid value replaces it.</summary>
    private readonly Dictionary<string, string> _badText = new Dictionary<string, string>();
    /// <summary>Accepted text per field ("12," or "12."), shown back while it still means the stored value.</summary>
    private readonly Dictionary<string, string> _typedText = new Dictionary<string, string>();

    internal CurveRow(CurveDesignSession session, int index, int piIndex)
    {
        _session = session;
        Index = index;
        PiIndex = piIndex;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public int Index { get; }
    public int PiIndex { get; }
    public string Name => "Đ" + (Index + 1);
    public PlanPoint Pi => _session.Pis[PiIndex];
    public int Turn => Curve.Turn;
    public string AText => AngleFormatter.Dms(Curve.DeltaRadians * 180 / Math.PI, _session.AngleSecondDecimals);
    public bool HasInputError => _badText.Count > 0;

    internal CurveInput Input => _session.InputFor(this);
    private DesignedCurve Curve => _session.CurveFor(this);

    public string RadiusText { get => Text(nameof(RadiusText), Input.Radius); set => Set(nameof(RadiusText), value, v => Input.Radius = v); }
    public string SpiralInText { get => Text(nameof(SpiralInText), Input.SpiralIn); set => Set(nameof(SpiralInText), value, v => Input.SpiralIn = v); }
    public string SpiralOutText { get => Text(nameof(SpiralOutText), Input.SpiralOut); set => Set(nameof(SpiralOutText), value, v => Input.SpiralOut = v); }
    public string WbText { get => Text(nameof(WbText), Input.Wb); set => Set(nameof(WbText), value, v => Input.Wb = v); }
    public string WlText { get => Text(nameof(WlText), Input.Wl); set => Set(nameof(WlText), value, v => Input.Wl = v); }

    public string T1Text => Element(e => e.T1);
    public string T2Text => Element(e => e.T2);
    public string PText => Element(e => e.P);
    public string KText => Element(e => e.K);

    public string StationsText
    {
        get
        {
            var c = Curve;
            if (c.Elements == null) return "";
            var parts = new List<string>();
            if (c.Input.SpiralIn > 0) parts.Add("NĐ " + Station(c.StationStart));
            parts.Add("TĐ " + Station(c.StationArcStart));
            parts.Add("P " + Station(c.StationArcMid));
            parts.Add("TC " + Station(c.StationArcEnd));
            if (c.Input.SpiralOut > 0) parts.Add("NC " + Station(c.StationEnd));
            return string.Join(" · ", parts);
        }
    }

    public string IssueText => string.Join("\n", Curve.Issues.Select(i => i.Message));

    public CurveRowSeverity Severity =>
        HasInputError || Curve.Issues.Any(i => i.IsError) ? CurveRowSeverity.Error
        : Curve.Issues.Count > 0 ? CurveRowSeverity.Warning
        : CurveRowSeverity.None;

    /// <summary>Called when the session overwrites every input value (copy down, suggest).</summary>
    internal void ClearInputErrors()
    {
        _badText.Clear();
        _typedText.Clear();
    }

    internal void Refresh()
    {
        foreach (var name in Derived) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void Set(string field, string text, Action<double> apply)
    {
        if (NumberInput.TryParse(text, out var value))
        {
            _badText.Remove(field);
            _typedText[field] = text.Trim();
            apply(value);
        }
        else
        {
            _badText[field] = text ?? "";
        }

        _session.Recalculate();
    }

    private string Element(Func<CurveElements, double> pick) =>
        Curve.Elements == null ? "?" : NumberFormat.Trimmed(pick(Curve.Elements), 2);

    private string Station(double station) => StationFormatter.Format(station, _session.StationDecimals, withKmPrefix: false);

    private string Text(string field, double value)
    {
        if (_badText.TryGetValue(field, out var bad)) return bad;
        // A grid bound on every keystroke reads the text back at once; "12," must not turn into "12".
        if (_typedText.TryGetValue(field, out var typed) && NumberInput.TryParse(typed, out var v) && v == value) return typed;
        return NumberFormat.Trimmed(value, 3);
    }
}
