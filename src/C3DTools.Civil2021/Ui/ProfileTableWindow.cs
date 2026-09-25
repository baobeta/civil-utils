using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using C3DTools.Core.Profiles;

namespace C3DTools.Civil2021.Ui;

/// <summary>The CTTRACDOC dialog: profile view, surface and design profiles, stations, rows (order, on/off, decimals), text height, outputs.</summary>
internal sealed class ProfileTableWindow : ToolWindow
{
    private readonly DataGrid _rows;

    public ProfileTableWindow(ProfileTableSession session)
        : base("CTTRACDOC", "Bảng số liệu trắc dọc", 720, 600, 560, 440)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        DataContext = session;

        var top = new StackPanel();
        top.Children.Add(BuildHeader("Trắc dọc: " + session.SourceText));

        var profiles = Row();
        profiles.Children.Add(new TextBlock { Text = "Tự nhiên", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        profiles.Children.Add(Combo(nameof(ProfileTableSession.SurfaceProfileNames), nameof(ProfileTableSession.SurfaceProfileIndex)));
        profiles.Children.Add(Label("Thiết kế"));
        profiles.Children.Add(Combo(nameof(ProfileTableSession.DesignProfileNames), nameof(ProfileTableSession.DesignProfileIndex)));
        top.Children.Add(profiles);

        var stations = Row();
        stations.Children.Add(new TextBlock { Text = "Cọc:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        stations.Children.Add(Radio("Tất cả cọc (chi tiết + cong)", nameof(ProfileTableSession.IsAllStakes)));
        stations.Children.Add(Radio("Chỉ cọc chi tiết", nameof(ProfileTableSession.IsInterval)));
        stations.Children.Add(Radio("Theo cọc mặt cắt (sample line)", nameof(ProfileTableSession.IsSampleLines)));
        top.Children.Add(stations);

        var sizes = Row();
        sizes.Children.Add(new TextBlock { Text = "Khoảng cách cọc (m)", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        sizes.Children.Add(Input(nameof(ProfileTableSession.IntervalText), nameof(ProfileTableSession.IsIntervalValid), 60));
        sizes.Children.Add(Label(session.TextHeightLabel));
        sizes.Children.Add(Input(nameof(ProfileTableSession.TextHeightText), nameof(ProfileTableSession.IsTextHeightValid), 60));
        top.Children.Add(sizes);

        var outputs = OutputOptions(
            ("Bảng", nameof(ProfileTableSession.WriteTable)),
            ("CSV", nameof(ProfileTableSession.WriteCsv)),
            ("Excel", nameof(ProfileTableSession.WriteXlsx)));
        outputs.Children.Insert(0, new TextBlock { Text = "Xuất ra:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        top.Children.Add(outputs);
        top.Children.Add(new TextBlock
        {
            Text = "Bảng: vẽ dưới trắc dọc (đường TD_BANG, chữ TD_CHU). CSV, Excel: mỗi cọc một dòng, ghi cạnh bản vẽ (<tên bản vẽ>_TRACDOC). Chạy lại cho cùng trắc dọc sẽ thay bảng cũ.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.DimGray,
            Margin = new Thickness(0, 2, 0, 6),
        });

        _rows = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserSortColumns = false,
            CanUserReorderColumns = false,
            SelectionMode = DataGridSelectionMode.Single,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            ItemsSource = session.Rows,
        };
        _rows.SetBinding(Selector.SelectedIndexProperty, new Binding(nameof(ProfileTableSession.SelectedRowIndex)) { Mode = BindingMode.TwoWay });
        _rows.Columns.Add(new DataGridCheckBoxColumn
        {
            Header = "Hiện",
            Binding = new Binding(nameof(ProfileTableRowOption.Include)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
            Width = new DataGridLength(50),
        });
        _rows.Columns.Add(new DataGridTextColumn
        {
            Header = "Dòng",
            Binding = new Binding(nameof(ProfileTableRowOption.Label)),
            IsReadOnly = true,
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
        });
        var decimalsStyle = new Style(typeof(DataGridCell));
        var invalid = new DataTrigger { Binding = new Binding(nameof(ProfileTableRowOption.IsDecimalsValid)), Value = false };
        invalid.Setters.Add(new Setter(BackgroundProperty, ErrorBrush));
        decimalsStyle.Triggers.Add(invalid);
        _rows.Columns.Add(new DataGridTextColumn
        {
            Header = "Số lẻ",
            Binding = new Binding(nameof(ProfileTableRowOption.DecimalsText)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
            ElementStyle = NumberCellStyle(),
            CellStyle = decimalsStyle,
            Width = new DataGridLength(60),
        });

        var move = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        var up = Button("Lên", (s, e) => session.MoveRow(session.SelectedRowIndex, -1));
        var down = Button("Xuống", (s, e) => session.MoveRow(session.SelectedRowIndex, +1));
        up.Margin = down.Margin = new Thickness(0, 0, 0, 6);
        move.Children.Add(up);
        move.Children.Add(down);

        var content = new DockPanel();
        DockPanel.SetDock(move, Dock.Right);
        content.Children.Add(move);
        content.Children.Add(_rows);

        SetLayout(top, BuildFooter(nameof(ProfileTableSession.SummaryText), nameof(ProfileTableSession.CanApply)), content);
    }

    protected override void CommitEdits() => _rows.CommitEdit(DataGridEditingUnit.Row, true);

    private static ComboBox Combo(string itemsPath, string indexPath)
    {
        var combo = new ComboBox { Width = 200, VerticalContentAlignment = VerticalAlignment.Center };
        combo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(itemsPath));
        combo.SetBinding(Selector.SelectedIndexProperty, new Binding(indexPath) { Mode = BindingMode.TwoWay });
        return combo;
    }

    private static RadioButton Radio(string text, string path)
    {
        var radio = new RadioButton { Content = text, GroupName = "CTTRACDOC_Stations", Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
        radio.SetBinding(ToggleButton.IsCheckedProperty, new Binding(path) { Mode = BindingMode.TwoWay });
        return radio;
    }

    /// <summary>A text box bound as the user types; red while validPath is false.</summary>
    private static TextBox Input(string path, string validPath, double width)
    {
        var box = new TextBox { Width = width, VerticalContentAlignment = VerticalAlignment.Center };
        box.SetBinding(TextBox.TextProperty, new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        var style = new Style(typeof(TextBox));
        var invalid = new DataTrigger { Binding = new Binding(validPath), Value = false };
        invalid.Setters.Add(new Setter(BackgroundProperty, ErrorBrush));
        style.Triggers.Add(invalid);
        box.Style = style;
        return box;
    }
}
