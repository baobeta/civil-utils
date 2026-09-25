using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using C3DTools.Core.Curves;
using C3DTools.Core.Presets;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Sections;

/// <summary>
/// State of the CTXEPTRANG dialog: the section views (size and station, in station order), paper size, margins,
/// columns × rows, gap and scale (all editable, from the preset's SheetLayout), the Khung output, and the sheet count.
/// </summary>
public sealed class SheetArrangeSession : INotifyPropertyChanged
{
    private readonly string _paper;
    private string _sourceText = "";
    private string _widthText, _heightText, _marginLeftText, _marginRightText, _marginTopText, _marginBottomText, _columnsText, _rowsText, _gapText, _scaleText;
    private bool _writeFrames = true;
    private List<(double Width, double Height)> _sizes = new List<(double, double)>();
    private List<double> _stations = new List<double>();

    public SheetArrangeSession(ProjectPreset preset)
    {
        var o = preset?.SheetLayout ?? new SheetLayoutOptions();
        _paper = string.IsNullOrEmpty(o.Paper) ? "" : o.Paper;
        _widthText = T(o.Width);
        _heightText = T(o.Height);
        _marginLeftText = T(o.MarginLeft);
        _marginRightText = T(o.MarginRight);
        _marginTopText = T(o.MarginTop);
        _marginBottomText = T(o.MarginBottom);
        _columnsText = o.Columns.ToString(CultureInfo.InvariantCulture);
        _rowsText = o.Rows.ToString(CultureInfo.InvariantCulture);
        _gapText = T(o.Gap);
        _scaleText = T(o.Scale);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>Preset paper name, e.g. "A3".</summary>
    public string Paper => _paper;

    public string SourceText => ViewCount > 0 ? _sourceText : "chưa chọn";
    public int ViewCount => _sizes.Count;
    public bool HasSource => _sizes.Count > 0;

    public string WidthText { get => _widthText; set => SetText(ref _widthText, value, nameof(WidthText)); }
    public string HeightText { get => _heightText; set => SetText(ref _heightText, value, nameof(HeightText)); }
    public string MarginLeftText { get => _marginLeftText; set => SetText(ref _marginLeftText, value, nameof(MarginLeftText)); }
    public string MarginRightText { get => _marginRightText; set => SetText(ref _marginRightText, value, nameof(MarginRightText)); }
    public string MarginTopText { get => _marginTopText; set => SetText(ref _marginTopText, value, nameof(MarginTopText)); }
    public string MarginBottomText { get => _marginBottomText; set => SetText(ref _marginBottomText, value, nameof(MarginBottomText)); }
    public string ColumnsText { get => _columnsText; set => SetText(ref _columnsText, value, nameof(ColumnsText)); }
    public string RowsText { get => _rowsText; set => SetText(ref _rowsText, value, nameof(RowsText)); }
    public string GapText { get => _gapText; set => SetText(ref _gapText, value, nameof(GapText)); }

    /// <summary>1:Scale.</summary>
    public string ScaleText { get => _scaleText; set => SetText(ref _scaleText, value, nameof(ScaleText)); }

    public bool IsWidthValid => Positive(_widthText);
    public bool IsHeightValid => Positive(_heightText);
    public bool IsMarginLeftValid => NonNegative(_marginLeftText);
    public bool IsMarginRightValid => NonNegative(_marginRightText);
    public bool IsMarginTopValid => NonNegative(_marginTopText);
    public bool IsMarginBottomValid => NonNegative(_marginBottomText);
    public bool IsColumnsValid => Count(_columnsText) >= 1;
    public bool IsRowsValid => Count(_rowsText) >= 1;
    public bool IsGapValid => NonNegative(_gapText);
    public bool IsScaleValid => Positive(_scaleText);

    /// <summary>"Khung": a sheet frame and title per sheet.</summary>
    public bool WriteFrames
    {
        get => _writeFrames;
        set
        {
            if (_writeFrames == value) return;
            _writeFrames = value;
            Raise(nameof(WriteFrames));
            Changed();
        }
    }

    /// <summary>The layout from the fields, or null while a field is invalid.</summary>
    public SheetLayoutOptions Layout()
    {
        if (!(IsWidthValid && IsHeightValid && IsMarginLeftValid && IsMarginRightValid && IsMarginTopValid && IsMarginBottomValid
              && IsColumnsValid && IsRowsValid && IsGapValid)) return null;
        return new SheetLayoutOptions
        {
            Paper = _paper,
            Width = N(_widthText),
            Height = N(_heightText),
            MarginLeft = N(_marginLeftText),
            MarginRight = N(_marginRightText),
            MarginTop = N(_marginTopText),
            MarginBottom = N(_marginBottomText),
            Columns = Count(_columnsText),
            Rows = Count(_rowsText),
            Gap = N(_gapText),
            Scale = IsScaleValid ? N(_scaleText) : 0,
        };
    }

    /// <summary>Drawing units (m) per paper mm: Scale / 1000; NaN while invalid.</summary>
    public double UnitsPerMm => IsScaleValid ? N(_scaleText) / 1000 : double.NaN;

    /// <summary>Why the layout cannot be used, or null.</summary>
    public string LayoutError
    {
        get
        {
            var layout = Layout();
            if (layout == null || !IsScaleValid) return "Có ô nhập chưa đúng (tô đỏ)";
            return SheetPacker.Validate(layout);
        }
    }

    /// <summary>Moving the views is the job; Khung is optional.</summary>
    public bool CanApply => HasSource && LayoutError == null;

    /// <summary>The plan with sheet 1's lower-left at the origin; null while the layout is invalid or there are no views.</summary>
    public SheetPlan Plan(double originX = 0, double originY = 0) =>
        HasSource && LayoutError == null ? SheetPacker.Pack(_sizes, Layout(), UnitsPerMm, originX, originY) : null;

    public string SummaryText
    {
        get
        {
            if (!HasSource) return "Chưa chọn trắc ngang";
            var error = LayoutError;
            if (error != null) return error;
            var plan = Plan();
            var text = ViewCount.ToString(CultureInfo.InvariantCulture) + " trắc ngang → " + plan.SheetCount.ToString(CultureInfo.InvariantCulture) + " tờ";
            if (plan.Oversize > 0)
                text += "; " + plan.Oversize.ToString(CultureInfo.InvariantCulture) + " trắc ngang lớn hơn ô ("
                        + NumberFormat.Trimmed(plan.CellWidth, 2) + " × " + NumberFormat.Trimmed(plan.CellHeight, 2) + ")";
            return text;
        }
    }

    /// <summary>The views' extents (drawing units) and stations (NaN = unknown), already in station order.</summary>
    public void SetViews(string description, IEnumerable<(double Width, double Height)> sizes, IEnumerable<double> stations)
    {
        _sourceText = description ?? "";
        _sizes = (sizes ?? Enumerable.Empty<(double, double)>()).ToList();
        _stations = (stations ?? Enumerable.Empty<double>()).ToList();
        if (_stations.Count != _sizes.Count) throw new ArgumentException("Cần một lý trình cho mỗi trắc ngang.", nameof(stations));
        Raise(nameof(SourceText));
        Raise(nameof(ViewCount));
        Raise(nameof(HasSource));
        Changed();
    }

    /// <summary>The frame title of a sheet of the plan.</summary>
    public string Title(SheetPlan plan, int sheet, int stationDecimals = 2)
    {
        var frame = plan.Sheets[sheet];
        return SheetPacker.Title(sheet, plan.SheetCount, _stations[frame.FirstItem], _stations[frame.LastItem], stationDecimals);
    }

    private void SetText(ref string field, string value, string name)
    {
        value ??= "";
        if (field == value) return;
        field = value;
        Raise(name);
        Raise("Is" + name.Substring(0, name.Length - "Text".Length) + "Valid");
        Changed();
    }

    private void Changed()
    {
        Raise(nameof(CanApply));
        Raise(nameof(SummaryText));
    }

    private static string T(double v) => NumberFormat.Trimmed(v, 3);
    private static double N(string text) => NumberInput.TryParse(text, out var v) ? v : double.NaN;
    private static bool Positive(string text) => NumberInput.TryParse(text, out var v) && v > 0;
    private static bool NonNegative(string text) => NumberInput.TryParse(text, out var v) && v >= 0;

    private static int Count(string text) =>
        int.TryParse((text ?? "").Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1 && n <= 50 ? n : -1;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
