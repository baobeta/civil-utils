using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using C3DTools.Core.Curves;
using C3DTools.Core.Tables;

namespace C3DTools.Civil2021.Ui;

/// <summary>What the dialog asks the command to do after it closes.</summary>
internal enum DialogAction { Cancel, Pick, ZoomToPi, ReadWidening, Preview, Apply }

/// <summary>The CTYTC grid dialog, built in code (no XAML) and bound to a CurveDesignSession.</summary>
internal sealed class CurveDesignWindow : Window
{
    private static readonly Brush WarningBrush = Frozen(Color.FromRgb(0xFF, 0xF4, 0xC2));
    private static readonly Brush ErrorBrush = Frozen(Color.FromRgb(0xFF, 0xD6, 0xD6));
    private static readonly Brush ReadOnlyBrush = Frozen(Color.FromRgb(0xEE, 0xEE, 0xEE));

    private readonly CurveDesignSession _session;
    private readonly bool _isAlignment;
    private readonly DataGrid _grid;
    private readonly DataGridTextColumn[] _geometryColumns;
    private readonly CheckBox _createAlignment;
    private readonly Button _suggest;
    private readonly Button _copyDown;

    public CurveDesignWindow(CurveDesignSession session, string sourceText, bool isAlignment, int selectedRow)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _isAlignment = isAlignment;
        DataContext = session;

        Title = "Yếu tố cong – TCVN 4054";
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 12;
        Width = 1100;
        Height = 600;
        MinWidth = 800;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var root = new DockPanel { Margin = new Thickness(10) };

        // Top: source, start station, speed, Rmin, mode.
        var top = new StackPanel();
        var sourceRow = Row();
        sourceRow.Children.Add(new TextBlock { Text = "Tuyến: " + sourceText, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        sourceRow.Children.Add(ActionButton("Chọn trên bản vẽ…", DialogAction.Pick));
        sourceRow.Children.Add(Label("Lý trình đầu"));
        var start = new TextBox { Width = 90, IsReadOnly = isAlignment, VerticalContentAlignment = VerticalAlignment.Center };
        start.SetBinding(TextBox.TextProperty, NumberBinding(nameof(CurveDesignSession.StartStation), UpdateSourceTrigger.LostFocus));
        if (isAlignment) start.Background = ReadOnlyBrush;
        sourceRow.Children.Add(start);
        sourceRow.Children.Add(Label("V"));
        var speed = new ComboBox { Width = 70, ItemsSource = session.AvailableSpeeds };
        speed.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(CurveDesignSession.DesignSpeed)) { Mode = BindingMode.TwoWay });
        sourceRow.Children.Add(speed);
        sourceRow.Children.Add(new TextBlock { Text = "km/h", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
        top.Children.Add(sourceRow);

        var rmin = new TextBlock { Margin = new Thickness(0, 4, 0, 4) };
        rmin.SetBinding(TextBlock.TextProperty, new Binding(nameof(CurveDesignSession.RminText)));
        top.Children.Add(rmin);

        if (isAlignment)
        {
            var mode = Row();
            mode.Children.Add(new TextBlock { Text = "Chế độ:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            var redesign = new RadioButton { Content = "Thiết kế lại cong", GroupName = "Mode", Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            redesign.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(CurveDesignSession.ReadOnlyGeometry)) { Converter = InverseBool.Instance });
            var stakesOnly = new RadioButton { Content = "Chỉ cắm cọc + khung", GroupName = "Mode", VerticalAlignment = VerticalAlignment.Center };
            stakesOnly.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(CurveDesignSession.ReadOnlyGeometry)));
            mode.Children.Add(redesign);
            mode.Children.Add(stakesOnly);
            mode.Children.Add(ActionButton("Đọc Wb/Wl từ Offset Alignment…", DialogAction.ReadWidening));
            top.Children.Add(mode);
        }

        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        // Bottom: selected row, row tools, outputs, actions.
        var bottom = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        _grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserSortColumns = false,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            ItemsSource = session.Rows,
        };

        var stationRow = Row();
        var stations = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        stations.SetBinding(TextBlock.TextProperty, new Binding("SelectedItem." + nameof(CurveRow.StationsText)) { Source = _grid, StringFormat = "Dòng chọn: {0}" });
        stationRow.Children.Add(stations);
        stationRow.Children.Add(ActionButton("Phóng tới đỉnh", DialogAction.ZoomToPi));
        bottom.Children.Add(stationRow);

        var tools = Row();
        _suggest = Button("Gợi ý R, L theo TCVN", (s, e) => { CommitGrid(); _session.SuggestAll(); });
        _copyDown = Button("Áp dòng này cho các dòng dưới", (s, e) =>
        {
            CommitGrid();
            if (_grid.SelectedIndex >= 0) _session.CopyDown(_grid.SelectedIndex);
        });
        tools.Children.Add(_suggest);
        tools.Children.Add(_copyDown);
        tools.Children.Add(Label("Chiều cao chữ"));
        var textHeight = new TextBox { Width = 60, VerticalContentAlignment = VerticalAlignment.Center };
        textHeight.SetBinding(TextBox.TextProperty, NumberBinding(nameof(CurveDesignSession.TextHeight), UpdateSourceTrigger.LostFocus));
        tools.Children.Add(textHeight);
        bottom.Children.Add(tools);

        var outputs = Row();
        outputs.Children.Add(Check("Vẽ đường cong (ARC/clothoid)", nameof(CurveDesignSession.DrawCurves)));
        _createAlignment = Check(isAlignment ? "Cập nhật Alignment Civil 3D" : "Tạo Alignment Civil 3D", nameof(CurveDesignSession.CreateAlignment));
        outputs.Children.Add(_createAlignment);
        outputs.Children.Add(Check("Khung", nameof(CurveDesignSession.DrawBoxes)));
        outputs.Children.Add(Check("Cọc", nameof(CurveDesignSession.DrawStakes)));
        outputs.Children.Add(Check("CSV", nameof(CurveDesignSession.WriteCsv)));
        bottom.Children.Add(outputs);

        var actions = new DockPanel { Margin = new Thickness(0, 6, 0, 0), LastChildFill = false };
        var summary = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        summary.SetBinding(TextBlock.TextProperty, new Binding(nameof(CurveDesignSession.SummaryText)));
        actions.Children.Add(summary);
        var cancel = Button("Hủy", (s, e) => Finish(DialogAction.Cancel));
        cancel.IsCancel = true;
        var apply = ActionButton("Áp dụng", DialogAction.Apply);
        apply.SetBinding(IsEnabledProperty, new Binding(nameof(CurveDesignSession.CanApply)));
        var preview = ActionButton("Xem trước", DialogAction.Preview);
        preview.SetBinding(IsEnabledProperty, new Binding(nameof(CurveDesignSession.CanApply)));
        foreach (var b in new[] { cancel, apply, preview })
        {
            DockPanel.SetDock(b, Dock.Right);
            actions.Children.Add(b);
        }

        bottom.Children.Add(actions);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);

        // Grid: one row per CurveRow.
        _grid.Columns.Add(TextColumn("Đỉnh", nameof(CurveRow.Name), false, 50));
        _grid.Columns.Add(TextColumn("A", nameof(CurveRow.AText), false, 90));
        _geometryColumns = new[]
        {
            TextColumn("R", nameof(CurveRow.RadiusText), true, 70),
            TextColumn("L1", nameof(CurveRow.SpiralInText), true, 60),
            TextColumn("L2", nameof(CurveRow.SpiralOutText), true, 60),
        };
        foreach (var c in _geometryColumns) _grid.Columns.Add(c);
        _grid.Columns.Add(TextColumn("Wb", nameof(CurveRow.WbText), true, 55));
        _grid.Columns.Add(TextColumn("Wl", nameof(CurveRow.WlText), true, 55));
        _grid.Columns.Add(TextColumn("T1", nameof(CurveRow.T1Text), false, 70));
        _grid.Columns.Add(TextColumn("T2", nameof(CurveRow.T2Text), false, 70));
        _grid.Columns.Add(TextColumn("P", nameof(CurveRow.PText), false, 60));
        _grid.Columns.Add(TextColumn("K", nameof(CurveRow.KText), false, 70));
        var issues = TextColumn("Cảnh báo", nameof(CurveRow.IssueText), false, 0);
        issues.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
        _grid.Columns.Add(issues);
        _grid.RowStyle = RowStyle();
        root.Children.Add(_grid);

        Content = root;

        if (session.Rows.Count > 0) _grid.SelectedIndex = Math.Max(0, Math.Min(selectedRow, session.Rows.Count - 1));
        _session.PropertyChanged += OnSessionChanged;
        Closed += (s, e) => _session.PropertyChanged -= OnSessionChanged;
        UpdateMode();
    }

    public DialogAction Action { get; private set; } = DialogAction.Cancel;

    public int SelectedRow => _grid.SelectedIndex;

    private void OnSessionChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CurveDesignSession.ReadOnlyGeometry)) return;
        // Switching an alignment to "Thiết kế lại cong" means updating it; back to "Chỉ cắm cọc + khung" leaves it alone.
        if (_isAlignment) _session.CreateAlignment = !_session.ReadOnlyGeometry;
        UpdateMode();
    }

    /// <summary>"Chỉ cắm cọc + khung": R, L1, L2 read-only and grey; the alignment is not touched.</summary>
    private void UpdateMode()
    {
        var readOnly = _session.ReadOnlyGeometry;
        foreach (var c in _geometryColumns)
        {
            c.IsReadOnly = readOnly;
            c.CellStyle = readOnly ? GreyCell() : null;
        }

        if (readOnly) _session.CreateAlignment = false;
        _createAlignment.IsEnabled = !readOnly;
        _suggest.IsEnabled = !readOnly;
        _copyDown.IsEnabled = !readOnly;
    }

    private void Finish(DialogAction action)
    {
        CommitGrid();
        Action = action;
        Close();
    }

    private void CommitGrid()
    {
        _grid.CommitEdit(DataGridEditingUnit.Cell, true);
        _grid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private Button ActionButton(string text, DialogAction action) => Button(text, (s, e) => Finish(action));

    private static Button Button(string text, RoutedEventHandler click)
    {
        var b = new Button { Content = text, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 8, 0), MinWidth = 80 };
        b.Click += click;
        return b;
    }

    private static StackPanel Row() => new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

    private static TextBlock Label(string text) =>
        new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 4, 0) };

    private static CheckBox Check(string text, string path)
    {
        var c = new CheckBox { Content = text, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
        c.SetBinding(ToggleButton.IsCheckedProperty, new Binding(path) { Mode = BindingMode.TwoWay });
        return c;
    }

    private static Binding NumberBinding(string path, UpdateSourceTrigger trigger) =>
        new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = trigger, Converter = NumberText.Instance };

    private static DataGridTextColumn TextColumn(string header, string path, bool editable, double width)
    {
        var binding = new Binding(path)
        {
            Mode = editable ? BindingMode.TwoWay : BindingMode.OneWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        };
        var column = new DataGridTextColumn { Header = header, Binding = binding, IsReadOnly = !editable };
        if (width > 0) column.Width = new DataGridLength(width);
        return column;
    }

    private static Style RowStyle()
    {
        var style = new Style(typeof(DataGridRow));
        style.Setters.Add(new Setter(ToolTipProperty, new Binding(nameof(CurveRow.IssueText))));
        foreach (var (severity, brush) in new[] { (CurveRowSeverity.Warning, WarningBrush), (CurveRowSeverity.Error, ErrorBrush) })
        {
            var trigger = new DataTrigger { Binding = new Binding(nameof(CurveRow.Severity)), Value = severity };
            trigger.Setters.Add(new Setter(BackgroundProperty, brush));
            style.Triggers.Add(trigger);
        }
        return style;
    }

    private static Style GreyCell()
    {
        var style = new Style(typeof(DataGridCell));
        style.Setters.Add(new Setter(BackgroundProperty, ReadOnlyBrush));
        style.Setters.Add(new Setter(ForegroundProperty, Brushes.DimGray));
        return style;
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <summary>double ⇄ text with InvariantCulture; accepts "2,5" like the grid does.</summary>
    private sealed class NumberText : IValueConverter
    {
        public static readonly NumberText Instance = new NumberText();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is double d ? NumberFormat.Trimmed(d, 3) : "";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            NumberInput.TryParse(value as string, out var d) ? d : Binding.DoNothing;
    }

    private sealed class InverseBool : IValueConverter
    {
        public static readonly InverseBool Instance = new InverseBool();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !(value is bool b && b);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => !(value is bool b && b);
    }
}
