using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using C3DTools.Core.Curves;
using C3DTools.Core.Presets;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Geodesy;

public enum Vn2000Target { Selection, Cogo }

/// <summary>A reference point the host will move: vertex, insertion point, centre (Radius &gt; 0 for arcs and circles) or COGO point.</summary>
public sealed class Vn2000Item
{
    public Vn2000Item(string name, double x, double y, double radius = 0)
    {
        Name = name ?? "";
        X = x;
        Y = y;
        Radius = radius;
    }

    public string Name { get; }
    public double X { get; }
    public double Y { get; }
    public double Radius { get; }
}

/// <summary>One line of the CTVN2000 preview grid.</summary>
public sealed class Vn2000PreviewRow
{
    public Vn2000PreviewRow(string name, string xBefore, string yBefore, string xAfter, string yAfter, string shift)
    {
        Name = name;
        XBefore = xBefore;
        YBefore = yBefore;
        XAfter = xAfter;
        YAfter = yAfter;
        Shift = shift;
    }

    public string Name { get; }
    public string XBefore { get; }
    public string YBefore { get; }
    public string XAfter { get; }
    public string YAfter { get; }
    public string Shift { get; }
}

/// <summary>
/// State of the CTVN2000 dialog: from/to meridian (a province from the preset or typed "105°45'"), zone width 3°/6° per
/// side, the target (selection or every COGO point), and a preview of the first 5 points with the max shift and distortion.
/// </summary>
public sealed class Vn2000Session : INotifyPropertyChanged
{
    public const int PreviewCount = 5;

    private readonly List<Vn2000Province> _provinces;
    private string _fromText = "", _toText = "", _selectionText;
    private int _fromZone = 3, _toZone = 3;
    private Vn2000Target _target = Vn2000Target.Selection;
    private List<Vn2000Item> _selection = new List<Vn2000Item>();
    private List<Vn2000Item> _cogo = new List<Vn2000Item>();
    private bool _hasSelection, _hasCogo, _statisticsStale;

    public Vn2000Session(ProjectPreset preset)
    {
        _provinces = (preset?.Vn2000?.Provinces ?? new List<Vn2000Province>())
            .Where(p => p != null && !string.IsNullOrWhiteSpace(p.Province)).ToList();
        MeridianChoices = _provinces.Select(Choice).ToList();
    }

    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>"Hà Nội 105°00'" per preset province; the combo is editable, any meridian can be typed.</summary>
    public IReadOnlyList<string> MeridianChoices { get; }

    public IReadOnlyList<int> ZoneWidths { get; } = new[] { 3, 6 };

    public bool HasUnverifiedProvinces => _provinces.Any(p => !p.Verified);

    public string FromText
    {
        get => _fromText;
        set { _fromText = value ?? ""; Raise(nameof(FromText)); Raise(nameof(FromMeridian)); Raise(nameof(FromIsInvalid)); RecalculatePreview(); }
    }

    public string ToText
    {
        get => _toText;
        set { _toText = value ?? ""; Raise(nameof(ToText)); Raise(nameof(ToMeridian)); Raise(nameof(ToIsInvalid)); RecalculatePreview(); }
    }

    public double? FromMeridian => Resolve(_fromText);
    public double? ToMeridian => Resolve(_toText);
    public bool FromIsInvalid => FromMeridian == null;
    public bool ToIsInvalid => ToMeridian == null;

    public int FromZoneWidth
    {
        get => _fromZone;
        set { if (value != 3 && value != 6 || value == _fromZone) return; _fromZone = value; Raise(nameof(FromZoneWidth)); Recalculate(); }
    }

    public int ToZoneWidth
    {
        get => _toZone;
        set { if (value != 3 && value != 6 || value == _toZone) return; _toZone = value; Raise(nameof(ToZoneWidth)); Recalculate(); }
    }

    public Vn2000Target Target
    {
        get => _target;
        set
        {
            if (_target == value) return;
            _target = value;
            Raise(nameof(Target));
            Raise(nameof(TargetSelection));
            Raise(nameof(TargetCogo));
            Raise(nameof(SourceText));
            Recalculate();
        }
    }

    /// <summary>Radio button "Đối tượng chọn".</summary>
    public bool TargetSelection { get => _target == Vn2000Target.Selection; set { if (value) Target = Vn2000Target.Selection; } }

    /// <summary>Radio button "Tất cả điểm COGO".</summary>
    public bool TargetCogo { get => _target == Vn2000Target.Cogo; set { if (value) Target = Vn2000Target.Cogo; } }

    public string SourceText => _target == Vn2000Target.Cogo
        ? (_hasCogo ? _cogo.Count.ToString(CultureInfo.InvariantCulture) + " điểm COGO trong bản vẽ" : "không đọc được điểm COGO")
        : (_hasSelection ? _selectionText : "chưa chọn");

    public IReadOnlyList<Vn2000Item> Items => _target == Vn2000Target.Cogo ? _cogo : _selection;

    public ObservableCollection<Vn2000PreviewRow> Rows { get; } = new ObservableCollection<Vn2000PreviewRow>();

    /// <summary>m, over every point of the target.</summary>
    public double MaxShift { get; private set; }

    /// <summary>ppm: max |k_to/k_from − 1|.</summary>
    public double MaxScaleDistortion { get; private set; }

    /// <summary>m: max radius × |k_to/k_from − 1| over arcs and circles (their radius is kept).</summary>
    public double MaxRadiusError { get; private set; }

    /// <summary>Max rotation change (radians) at a point; applied to block rotation.</summary>
    public double MaxRotation { get; private set; }

    public string DistortionText
    {
        get
        {
            if (Transform() == null || Items.Count == 0) return "";
            if (_statisticsStale) return "Dịch chuyển và biến dạng lớn nhất được tính lại khi Xem trước / Áp dụng";
            var text = "Dịch chuyển lớn nhất " + NumberFormat.Fixed(MaxShift, 3) + " m · biến dạng tỷ lệ lớn nhất "
                + NumberFormat.Fixed(MaxScaleDistortion, 1) + " ppm · xoay " + NumberFormat.Fixed(MaxRotation * 180 / Math.PI * 3600, 1) + "\"";
            if (Items.Any(i => i.Radius > 0))
                text += " · cung/tròn giữ bán kính, sai lệch bán kính tới " + NumberFormat.Fixed(MaxRadiusError * 1000, 1) + " mm";
            return text;
        }
    }

    public bool CanApply
    {
        get
        {
            var t = Transform();
            return t != null && !t.IsIdentity && Items.Count > 0;
        }
    }

    public string SummaryText
    {
        get
        {
            if (FromIsInvalid) return "Kinh tuyến nguồn không hợp lệ (ví dụ 105°45')";
            if (ToIsInvalid) return "Kinh tuyến đích không hợp lệ (ví dụ 106°15')";
            if (Transform().IsIdentity) return "Kinh tuyến và múi chiếu nguồn, đích trùng nhau";
            if (_target == Vn2000Target.Selection && !_hasSelection) return "Chưa chọn đối tượng";
            if (Items.Count == 0) return "Không có điểm nào để chuyển";
            return Items.Count.ToString(CultureInfo.InvariantCulture) + " điểm: " + Describe(FromMeridian.Value, _fromZone)
                + " → " + Describe(ToMeridian.Value, _toZone);
        }
    }

    /// <summary>The meridian change, or null while a meridian is invalid.</summary>
    public Vn2000Transform Transform()
    {
        var from = FromMeridian;
        var to = ToMeridian;
        return from == null || to == null ? null : new Vn2000Transform(from.Value, _fromZone, to.Value, _toZone);
    }

    public void SetSelection(string description, IEnumerable<Vn2000Item> items)
    {
        _selectionText = description ?? "";
        _selection = (items ?? Enumerable.Empty<Vn2000Item>()).Where(i => i != null).ToList();
        _hasSelection = true;
        Raise(nameof(SourceText));
        Recalculate();
    }

    public void SetCogo(IEnumerable<Vn2000Item> items)
    {
        _cogo = (items ?? Enumerable.Empty<Vn2000Item>()).Where(i => i != null).ToList();
        _hasCogo = items != null;
        Raise(nameof(SourceText));
        Recalculate();
    }

    private static string Describe(double meridian, int zone) =>
        MeridianText.Format(meridian) + " múi " + zone.ToString(CultureInfo.InvariantCulture) + "°";

    private static string Choice(Vn2000Province p) => p.Province + " " + MeridianText.Format(p.Meridian);

    private double? Resolve(string text)
    {
        var t = (text ?? "").Trim();
        if (t.Length == 0) return null;
        foreach (var p in _provinces)
        {
            if (string.Equals(t, Choice(p), StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, p.Province.Trim(), StringComparison.OrdinalIgnoreCase))
                return p.Meridian;
        }

        return MeridianText.TryParse(t, out var deg) ? deg : (double?)null;
    }

    /// <summary>Full pass over every point (Xem trước / Áp dụng, a new selection, target or zone): rows and statistics.</summary>
    public void UpdateStatistics() => Recalculate();

    private void Recalculate()
    {
        MaxShift = MaxScaleDistortion = MaxRadiusError = MaxRotation = 0;
        var t = Transform();
        if (t != null)
        {
            foreach (var item in Items)
            {
                var p = t.Apply(item.X, item.Y);
                var distortion = Math.Abs(t.ScaleRatio(item.X, item.Y) - 1);
                MaxShift = Math.Max(MaxShift, Shift(item, p));
                MaxScaleDistortion = Math.Max(MaxScaleDistortion, distortion * 1e6);
                MaxRotation = Math.Max(MaxRotation, Math.Abs(t.RotationDelta(item.X, item.Y)));
                if (item.Radius > 0) MaxRadiusError = Math.Max(MaxRadiusError, item.Radius * distortion);
            }
        }

        _statisticsStale = false;
        Raise(nameof(MaxShift));
        Raise(nameof(MaxScaleDistortion));
        Raise(nameof(MaxRadiusError));
        Raise(nameof(MaxRotation));
        RecalculatePreview(stale: false);
    }

    /// <summary>While a meridian is typed: only the first PreviewCount rows; the statistics wait for UpdateStatistics.</summary>
    private void RecalculatePreview(bool stale = true)
    {
        if (stale) _statisticsStale = true;
        Rows.Clear();
        var t = Transform();
        if (t != null)
        {
            foreach (var item in Items.Take(PreviewCount))
            {
                var p = t.Apply(item.X, item.Y);
                Rows.Add(new Vn2000PreviewRow(item.Name, NumberFormat.Fixed(item.X, 3), NumberFormat.Fixed(item.Y, 3),
                    NumberFormat.Fixed(p.X, 3), NumberFormat.Fixed(p.Y, 3), NumberFormat.Fixed(Shift(item, p), 3)));
            }
        }

        Raise(nameof(Items));
        Raise(nameof(DistortionText));
        Raise(nameof(CanApply));
        Raise(nameof(SummaryText));
    }

    private static double Shift(Vn2000Item item, PlanPoint p) =>
        Math.Sqrt((p.X - item.X) * (p.X - item.X) + (p.Y - item.Y) * (p.Y - item.Y));

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
