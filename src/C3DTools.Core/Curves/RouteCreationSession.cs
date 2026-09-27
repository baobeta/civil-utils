using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using C3DTools.Core.Presets;
using C3DTools.Core.Stations;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Curves;

/// <summary>
/// State of the CTTUYEN dialog ("Tạo tuyến cho bình đồ mới"): name, scale, start station, speed, styles, layer,
/// ground surface, cross-section assembly, and the centreline source (a polyline or picked points).
/// </summary>
public sealed class RouteCreationSession : INotifyPropertyChanged
{
    public const string NoSurface = "(Không tạo trắc dọc tự nhiên)";
    public const string NoAssembly = "(Chưa chọn)";

    /// <summary>mm on paper of stake and curve-box text; the drawing unit is the metre.</summary>
    public const double PaperTextHeight = 2.5;

    private static readonly double[] FallbackSpeeds = { 20, 30, 40, 60, 80, 100, 120 };

    private readonly HashSet<string> _existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private string _name = "Tuyen1", _description = "", _scaleText = "1000", _startText = "0", _layer = "TUYEN", _sectionFile = "";
    private double _designSpeed;
    private int _styleIndex, _labelSetIndex, _surfaceIndex, _assemblyIndex;
    private List<string> _styles = new List<string>(), _labelSets = new List<string>(), _surfaces = new List<string> { NoSurface };
    private List<string> _drawingAssemblies = new List<string>(), _fileAssemblies = new List<string>();
    private List<string> _assemblies = new List<string> { NoAssembly };
    private bool _loadAllAssemblies = true, _openCurveDesign = true, _isPolyline;
    private string _sourceText = "";
    private IReadOnlyList<PlanPoint> _points = new PlanPoint[0];

    public RouteCreationSession(ProjectPreset preset)
    {
        _designSpeed = preset?.DesignSpeed ?? 60;
        var rules = preset?.CurveRules;
        var speeds = rules != null && rules.MinRadius.Count > 0 ? rules.MinRadius.Select(r => r.DesignSpeed) : FallbackSpeeds;
        AvailableSpeeds = speeds.Concat(new[] { _designSpeed }).Distinct().OrderBy(v => v).ToList();
    }

    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>"Tên đường tuyến"; must not be an existing alignment's name.</summary>
    public string Name { get => _name; set => SetText(ref _name, value, nameof(Name), nameof(IsNameValid)); }
    public bool IsNameValid => _name.Trim().Length > 0 && !_existingNames.Contains(_name.Trim());

    /// <summary>"Mô tả": written into the alignment's description.</summary>
    public string Description { get => _description; set => SetText(ref _description, value, nameof(Description)); }

    /// <summary>"Tỉ lệ bình đồ 1/…".</summary>
    public string ScaleText { get => _scaleText; set => SetText(ref _scaleText, value, nameof(ScaleText), nameof(Scale), nameof(IsScaleValid), nameof(TextHeight), nameof(TextHeightText)); }
    public double Scale => NumberInput.TryParse(_scaleText, out var v) && v > 0 ? v : double.NaN;
    public bool IsScaleValid => !double.IsNaN(Scale);

    /// <summary>Text height in drawing units for CTYTC on this route: 2.5 mm at the plan scale.</summary>
    public double TextHeight => IsScaleValid ? PaperTextHeight * Scale / 1000 : double.NaN;
    public string TextHeightText => IsScaleValid ? $"Chữ cọc, khung cong: {NumberFormat.Trimmed(TextHeight, 3)} (2.5 mm ở 1/{NumberFormat.Trimmed(Scale, 0)})" : "";

    /// <summary>"Lý trình đầu": "Km0+000", "0+000" or a number.</summary>
    public string StartStationText { get => _startText; set => SetText(ref _startText, value, nameof(StartStationText), nameof(StartStation), nameof(IsStartValid)); }
    public double StartStation => StakeGenerationSession.ParseStation(_startText);
    public bool IsStartValid => !double.IsNaN(StartStation) && StartStation >= 0;

    public IReadOnlyList<double> AvailableSpeeds { get; }
    public double DesignSpeed { get => _designSpeed; set { if (value > 0) Set(ref _designSpeed, value, nameof(DesignSpeed)); } }

    public IReadOnlyList<string> StyleNames => _styles;
    public int StyleIndex { get => _styleIndex; set => SetIndex(ref _styleIndex, value, _styles, nameof(StyleIndex)); }
    public IReadOnlyList<string> LabelSetNames => _labelSets;
    public int LabelSetIndex { get => _labelSetIndex; set => SetIndex(ref _labelSetIndex, value, _labelSets, nameof(LabelSetIndex)); }

    /// <summary>Layer of the alignment; created when missing.</summary>
    public string LayerName { get => _layer; set => SetText(ref _layer, value, nameof(LayerName), nameof(IsLayerValid)); }
    public bool IsLayerValid => _layer.Trim().Length > 0 && _layer.IndexOfAny(new[] { '<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', ',', '=', '`' }) < 0;

    /// <summary>NoSurface first: a ground profile ("trắc dọc tự nhiên") is made from the chosen surface.</summary>
    public IReadOnlyList<string> SurfaceNames => _surfaces;
    public int SurfaceIndex { get => _surfaceIndex; set => SetIndex(ref _surfaceIndex, value, _surfaces, nameof(SurfaceIndex), nameof(Surface)); }
    public string Surface => _surfaceIndex > 0 ? _surfaces[_surfaceIndex] : null;

    /// <summary>"Tệp mặt cắt": a DWG holding cross-section assemblies.</summary>
    public string SectionFile { get => _sectionFile; set => SetText(ref _sectionFile, value, nameof(SectionFile)); }

    /// <summary>"Tải toàn bộ mặt cắt trong tệp": import every assembly of the file, not only the chosen one.</summary>
    public bool LoadAllAssemblies { get => _loadAllAssemblies; set => Set(ref _loadAllAssemblies, value, nameof(LoadAllAssemblies)); }

    /// <summary>NoAssembly, the drawing's assemblies, then the file's ones not in the drawing yet.</summary>
    public IReadOnlyList<string> AssemblyNames => _assemblies;
    public int AssemblyIndex { get => _assemblyIndex; set => SetIndex(ref _assemblyIndex, value, _assemblies, nameof(AssemblyIndex), nameof(Assembly), nameof(AssemblyFromFile)); }

    /// <summary>"Mặt cắt cho tuyến"; null for none.</summary>
    public string Assembly => _assemblyIndex > 0 ? _assemblies[_assemblyIndex] : null;

    /// <summary>The chosen assembly has to be imported from SectionFile first.</summary>
    public bool AssemblyFromFile => Assembly != null && !_drawingAssemblies.Contains(Assembly, StringComparer.OrdinalIgnoreCase);

    /// <summary>The assemblies to import from SectionFile: all of the file's new ones, or only the chosen one.</summary>
    public IReadOnlyList<string> AssembliesToImport =>
        _loadAllAssemblies ? _fileAssemblies.Where(n => !_drawingAssemblies.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList()
        : AssemblyFromFile ? new List<string> { Assembly } : new List<string>();

    /// <summary>"Bố trí cong ngay": open CTYTC on the new alignment after Áp dụng.</summary>
    public bool OpenCurveDesign { get => _openCurveDesign; set => Set(ref _openCurveDesign, value, nameof(OpenCurveDesign)); }

    public string SourceText => _points.Count > 0 || _isPolyline ? _sourceText : "chưa chọn";
    public bool HasSource => _isPolyline || _points.Count >= 2;

    /// <summary>The centreline comes from an existing polyline ("Theo polyline…"); false for picked points ("Chỉ điểm…").</summary>
    public bool IsPolyline => _isPolyline;
    public IReadOnlyList<PlanPoint> PickedPoints => _points;

    public bool CanApply => HasSource && IsNameValid && IsScaleValid && IsStartValid && IsLayerValid;

    public string SummaryText
    {
        get
        {
            if (!IsNameValid) return _name.Trim().Length == 0 ? "Nhập tên tuyến" : $"Đã có alignment tên {_name.Trim()}";
            if (!IsScaleValid) return "Tỉ lệ phải là số lớn hơn 0";
            if (!IsStartValid) return "Lý trình đầu không hợp lệ";
            if (!IsLayerValid) return "Tên layer không hợp lệ";
            if (!HasSource) return "Bấm Theo polyline… hoặc Chỉ điểm… để có tim tuyến";
            return $"Tạo alignment {_name.Trim()} từ {_sourceText}";
        }
    }

    /// <summary>The drawing's alignment names (for the unique-name check), styles, label sets, surfaces and assemblies.</summary>
    public void SetDrawing(IEnumerable<string> alignmentNames, IEnumerable<string> styles, string preferredStyle,
        IEnumerable<string> labelSets, string preferredLabelSet, IEnumerable<string> surfaces, IEnumerable<string> assemblies)
    {
        _existingNames.Clear();
        foreach (var n in alignmentNames ?? Enumerable.Empty<string>()) if (!string.IsNullOrEmpty(n)) _existingNames.Add(n.Trim());
        for (var i = 1; !IsNameValid && _name.StartsWith("Tuyen", StringComparison.Ordinal) && i < 1000; i++) _name = "Tuyen" + i;

        _styles = Clean(styles);
        _styleIndex = Math.Max(0, _styles.FindIndex(n => string.Equals(n, preferredStyle, StringComparison.OrdinalIgnoreCase)));
        _labelSets = Clean(labelSets);
        _labelSetIndex = Math.Max(0, _labelSets.FindIndex(n => string.Equals(n, preferredLabelSet, StringComparison.OrdinalIgnoreCase)));
        _surfaces = new List<string> { NoSurface };
        _surfaces.AddRange(Clean(surfaces));
        _surfaceIndex = 0;
        _drawingAssemblies = Clean(assemblies);
        RebuildAssemblies();
        foreach (var name in new[] { nameof(Name), nameof(IsNameValid), nameof(StyleNames), nameof(StyleIndex), nameof(LabelSetNames), nameof(LabelSetIndex),
                     nameof(SurfaceNames), nameof(SurfaceIndex), nameof(Surface) })
            Raise(name);
        Changed();
    }

    /// <summary>The assemblies found in SectionFile (the dialog's "…" button).</summary>
    public void SetFileAssemblies(string path, IEnumerable<string> names)
    {
        _sectionFile = path ?? "";
        _fileAssemblies = Clean(names);
        RebuildAssemblies();
        Raise(nameof(SectionFile));
        if (_assemblyIndex == 0 && _fileAssemblies.Count > 0) AssemblyIndex = _assemblies.IndexOf(_fileAssemblies[0]);
    }

    /// <summary>"Theo polyline…": the picked polyline.</summary>
    public void SetPolylineSource(string description)
    {
        _isPolyline = true;
        _points = new PlanPoint[0];
        _sourceText = description ?? "polyline";
        SourceChanged();
    }

    /// <summary>"Chỉ điểm…": the picked points (duplicates removed).</summary>
    public void SetPickedPoints(IEnumerable<PlanPoint> points)
    {
        _isPolyline = false;
        _points = RouteDesigner.RemoveDuplicatePoints(points ?? Enumerable.Empty<PlanPoint>());
        _sourceText = $"{_points.Count} điểm đã chỉ";
        SourceChanged();
    }

    private void RebuildAssemblies()
    {
        var chosen = Assembly;
        _assemblies = new List<string> { NoAssembly };
        _assemblies.AddRange(_drawingAssemblies);
        _assemblies.AddRange(_fileAssemblies.Where(n => !_drawingAssemblies.Contains(n, StringComparer.OrdinalIgnoreCase)));
        var index = chosen == null ? 0 : _assemblies.IndexOf(chosen);
        _assemblyIndex = index < 0 ? 0 : index;
        Raise(nameof(AssemblyNames));
        Raise(nameof(AssemblyIndex));
        Raise(nameof(Assembly));
        Raise(nameof(AssemblyFromFile));
    }

    private void SourceChanged()
    {
        Raise(nameof(SourceText));
        Raise(nameof(HasSource));
        Raise(nameof(IsPolyline));
        Raise(nameof(PickedPoints));
        Changed();
    }

    private static List<string> Clean(IEnumerable<string> names) =>
        (names ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private void SetIndex(ref int field, int value, List<string> list, params string[] names)
    {
        if (value < 0 || value >= Math.Max(1, list.Count) || field == value) return;
        field = value;
        foreach (var n in names) Raise(n);
        Changed();
    }

    private void SetText(ref string field, string value, params string[] names)
    {
        value ??= "";
        if (field == value) return;
        field = value;
        foreach (var n in names) Raise(n);
        Changed();
    }

    private void Set<T>(ref T field, T value, string name)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Raise(name);
        Changed();
    }

    private void Changed()
    {
        Raise(nameof(CanApply));
        Raise(nameof(SummaryText));
    }

    private void Raise(string name) => C3DTools.Core.Ui.NotifyGuard.Raise(this, PropertyChanged, name);
}
