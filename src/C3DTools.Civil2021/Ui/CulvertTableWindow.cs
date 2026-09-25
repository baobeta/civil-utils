using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using C3DTools.Core.Drainage;

namespace C3DTools.Civil2021.Ui;

/// <summary>The CTBANGCONG dialog: alignment, pipe networks, surface for ground, outputs, and the schedule (Tên, Loại, Ghi chú editable).</summary>
internal sealed class CulvertTableWindow : ToolWindow
{
    private readonly DataGrid _grid;

    public CulvertTableWindow(CulvertSession session)
        : base("CTBANGCONG", "Bảng thống kê cống", 1000, 600, 700, 420)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        DataContext = session;

        var top = new StackPanel();
        top.Children.Add(BuildHeader("Tuyến: " + session.AlignmentText));

        var networks = Row();
        networks.Children.Add(new TextBlock { Text = "Mạng cống", Width = 130, VerticalAlignment = VerticalAlignment.Center });
        var list = new ItemsControl { ItemsSource = session.Networks, ItemTemplate = NetworkTemplate(), VerticalAlignment = VerticalAlignment.Center };
        list.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(WrapPanel)));
        networks.Children.Add(list);
        if (session.Networks.Count == 0)
            networks.Children.Add(new TextBlock { Text = "(bản vẽ không có mạng cống)", Foreground = System.Windows.Media.Brushes.DimGray, VerticalAlignment = VerticalAlignment.Center });
        top.Children.Add(networks);

        var surface = Row();
        surface.Children.Add(new TextBlock { Text = "Cao độ mặt đất", Width = 130, VerticalAlignment = VerticalAlignment.Center });
        var combo = new ComboBox { Width = 240, VerticalContentAlignment = VerticalAlignment.Center };
        combo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(CulvertSession.SurfaceNames)));
        combo.SetBinding(Selector.SelectedIndexProperty, new Binding(nameof(CulvertSession.SurfaceIndex)) { Mode = BindingMode.TwoWay });
        surface.Children.Add(combo);
        surface.Children.Add(new TextBlock
        {
            Text = "  Đầu cống có hố ga: lấy cao độ đỉnh hố ga; không có: lấy từ mặt phủ này.",
            Foreground = System.Windows.Media.Brushes.DimGray,
            VerticalAlignment = VerticalAlignment.Center,
        });
        top.Children.Add(surface);

        var outputs = OutputOptions(
            ("Bảng", nameof(CulvertSession.WriteTable)),
            ("CSV", nameof(CulvertSession.WriteCsv)),
            ("Excel", nameof(CulvertSession.WriteXlsx)));
        outputs.Children.Insert(0, new TextBlock { Text = "Xuất ra:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        top.Children.Add(outputs);
        var rules = session.HasRules
            ? "Cảnh báo theo PipeRules của preset (độ dốc, chiều sâu chôn)."
            : "Preset chưa có PipeRules: không kiểm tra độ dốc, chiều sâu chôn.";
        top.Children.Add(new TextBlock
        {
            Text = "Sửa được cột Tên (để trống = tự đặt C1, C2… theo lý trình), Loại và Ghi chú. Bảng: chọn điểm chèn sau khi bấm Áp dụng; "
                + "chạy lại cho cùng tuyến sẽ thay bảng cũ. CSV, Excel: ghi cạnh bản vẽ (<tên bản vẽ>_BANGCONG). " + rules,
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.DimGray,
            Margin = new Thickness(0, 2, 0, 6),
        });

        _grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserSortColumns = false,
            SelectionMode = DataGridSelectionMode.Single,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            ItemsSource = session.Rows,
        };
        _grid.Columns.Add(Fixed("STT", nameof(CulvertRow.Number), 40, number: true));
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Tên",
            Binding = new Binding(nameof(CulvertRow.Label)) { Mode = BindingMode.TwoWay },
            Width = new DataGridLength(70),
        });
        _grid.Columns.Add(Fixed("Ống", nameof(CulvertRow.PipeName), 100, number: false));
        _grid.Columns.Add(Fixed("Lý trình", nameof(CulvertRow.Station), 90, number: true));
        _grid.Columns.Add(Fixed("Vị trí", nameof(CulvertRow.Side), 75, number: false));
        _grid.Columns.Add(Fixed("Góc chéo", nameof(CulvertRow.Skew), 65, number: true));
        _grid.Columns.Add(new DataGridTemplateColumn { Header = "Loại", CellTemplate = KindTemplate(), Width = new DataGridLength(110) });
        _grid.Columns.Add(Fixed("Khẩu độ", nameof(CulvertRow.Size), 95, number: false));
        _grid.Columns.Add(Fixed("Dài (m)", nameof(CulvertRow.Length), 60, number: true));
        _grid.Columns.Add(Fixed("CĐ đáy TL", nameof(CulvertRow.InvertUpstream), 70, number: true));
        _grid.Columns.Add(Fixed("CĐ đáy HL", nameof(CulvertRow.InvertDownstream), 70, number: true));
        _grid.Columns.Add(Fixed("Dốc (%)", nameof(CulvertRow.Slope), 60, number: true));
        _grid.Columns.Add(Fixed("Chôn min", nameof(CulvertRow.Cover), 65, number: true));
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Ghi chú",
            Binding = new Binding(nameof(CulvertRow.Note)) { Mode = BindingMode.TwoWay },
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            MinWidth = 90,
        });
        var warning = Fixed("Cảnh báo", nameof(CulvertRow.Warning), 0, number: false);
        warning.CellStyle = null;   // the row's yellow shows through
        _grid.Columns.Add(warning);
        _grid.RowStyle = RowStyle();

        SetLayout(top, BuildFooter(nameof(CulvertSession.SummaryText), nameof(CulvertSession.CanApply)), _grid);
    }

    protected override void CommitEdits()
    {
        _grid.CommitEdit(DataGridEditingUnit.Cell, true);
        _grid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    /// <summary>A read-only grey column; width 0 shares the remaining width.</summary>
    private static DataGridTextColumn Fixed(string header, string path, double width, bool number) =>
        new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(path) { Mode = BindingMode.OneWay },
            IsReadOnly = true,
            CellStyle = GreyCell(),
            ElementStyle = number ? NumberCellStyle() : null,
            Width = width > 0 ? new DataGridLength(width) : new DataGridLength(1, DataGridLengthUnitType.Star),
            MinWidth = width > 0 ? 0 : 120,
        };

    private static DataTemplate NetworkTemplate()
    {
        var check = new FrameworkElementFactory(typeof(CheckBox));
        check.SetBinding(ContentControl.ContentProperty, new Binding(nameof(CulvertNetworkItem.Name)));
        check.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(CulvertNetworkItem.IsChecked)) { Mode = BindingMode.TwoWay });
        check.SetValue(MarginProperty, new Thickness(0, 0, 14, 0));
        check.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        return new DataTemplate { VisualTree = check };
    }

    /// <summary>An editable combo (standard kinds + free text) in every row, bound to Kind as the user types.</summary>
    private static DataTemplate KindTemplate()
    {
        var combo = new FrameworkElementFactory(typeof(ComboBox));
        combo.SetValue(ComboBox.IsEditableProperty, true);
        combo.SetValue(ComboBox.IsTextSearchEnabledProperty, false);
        combo.SetValue(BorderThicknessProperty, new Thickness(0));
        combo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("DataContext." + nameof(CulvertSession.KindNames))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGrid), 1),
        });
        combo.SetBinding(ComboBox.TextProperty, new Binding(nameof(CulvertRow.Kind))
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
        });
        return new DataTemplate { VisualTree = combo };
    }

    private static Style RowStyle()
    {
        var style = new Style(typeof(DataGridRow));
        var warning = new DataTrigger { Binding = new Binding(nameof(CulvertRow.HasWarning)), Value = true };
        warning.Setters.Add(new Setter(BackgroundProperty, WarningBrush));
        style.Triggers.Add(warning);
        return style;
    }
}
