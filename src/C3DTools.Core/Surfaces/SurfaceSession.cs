using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using C3DTools.Core.Curves;
using C3DTools.Core.Presets;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Surfaces;

/// <summary>
/// State of the CTMATDIA dialog. Tab 0 "Xoá tam giác dài": max edge and/or a boundary polyline, "Đếm" result.
/// Tab 1 "Ghi cao độ đồng mức": picked lines, major/minor interval, text height, spacing.
/// </summary>
public sealed class SurfaceSession : INotifyPropertyChanged
{
    public const int CleanupTab = 0;
    public const int LabelTab = 1;

    private readonly int _decimals;
    private int _tab, _surfaceIndex = -1, _lineCount;
    private List<string> _surfaceNames = new List<string>();
    private string _maxEdgeText, _majorText, _minorText, _heightText, _spacingText;
    private bool _useMaxEdge = true, _labelMinor = true, _intervalsFromStyle;
    private string _boundaryText, _linesText, _countText;

    public SurfaceSession(ProjectPreset preset)
    {
        var s = preset?.Surface ?? new SurfaceOptions();
        _maxEdgeText = Text(s.MaxEdgeLength);
        _majorText = Text(s.MajorInterval);
        _minorText = Text(s.MinorInterval);
        _heightText = Text(s.ContourTextHeight);
        _spacingText = Text(s.ContourLabelSpacing);
        _decimals = s.ContourDecimals;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public int TabIndex
    {
        get => _tab;
        set { if (value != CleanupTab && value != LabelTab || value == _tab) return; _tab = value; Raise(nameof(TabIndex)); Raise(nameof(SourceText)); Changed(); }
    }

    public IReadOnlyList<string> SurfaceNames => _surfaceNames;

    public int SurfaceIndex
    {
        get => _surfaceIndex;
        set
        {
            if (value < 0 || value >= _surfaceNames.Count || value == _surfaceIndex) return;
            _surfaceIndex = value;
            _countText = null;
            Raise(nameof(SurfaceIndex));
            Raise(nameof(Surface));
            Raise(nameof(CountText));
            Changed();
        }
    }

    public string Surface => _surfaceIndex >= 0 ? _surfaceNames[_surfaceIndex] : null;

    /// <summary>What "Chọn trên bản vẽ…" picks on the current tab, and what has been picked.</summary>
    public string SourceText => _tab == CleanupTab
        ? "Ranh giới: " + (_boundaryText ?? "không (chỉ lọc theo cạnh dài)")
        : "Đường cắt đồng mức: " + (_linesText ?? "chưa chọn");

    // ---- Tab 0 ----

    public bool UseMaxEdge { get => _useMaxEdge; set { if (_useMaxEdge == value) return; _useMaxEdge = value; ResetCount(); Raise(nameof(UseMaxEdge)); Changed(); } }

    public string MaxEdgeText { get => _maxEdgeText; set { _maxEdgeText = value ?? ""; ResetCount(); Raise(nameof(MaxEdgeText)); Raise(nameof(IsMaxEdgeValid)); Changed(); } }

    public bool IsMaxEdgeValid => !_useMaxEdge || Positive(_maxEdgeText).HasValue;

    /// <summary>m; 0 when the length rule is off.</summary>
    public double MaxEdge => _useMaxEdge ? Positive(_maxEdgeText) ?? 0 : 0;

    public bool HasBoundary => _boundaryText != null;

    /// <summary>Result of "Đếm", or a hint; reset whenever an option changes.</summary>
    public string CountText => _countText ?? "Bấm Đếm để xem số tam giác sẽ xoá";

    public void SetBoundary(string description)
    {
        _boundaryText = description;
        ResetCount();
        Raise(nameof(HasBoundary));
        Raise(nameof(SourceText));
        Changed();
    }

    public void ClearBoundary() => SetBoundary(null);

    public void SetCount(int triangles, int flagged, int edges)
    {
        _countText = flagged.ToString(CultureInfo.InvariantCulture) + " / " + triangles.ToString(CultureInfo.InvariantCulture)
            + " tam giác bị loại, " + edges.ToString(CultureInfo.InvariantCulture) + " cạnh sẽ xoá";
        Raise(nameof(CountText));
    }

    // ---- Tab 1 ----

    public string MajorIntervalText { get => _majorText; set { _majorText = value ?? ""; _intervalsFromStyle = false; Raise(nameof(MajorIntervalText)); Raise(nameof(IsMajorValid)); Raise(nameof(IntervalSourceText)); Changed(); } }
    public string MinorIntervalText { get => _minorText; set { _minorText = value ?? ""; _intervalsFromStyle = false; Raise(nameof(MinorIntervalText)); Raise(nameof(IsMinorValid)); Raise(nameof(IntervalSourceText)); Changed(); } }
    public string TextHeightText { get => _heightText; set { _heightText = value ?? ""; Raise(nameof(TextHeightText)); Raise(nameof(IsTextHeightValid)); Changed(); } }
    public string SpacingText { get => _spacingText; set { _spacingText = value ?? ""; Raise(nameof(SpacingText)); Raise(nameof(IsSpacingValid)); Changed(); } }

    public bool IsMajorValid => Positive(_majorText).HasValue;
    public bool IsMinorValid => Positive(_minorText).HasValue;
    public bool IsTextHeightValid => Positive(_heightText).HasValue;
    public bool IsSpacingValid => NumberInput.TryParse(_spacingText, out var v) && v >= 0;

    public double MajorInterval => Positive(_majorText) ?? 0;
    public double MinorInterval => Positive(_minorText) ?? 0;
    public double TextHeight => Positive(_heightText) ?? 0;

    /// <summary>"Ghi cả đồng mức con".</summary>
    public bool LabelMinor { get => _labelMinor; set { if (_labelMinor == value) return; _labelMinor = value; Raise(nameof(LabelMinor)); Changed(); } }

    public string IntervalSourceText => _intervalsFromStyle ? "theo kiểu hiển thị của mặt phủ" : "theo preset / nhập tay";

    public int LineCount => _lineCount;

    /// <summary>Major/minor interval read from the surface style; kept until the user types another.</summary>
    public void SetIntervalsFromStyle(double major, double minor)
    {
        if (!(major > 0) || !(minor > 0)) return;
        MajorIntervalText = Text(major);
        MinorIntervalText = Text(minor);
        _intervalsFromStyle = true;
        Raise(nameof(IntervalSourceText));
    }

    public void SetLines(string description, int count)
    {
        _linesText = count > 0 ? description : null;
        _lineCount = Math.Max(0, count);
        Raise(nameof(LineCount));
        Raise(nameof(SourceText));
        Changed();
    }

    public ContourLabelOptions LabelOptions() => new ContourLabelOptions
    {
        MajorInterval = MajorInterval,
        Decimals = _decimals,
        Spacing = NumberInput.TryParse(_spacingText, out var s) && s > 0 ? s : 0,
        IncludeMinor = _labelMinor,
    };

    // ---- both ----

    public void SetSurfaces(IEnumerable<string> names, string preferred = null)
    {
        var current = Surface ?? preferred;
        _surfaceNames = (names ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n)).ToList();
        var index = current == null ? -1 : _surfaceNames.IndexOf(current);
        _surfaceIndex = index >= 0 ? index : _surfaceNames.Count > 0 ? 0 : -1;
        _countText = null;
        Raise(nameof(SurfaceNames));
        Raise(nameof(SurfaceIndex));
        Raise(nameof(Surface));
        Raise(nameof(CountText));
        Changed();
    }

    public bool CanApply
    {
        get
        {
            if (Surface == null) return false;
            if (_tab == CleanupTab) return IsMaxEdgeValid && (MaxEdge > 0 || HasBoundary);
            return _lineCount > 0 && IsMajorValid && IsMinorValid && IsTextHeightValid && IsSpacingValid;
        }
    }

    public string SummaryText
    {
        get
        {
            if (_surfaceNames.Count == 0) return "Bản vẽ không có mặt phủ TIN";
            if (Surface == null) return "Chọn mặt phủ";
            if (_tab == CleanupTab)
            {
                if (!IsMaxEdgeValid) return "Cạnh dài nhất phải là số dương";
                if (!(MaxEdge > 0) && !HasBoundary) return "Nhập cạnh dài nhất hoặc chọn ranh giới";
                var rules = new List<string>();
                if (MaxEdge > 0) rules.Add("cạnh > " + NumberFormat.Trimmed(MaxEdge, 3) + " m");
                if (HasBoundary) rules.Add("đỉnh ngoài ranh giới");
                return "Xoá tam giác có " + string.Join(" hoặc ", rules) + " trên " + Surface;
            }

            if (!IsMajorValid || !IsMinorValid) return "Khoảng cao đều phải là số dương";
            if (!IsTextHeightValid) return "Chiều cao chữ phải là số dương";
            if (!IsSpacingValid) return "Khoảng cách nhãn phải là số không âm";
            if (_lineCount == 0) return "Chọn đường cắt qua các đường đồng mức";
            return _lineCount.ToString(CultureInfo.InvariantCulture) + " đường, đồng mức " + NumberFormat.Trimmed(MinorInterval, 3)
                + " m (chính " + NumberFormat.Trimmed(MajorInterval, 3) + " m) trên " + Surface;
        }
    }

    private static string Text(double v) => NumberFormat.Trimmed(v, 3);

    private static double? Positive(string text) => NumberInput.TryParse(text, out var v) && v > 0 ? v : (double?)null;

    private void ResetCount()
    {
        _countText = null;
        Raise(nameof(CountText));
    }

    private void Changed()
    {
        Raise(nameof(CanApply));
        Raise(nameof(SummaryText));
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
