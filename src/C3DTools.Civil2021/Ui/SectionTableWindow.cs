using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using C3DTools.Core.Profiles;
using C3DTools.Core.Sections;

namespace C3DTools.Civil2021.Ui;

/// <summary>The CTTRACNGANG dialog: section views, ground and design sections, rows (order, on/off, decimals), text height, outputs.</summary>
internal sealed class SectionTableWindow : ToolWindow
{
    private readonly DataGrid _rows;

    public SectionTableWindow(SectionTableSession session)
        : base("CTTRACNGANG", "Bảng số liệu trắc ngang", 720, 580, 560, 440)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        DataContext = session;

        var top = new StackPanel();
        var header = BuildHeader("Trắc ngang: " + session.SourceText);
        header.Children.Add(Check("Cả nhóm (section view group)", nameof(SectionTableSession.WholeGroup)));
        top.Children.Add(header);

        var sections = Row();
        sections.Children.Add(new TextBlock { Text = "Tự nhiên", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        sections.Children.Add(Combo(nameof(SectionTableSession.GroundSectionNames), nameof(SectionTableSession.GroundSectionIndex)));
        sections.Children.Add(Label("Thiết kế"));
        sections.Children.Add(Combo(nameof(SectionTableSession.DesignSectionNames), nameof(SectionTableSession.DesignSectionIndex)));
        top.Children.Add(sections);

        var sizes = Row();
        sizes.Children.Add(new TextBlock { Text = session.TextHeightLabel, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        sizes.Children.Add(Input(nameof(SectionTableSession.TextHeightText), nameof(SectionTableSession.IsTextHeightValid), 60));
        top.Children.Add(sizes);

        var outputs = OutputOptions(
            ("Bảng", nameof(SectionTableSession.WriteTable)),
            ("CSV", nameof(SectionTableSession.WriteCsv)),
            ("Excel", nameof(SectionTableSession.WriteXlsx)));
        outputs.Children.Insert(0, new TextBlock { Text = "Xuất ra:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        top.Children.Add(outputs);
        top.Children.Add(new TextBlock
        {
            Text = "Bảng: vẽ dưới mỗi trắc ngang (đường TN_BANG, chữ TN_CHU); diện tích đào/đắp tính từ hai đường mặt cắt đã chọn. "
                   + "CSV, Excel: mỗi trắc ngang một dòng (<tên bản vẽ>_TRACNGANG). Chạy lại cho cùng trắc ngang sẽ thay bảng cũ.",
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
        _rows.SetBinding(Selector.SelectedIndexProperty, new Binding(nameof(SectionTableSession.SelectedRowIndex)) { Mode = BindingMode.TwoWay });
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

        SetLayout(top, BuildFooter(nameof(SectionTableSession.SummaryText), nameof(SectionTableSession.CanApply)), content);
    }

    protected override void CommitEdits() => _rows.CommitEdit(DataGridEditingUnit.Row, true);

    private static ComboBox Combo(string itemsPath, string indexPath)
    {
        var combo = new ComboBox { Width = 220, VerticalContentAlignment = VerticalAlignment.Center };
        combo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(itemsPath));
        combo.SetBinding(Selector.SelectedIndexProperty, new Binding(indexPath) { Mode = BindingMode.TwoWay });
        return combo;
    }

    /// <summary>A text box bound as the user types; red while validPath is false.</summary>
    internal static TextBox Input(string path, string validPath, double width)
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
