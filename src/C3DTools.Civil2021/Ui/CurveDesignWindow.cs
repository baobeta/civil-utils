using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
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

    public CurveDesignWindow(CurveDesignSession session, string sourceText, bool isAlignment, int selectedRow)
        : base("CTYTC", "Yếu tố cong – TCVN 4054", 1100, 600, 800, 400)
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
        bottom.Children.Add(tools);

        var outputs = OutputOptions(
            ("Vẽ đường cong (ARC/clothoid)", nameof(CurveDesignSession.DrawCurves)),
            (isAlignment ? "Cập nhật Alignment Civil 3D" : "Tạo Alignment Civil 3D", nameof(CurveDesignSession.CreateAlignment)),
            ("Khung", nameof(CurveDesignSession.DrawBoxes)),
            ("Cọc", nameof(CurveDesignSession.DrawStakes)),
            ("CSV", nameof(CurveDesignSession.WriteCsv)),
            ("Bảng", nameof(CurveDesignSession.WriteTable)));
        _createAlignment = (CheckBox)outputs.Children[1];
        bottom.Children.Add(outputs);

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
        _grid.Columns.Add(TextColumn("T1", nameof(CurveRow.T1Text), false, 70));
        _grid.Columns.Add(TextColumn("T2", nameof(CurveRow.T2Text), false, 70));
        _grid.Columns.Add(TextColumn("P", nameof(CurveRow.PText), false, 60));
        _grid.Columns.Add(TextColumn("K", nameof(CurveRow.KText), false, 70));
        var issues = TextColumn("Cảnh báo", nameof(CurveRow.IssueText), false, 0);
        issues.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
        _grid.Columns.Add(issues);
        _grid.RowStyle = RowStyle();

        SetLayout(top, bottom, _grid);

        if (session.Rows.Count > 0) _grid.SelectedIndex = Math.Max(0, Math.Min(selectedRow, session.Rows.Count - 1));
        _session.PropertyChanged += OnSessionChanged;
        Closed += (s, e) => _session.PropertyChanged -= OnSessionChanged;
        UpdateMode();
    }

    public int SelectedRow => _grid.SelectedIndex;

    protected override void CommitEdits() => CommitGrid();

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
