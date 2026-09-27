using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using C3DTools.Core.Curves;

namespace C3DTools.Civil2021.Ui;

/// <summary>The CTYTC grid dialog, built in code (no XAML) and bound to a CurveDesignSession.</summary>
internal sealed class CurveDesignWindow : ToolWindow
{
    private readonly CurveDesignSession _session;
    private readonly bool _isAlignment;
    private readonly DataGrid _grid;
    private readonly DataGridTextColumn[] _geometryColumns;
    private readonly CheckBox _createAlignment;
    private readonly Button _suggest;
    private readonly Button _copyDown;
    private readonly TextBlock _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.DarkRed, Margin = new Thickness(0, 4, 0, 0) };

    public CurveDesignWindow(CurveDesignSession session, string sourceText, bool isAlignment, int selectedRow)
        : base("CTYTC", "Yếu tố cong – TCVN 4054", 1400, 680, 1000, 480)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _isAlignment = isAlignment;
        DataContext = session;

        // Top: source, start station, speed, Rmin, mode.
        var top = new StackPanel();
        var sourceRow = BuildHeader("Tuyến: " + sourceText);
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
        tools.Children.Add(Label("B/2 (m)"));
        var halfWidth = new TextBox { Width = 50, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Bề rộng nửa mặt đường, dùng cho polyline các đoạn nối" };
        halfWidth.SetBinding(TextBox.TextProperty, NumberBinding(nameof(CurveDesignSession.PavementHalfWidth), UpdateSourceTrigger.LostFocus));
        tools.Children.Add(halfWidth);
        tools.Children.Add(Label("in (%)"));
        var crossSlope = new TextBox { Width = 45, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Dốc ngang mặt đường thông thường" };
        crossSlope.SetBinding(TextBox.TextProperty, NumberBinding(nameof(CurveDesignSession.CrossSlope), UpdateSourceTrigger.LostFocus));
        tools.Children.Add(crossSlope);
        bottom.Children.Add(tools);

        var outputs = OutputOptions(
            ("Vẽ đường cong (ARC/clothoid)", nameof(CurveDesignSession.DrawCurves)),
            (isAlignment ? "Cập nhật Alignment Civil 3D" : "Tạo Alignment Civil 3D", nameof(CurveDesignSession.CreateAlignment)),
            ("Khung", nameof(CurveDesignSession.DrawBoxes)),
            ("Cọc", nameof(CurveDesignSession.DrawStakes)),
            ("CSV", nameof(CurveDesignSession.WriteCsv)),
            ("Excel", nameof(CurveDesignSession.WriteXlsx)),
            ("Bảng", nameof(CurveDesignSession.WriteTable)));
        _createAlignment = (CheckBox)outputs.Children[1];
        bottom.Children.Add(outputs);
        var more = OutputOptions(
            ("Polyline các đoạn nối", nameof(CurveDesignSession.DrawEdges)),
            ("Siêu cao → Alignment", nameof(CurveDesignSession.WriteSuperelevation)));
        if (isAlignment) more.Children.Add(Check("Dồn dịch đỉnh trắc dọc phía sau", nameof(CurveDesignSession.ShiftProfiles)));
        bottom.Children.Add(more);

        bottom.Children.Add(BuildFooter(nameof(CurveDesignSession.SummaryText), nameof(CurveDesignSession.CanApply)));

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
        _grid.Columns.Add(TextColumn("V", nameof(CurveRow.SpeedText), true, 40));
        _grid.Columns.Add(TextColumn("SC", nameof(CurveRow.SuperelevationText), false, 45));
        _grid.Columns.Add(TextColumn("T1", nameof(CurveRow.T1Text), false, 70));
        _grid.Columns.Add(TextColumn("T2", nameof(CurveRow.T2Text), false, 70));
        _grid.Columns.Add(TextColumn("P", nameof(CurveRow.PText), false, 60));
        _grid.Columns.Add(TextColumn("K", nameof(CurveRow.KText), false, 70));
        var issues = TextColumn("Cảnh báo", nameof(CurveRow.IssueText), false, 0);
        issues.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
        _grid.Columns.Add(issues);
        _grid.RowStyle = RowStyle();

        var centre = new DockPanel();
        var detail = BuildDetail(session);
        DockPanel.SetDock(detail, Dock.Right);
        centre.Children.Add(detail);
        centre.Children.Add(_grid);
        SetLayout(top, bottom, centre);

        if (session.Rows.Count > 0) _grid.SelectedIndex = Math.Max(0, Math.Min(selectedRow, session.Rows.Count - 1));
        _session.PropertyChanged += OnSessionChanged;
        Closed += (s, e) => _session.PropertyChanged -= OnSessionChanged;
        UpdateMode();
    }

    public int SelectedRow => _grid.SelectedIndex;

    protected override void CommitEdits()
    {
        CommitGrid();
        // A detail field being typed in updates on LostFocus: push it before the dialog closes.
        (Keyboard.FocusedElement as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    /// <summary>
    /// "Hiệu chỉnh yếu tố cong và thông số siêu cao" for the selected row, laid out like AND Design's dialog:
    /// speed and angle, R and spirals with Rmax/Lmax, superelevation and widening with the start/end runoffs.
    /// </summary>
    private FrameworkElement BuildDetail(CurveDesignSession session)
    {
        var panel = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        panel.SetBinding(DataContextProperty, new Binding(nameof(DataGrid.SelectedItem)) { Source = _grid });

        var header = new TextBlock { FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
        header.SetBinding(TextBlock.TextProperty, new Binding(nameof(CurveRow.HeaderText)));
        panel.Children.Add(header);

        var nav = Row();
        nav.Children.Add(Button("< Trước", (s, e) => MoveSelection(-1)));
        nav.Children.Add(Button("Tiếp >", (s, e) => MoveSelection(1)));
        panel.Children.Add(nav);

        var geometry = new GroupBox { Header = "Thông số cong", Padding = new Thickness(4) };
        var g = new StackPanel();
        g.Children.Add(Field("Tốc độ tại đỉnh (km/h)", nameof(CurveRow.SpeedText), 60, "Để trống: dùng V của tuyến"));
        var angle = Field("Góc chuyển hướng (°)", nameof(CurveRow.DeflectionText), 90,
            "Hiệu chỉnh góc chuyển hướng: xoay phần tuyến sau đỉnh này (chỉ khi thiết kế từ polyline)");
        angle.SetBinding(IsEnabledProperty, new Binding(nameof(CurveDesignSession.CanEditDeflection)) { Source = session });
        g.Children.Add(angle);
        var radius = Row();
        var rmin = new RadioButton { Content = "Rmin tối thiểu", GroupName = "Rmin", Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        rmin.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(CurveDesignSession.SuggestNormalRadius)) { Source = session, Converter = InverseBool.Instance });
        var rnormal = new RadioButton { Content = "Rmin thông thường", GroupName = "Rmin", VerticalAlignment = VerticalAlignment.Center };
        rnormal.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(CurveDesignSession.SuggestNormalRadius)) { Source = session });
        radius.Children.Add(rmin);
        radius.Children.Add(rnormal);
        g.Children.Add(radius);
        var editable = new StackPanel();
        editable.SetBinding(IsEnabledProperty, new Binding(nameof(CurveDesignSession.ReadOnlyGeometry)) { Source = session, Converter = InverseBool.Instance });
        editable.Children.Add(Pair(("R", nameof(CurveRow.RadiusText)), ("", null)));
        editable.Children.Add(Pair(("L1", nameof(CurveRow.SpiralInText)), ("L2", nameof(CurveRow.SpiralOutText))));
        editable.Children.Add(Pair(("A1", nameof(CurveRow.A1Text)), ("A2", nameof(CurveRow.A2Text))));
        g.Children.Add(editable);
        var limits = Row();
        limits.Children.Add(Bound(nameof(CurveRow.RmaxText), 150));
        limits.Children.Add(Bound(nameof(CurveRow.LmaxText), 150));
        g.Children.Add(limits);
        var suggest = Button("Tra yếu tố cong", (s, e) =>
        {
            CommitEdits();
            if (_grid.SelectedIndex >= 0) _session.SuggestRow(_grid.SelectedIndex);
        });
        suggest.SetBinding(IsEnabledProperty, new Binding(nameof(CurveDesignSession.ReadOnlyGeometry)) { Source = session, Converter = InverseBool.Instance });
        g.Children.Add(suggest);
        geometry.Content = g;
        panel.Children.Add(geometry);

        var super = new GroupBox { Header = "Thông số siêu cao, mở rộng", Padding = new Thickness(4), Margin = new Thickness(0, 6, 0, 0) };
        var p = new StackPanel();
        var mode = Row();
        var none = new RadioButton { Content = "Không bố trí", GroupName = "SC", Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        none.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(CurveRow.Superelevated)) { Converter = InverseBool.Instance });
        var on = new RadioButton { Content = "Siêu cao", GroupName = "SC", VerticalAlignment = VerticalAlignment.Center };
        on.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(CurveRow.Superelevated)));
        mode.Children.Add(none);
        mode.Children.Add(on);
        mode.Children.Add(Label("i max (%)"));
        mode.Children.Add(Input(nameof(CurveRow.SuperRateText), 45));
        p.Children.Add(mode);
        var spiral = new CheckBox { Content = "Bố trí theo chuyển tiếp", Margin = new Thickness(0, 2, 0, 2) };
        spiral.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(CurveRow.RunoffOnSpiral)) { Mode = BindingMode.TwoWay });
        p.Children.Add(spiral);
        var split = new CheckBox { Content = "Mở rộng phân đều khi tra", Margin = new Thickness(0, 2, 0, 2) };
        split.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(CurveDesignSession.SplitWidening)) { Source = session, Mode = BindingMode.TwoWay });
        p.Children.Add(split);
        p.Children.Add(Pair(("Mở rộng bụng", nameof(CurveRow.WbText)), ("lưng", nameof(CurveRow.WlText))));
        var runoffs = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        for (var c = 0; c < 3; c++) runoffs.ColumnDefinitions.Add(new ColumnDefinition { Width = c == 0 ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });
        for (var r = 0; r < 3; r++) runoffs.RowDefinitions.Add(new RowDefinition());
        Place(runoffs, new TextBlock { Text = "Chiều dài nối", Margin = new Thickness(4, 0, 4, 2) }, 0, 1);
        Place(runoffs, new TextBlock { Text = "Lệch ngoài", Margin = new Thickness(4, 0, 4, 2) }, 0, 2);
        Place(runoffs, new TextBlock { Text = "Nối đầu:", VerticalAlignment = VerticalAlignment.Center }, 1, 0);
        Place(runoffs, Input(nameof(CurveRow.RunoffInText), 0), 1, 1);
        Place(runoffs, Input(nameof(CurveRow.OffsetInText), 0), 1, 2);
        Place(runoffs, new TextBlock { Text = "Nối cuối:", VerticalAlignment = VerticalAlignment.Center }, 2, 0);
        Place(runoffs, Input(nameof(CurveRow.RunoffOutText), 0), 2, 1);
        Place(runoffs, Input(nameof(CurveRow.OffsetOutText), 0), 2, 2);
        p.Children.Add(runoffs);
        p.Children.Add(Button("Tra siêu cao", (s, e) =>
        {
            CommitEdits();
            _status.Text = _grid.SelectedIndex >= 0 ? _session.SuggestSuperelevation(_grid.SelectedIndex) ?? "" : "";
        }));
        p.Children.Add(_status);
        super.Content = p;
        panel.Children.Add(super);

        return new ScrollViewer { Content = panel, Width = 380, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private void MoveSelection(int step)
    {
        CommitEdits();
        var next = _grid.SelectedIndex + step;
        if (next < 0 || next >= _session.Rows.Count) return;
        _grid.SelectedIndex = next;
        _grid.ScrollIntoView(_grid.SelectedItem);
        _status.Text = "";
    }

    /// <summary>Label and a text box bound (on LostFocus) to a CurveRow property.</summary>
    private static StackPanel Field(string label, string path, double width, string tip)
    {
        var row = Row();
        row.Children.Add(new TextBlock { Text = label, Width = 150, VerticalAlignment = VerticalAlignment.Center });
        var box = Input(path, width);
        box.ToolTip = tip;
        row.Children.Add(box);
        return row;
    }

    private static StackPanel Pair((string label, string path) a, (string label, string path) b)
    {
        var row = Row();
        foreach (var (label, path) in new[] { a, b })
        {
            if (path == null) continue;
            row.Children.Add(new TextBlock { Text = label, Width = label.Length > 3 ? 90 : 24, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            row.Children.Add(Input(path, 70));
            row.Children.Add(new Border { Width = 10 });
        }

        return row;
    }

    private static TextBox Input(string path, double width)
    {
        var box = new TextBox { VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 0, 1) };
        if (width > 0) box.Width = width;
        box.SetBinding(TextBox.TextProperty, new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus });
        return box;
    }

    private static TextBlock Bound(string path, double width)
    {
        var text = new TextBlock { Width = width, VerticalAlignment = VerticalAlignment.Center };
        text.SetBinding(TextBlock.TextProperty, new Binding(path));
        return text;
    }

    private static void Place(Grid grid, UIElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

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

    private void CommitGrid()
    {
        _grid.CommitEdit(DataGridEditingUnit.Cell, true);
        _grid.CommitEdit(DataGridEditingUnit.Row, true);
    }

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
}
