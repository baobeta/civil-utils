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
    private double _crossSlope, _halfWidth, _edgeStep;
    private bool _drawEdges, _splitWidening, _suggestNormalRadius = true, _canEditDeflection, _pisEdited, _shiftProfiles, _writeSuperelevation;

    public CurveDesignSession(ProjectPreset preset)
    {
        _preset = preset ?? throw new ArgumentNullException(nameof(preset));
        _rules = preset.CurveRules;
        _designSpeed = preset.DesignSpeed ?? 60;
        _textHeight = preset.CurveBox?.TextHeight ?? 2.5;
        _crossSlope = preset.RoadSection?.CrossSlope ?? 2;
        _halfWidth = preset.RoadSection?.PavementHalfWidth ?? 3.5;
        _edgeStep = preset.RoadSection?.EdgeStep > 0 ? preset.RoadSection.EdgeStep : 1;
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

    /// <summary>"Tạo polyline các đoạn nối": the widened pavement edges of every widened curve.</summary>
    public bool DrawEdges { get => _drawEdges; set => Set(ref _drawEdges, value, nameof(DrawEdges)); }

    /// <summary>"Siêu cao → Alignment": write the critical stations into the Civil 3D alignment (only when it is created or updated).</summary>
    public bool WriteSuperelevation { get => _writeSuperelevation; set => Set(ref _writeSuperelevation, value, nameof(WriteSuperelevation)); }

    /// <summary>"Dồn dịch đỉnh trắc dọc phía sau": move the PVIs of the alignment's layout profiles with the new stations.</summary>
    public bool ShiftProfiles { get => _shiftProfiles; set => Set(ref _shiftProfiles, value, nameof(ShiftProfiles)); }

    /// <summary>"Mở rộng phân đều khi tra": the table's widening goes half inside, half outside. Off (default, as YTC.lsp): all inside.</summary>
    public bool SplitWidening { get => _splitWidening; set => Set(ref _splitWidening, value, nameof(SplitWidening)); }

    /// <summary>"Rmin thông thường" (true) or "Rmin tối thiểu" for "Tra yếu tố cong".</summary>
    public bool SuggestNormalRadius { get => _suggestNormalRadius; set => Set(ref _suggestNormalRadius, value, nameof(SuggestNormalRadius)); }

    /// <summary>%, normal crown slope (in).</summary>
    public double CrossSlope
    {
        get => _crossSlope;
        set
        {
            if (_crossSlope == value || value < 0) return;
            _crossSlope = value;
            Raise(nameof(CrossSlope));
            Recalculate();
        }
    }

    /// <summary>m, B/2 for the edge polylines.</summary>
    public double PavementHalfWidth { get => _halfWidth; set { if (value > 0) Set(ref _halfWidth, value, nameof(PavementHalfWidth)); } }

    public double EdgeStep => _edgeStep;

    /// <summary>Set by the host: the source is a polyline being designed, so a PI angle can be changed.</summary>
    public bool CanEditDeflection { get => _canEditDeflection; set => Set(ref _canEditDeflection, value, nameof(CanEditDeflection)); }

    /// <summary>A PI angle was changed: Áp dụng moves the polyline's vertices.</summary>
    public bool PisEdited => _pisEdited;

    /// <summary>Critical stations of every superelevated curve, in station order.</summary>
    public List<SuperelevationPoint> SuperelevationPoints =>
        Design == null ? new List<SuperelevationPoint>() : Design.Curves.SelectMany(c => SuperelevationPlanner.Plan(c, _crossSlope)).OrderBy(p => p.Station).ToList();

    public bool HasSuperelevation => Design != null && Design.Curves.Any(c => c.Elements != null && c.Input.Superelevated);

    public ObservableCollection<CurveRow> Rows { get; } = new ObservableCollection<CurveRow>();
    public RouteDesign Design { get; private set; }
    public IReadOnlyList<PlanPoint> Pis => _pis;

    /// <summary>One per interior PI, including collinear PIs that have no row.</summary>
    public IReadOnlyList<CurveInput> Inputs => _inputs;

    public bool CanApply => Design != null && Design.CanApply && Rows.All(r => !r.HasInputError);
    public double EndStation => Design?.EndStation ?? _startStation;

    public string SummaryText =>
        $"{Design?.Curves.Count(c => c.Elements != null) ?? 0} đường cong, chiều dài tuyến = {NumberFormat.Trimmed(EndStation - _startStation, 2)} m";

    public int StationDecimals => _preset.StationDecimals;
    internal int AngleSecondDecimals => _preset.CurveBox?.AngleSecondDecimals ?? 0;

    /// <summary>pis must already be de-duplicated. existingInputs: null, or one per interior PI (null entries get defaults).</summary>
    public void Load(IReadOnlyList<PlanPoint> pis, IReadOnlyList<CurveInput> existingInputs)
    {
        if (pis == null) throw new ArgumentNullException(nameof(pis));
        if (pis.Count < 2) throw new ArgumentException("Tuyến cần ít nhất 2 đỉnh.", nameof(pis));
        if (existingInputs != null && existingInputs.Count != pis.Count - 2)
            throw new ArgumentException($"Cần {pis.Count - 2} bộ thông số cong.", nameof(existingInputs));

        _pis = pis.ToList();
        _pisEdited = false;
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
            foreach (var row in Rows)
            {
                Suggest(row.Input, _suggestNormalRadius);
                row.ClearInputErrors();
            }

        Recalculate();
    }

    /// <summary>"Tra yếu tố cong" for one row: R from the TCVN radius table (Rmin tối thiểu or thông thường), then L and W.</summary>
    public void SuggestRow(int rowIndex)
    {
        if (_rules == null || rowIndex < 0 || rowIndex >= Rows.Count) return;
        Suggest(Rows[rowIndex].Input, _suggestNormalRadius);
        Rows[rowIndex].ClearInputErrors();
        Recalculate();
    }

    /// <summary>
    /// "Tra siêu cao" for one row: isc from the table (Bảng 13) and the runoff (the spiral, or Bảng 14 length with half of
    /// it on the tangent). Returns a message when the table has no value, else null.
    /// </summary>
    public string SuggestSuperelevation(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= Rows.Count) return null;
        var input = Rows[rowIndex].Input;
        var speed = input.DesignSpeed ?? _designSpeed;
        var rate = _rules == null ? null : CurveRuleChecker.Find(_rules.Superelevation, input.Radius, speed);
        if (rate == null)
            return $"Preset chưa có độ dốc siêu cao cho R = {NumberFormat.Trimmed(input.Radius, 2)} m, V = {NumberFormat.Trimmed(speed, 0)} km/h.";

        input.Superelevated = rate.Value > _crossSlope;
        input.SuperRate = rate.Value;
        input.RunoffOnSpiral = input.SpiralIn > 0 || input.SpiralOut > 0;
        var runoff = CurveRuleChecker.Find(_rules.MinSpiral, input.Radius, speed)?.Value;
        if (runoff > 0)
        {
            input.RunoffIn = input.RunoffOut = runoff.Value;
            input.OffsetIn = input.OffsetOut = runoff.Value / 2;
        }

        Rows[rowIndex].ClearInputErrors();
        Recalculate();
        return null;
    }

    /// <summary>"Hiệu chỉnh góc chuyển hướng": turns everything after the row's PI so its angle becomes degrees.</summary>
    public void SetDeflection(int rowIndex, double degrees)
    {
        if (!_canEditDeflection || _readOnlyGeometry) throw new InvalidOperationException("Chỉ sửa được góc chuyển hướng khi thiết kế từ polyline.");
        if (rowIndex < 0 || rowIndex >= Rows.Count) throw new ArgumentOutOfRangeException(nameof(rowIndex));
        _pis = PiEditor.SetDeflection(_pis, Rows[rowIndex].PiIndex, degrees * Math.PI / 180);
        _pisEdited = true;
        Raise(nameof(Pis));
        Raise(nameof(PisEdited));
        Recalculate();
    }

    private void Suggest(CurveInput input, bool normal)
    {
        var speed = input.DesignSpeed ?? _designSpeed;
        var speedRule = _rules.MinRadius.FirstOrDefault(r => r.DesignSpeed == speed);
        if (speedRule != null) input.Radius = normal ? speedRule.NormalRadius : speedRule.MinRadius;
        var spiral = CurveRuleChecker.Find(_rules.MinSpiral, input.Radius, speed);
        if (spiral != null) input.SpiralIn = input.SpiralOut = spiral.Value;
        var widening = CurveRuleChecker.Find(_rules.Widening, input.Radius, speed)?.Value;
        if (widening != null)
        {
            input.Wb = _splitWidening ? widening.Value / 2 : widening.Value;
            if (_splitWidening) input.Wl = widening.Value / 2;
        }
    }

    public void Recalculate()
    {
        if (_pis.Count < 2) return;
        Design = RouteDesigner.Design(_pis, _startStation, _inputs, _designSpeed, _rules);
        SuperelevationPlanner.Check(Design.Curves, _startStation, Design.EndStation);
        foreach (var row in Rows) row.Refresh();
        Raise(nameof(Design));
        Raise(nameof(CanApply));
        Raise(nameof(EndStation));
        Raise(nameof(SummaryText));
        Raise(nameof(HasSuperelevation));
    }

    internal double RouteSpeed => _designSpeed;
    internal double CrossSlopeValue => _crossSlope;

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
        nameof(SpeedText), nameof(DeflectionText), nameof(A1Text), nameof(A2Text), nameof(RmaxText), nameof(LmaxText),
        nameof(Superelevated), nameof(SuperRateText), nameof(RunoffOnSpiral), nameof(RunoffInText), nameof(RunoffOutText),
        nameof(OffsetInText), nameof(OffsetOutText), nameof(SuperelevationText), nameof(HeaderText),
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

    // The detail panel ("Hiệu chỉnh yếu tố cong và thông số siêu cao"), bound to the selected row.

    /// <summary>"Đỉnh: 1 · Đoạn trước 270.17 · Đoạn sau 252.21".</summary>
    public string HeaderText
    {
        get
        {
            var pis = _session.Pis;
            return $"{Name} · A = {AText} · đoạn trước {NumberFormat.Fixed(Distance(pis[PiIndex - 1], pis[PiIndex]), 2)} m · đoạn sau {NumberFormat.Fixed(Distance(pis[PiIndex], pis[PiIndex + 1]), 2)} m";
        }
    }

    /// <summary>"Tốc độ tại đỉnh": empty = the route's speed.</summary>
    public string SpeedText
    {
        get => _badText.TryGetValue(nameof(SpeedText), out var bad) ? bad : Input.DesignSpeed.HasValue ? NumberFormat.Trimmed(Input.DesignSpeed.Value, 0) : "";
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                _badText.Remove(nameof(SpeedText));
                Input.DesignSpeed = null;
                _session.Recalculate();
                return;
            }

            Set(nameof(SpeedText), value, v => Input.DesignSpeed = v, v => v > 0);
        }
    }

    /// <summary>"Hiệu chỉnh góc chuyển hướng": the deflection in decimal degrees; setting it turns the route after this PI.</summary>
    public string DeflectionText
    {
        get => _badText.TryGetValue(nameof(DeflectionText), out var bad) ? bad : NumberFormat.Trimmed(Curve.DeltaRadians * 180 / Math.PI, 6);
        set => Set(nameof(DeflectionText), value, v => _session.SetDeflection(Index, v), v => v > 0 && v < 180 && _session.CanEditDeflection && !_session.ReadOnlyGeometry);
    }

    /// <summary>"Nhập thông số A": A = √(R·L); typing A sets L = A²/R.</summary>
    public string A1Text { get => Text(nameof(A1Text), CurveLimits.SpiralParameter(Input.Radius, Input.SpiralIn)); set => Set(nameof(A1Text), value, v => Input.SpiralIn = CurveLimits.SpiralLength(Input.Radius, v)); }
    public string A2Text { get => Text(nameof(A2Text), CurveLimits.SpiralParameter(Input.Radius, Input.SpiralOut)); set => Set(nameof(A2Text), value, v => Input.SpiralOut = CurveLimits.SpiralLength(Input.Radius, v)); }

    /// <summary>"Rmax…": the largest R whose tangents fit between the neighbours with the current L1, L2.</summary>
    public string RmaxText
    {
        get
        {
            if (_session.Design == null) return "";
            CurveLimits.Available(_session.Pis, _session.Design, Curve, out var before, out var after);
            var r = CurveLimits.MaxRadius(Curve.DeltaRadians, Input.SpiralIn, Input.SpiralOut, before, after);
            return r == null ? "Rmax = – (không đủ chỗ)" : "Rmax = " + NumberFormat.Fixed(r.Value, 2);
        }
    }

    /// <summary>"Lmax…": the largest L1 = L2 whose tangents fit at the current R.</summary>
    public string LmaxText
    {
        get
        {
            if (_session.Design == null) return "";
            CurveLimits.Available(_session.Pis, _session.Design, Curve, out var before, out var after);
            var l = CurveLimits.MaxSpiral(Input.Radius, Curve.DeltaRadians, before, after);
            return l == null ? "Lmax = – (không đủ chỗ)" : "Lmax = " + NumberFormat.Fixed(l.Value, 2);
        }
    }

    /// <summary>"Siêu cao" (true) / "Không bố trí".</summary>
    public bool Superelevated
    {
        get => Input.Superelevated;
        set
        {
            if (Input.Superelevated == value) return;
            Input.Superelevated = value;
            _session.Recalculate();
        }
    }

    /// <summary>"Bố trí theo chuyển tiếp".</summary>
    public bool RunoffOnSpiral
    {
        get => Input.RunoffOnSpiral;
        set
        {
            if (Input.RunoffOnSpiral == value) return;
            Input.RunoffOnSpiral = value;
            _session.Recalculate();
        }
    }

    /// <summary>"i max" (isc, %).</summary>
    public string SuperRateText { get => Text(nameof(SuperRateText), Input.SuperRate); set => Set(nameof(SuperRateText), value, v => Input.SuperRate = v, v => v >= 0 && v <= 20); }

    /// <summary>"Chiều dài nối" / "Lệch ngoài" at the start (Nối đầu) and end (Nối cuối).</summary>
    public string RunoffInText { get => Text(nameof(RunoffInText), Input.RunoffIn); set => Set(nameof(RunoffInText), value, v => Input.RunoffIn = v, v => v >= 0); }
    public string RunoffOutText { get => Text(nameof(RunoffOutText), Input.RunoffOut); set => Set(nameof(RunoffOutText), value, v => Input.RunoffOut = v, v => v >= 0); }
    public string OffsetInText { get => Text(nameof(OffsetInText), Input.OffsetIn); set => Set(nameof(OffsetInText), value, v => Input.OffsetIn = v, v => v >= 0); }
    public string OffsetOutText { get => Text(nameof(OffsetOutText), Input.OffsetOut); set => Set(nameof(OffsetOutText), value, v => Input.OffsetOut = v, v => v >= 0); }

    /// <summary>Grid column "Siêu cao": "6%" or empty.</summary>
    public string SuperelevationText => Input.Superelevated ? NumberFormat.Trimmed(Math.Max(Input.SuperRate, _session.CrossSlopeValue), 2) + "%" : "";

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

    private void Set(string field, string text, Action<double> apply) => Set(field, text, apply, null);

    private void Set(string field, string text, Action<double> apply, Func<double, bool> valid)
    {
        if (NumberInput.TryParse(text, out var value) && (valid == null || valid(value)))
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

    private static double Distance(PlanPoint a, PlanPoint b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

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
