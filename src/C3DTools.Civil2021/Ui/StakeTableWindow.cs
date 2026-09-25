using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using C3DTools.Core.Stations;

namespace C3DTools.Civil2021.Ui;

/// <summary>The CTTOADO dialog: alignment, which stakes, surface for Z, outputs, and the table as it will be written.</summary>
internal sealed class StakeTableWindow : ToolWindow
{
    public StakeTableWindow(StakeTableSession session)
        : base("CTTOADO", "Bảng toạ độ cọc", 760, 560, 560, 400)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        DataContext = session;

        var top = new StackPanel();
        top.Children.Add(BuildHeader("Tuyến: " + session.SourceText));

        var stakes = Row();
        stakes.Children.Add(new TextBlock { Text = "Khoảng cách cọc (m)", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        stakes.Children.Add(Input(nameof(StakeTableSession.IntervalText), nameof(StakeTableSession.IsIntervalValid), 60));
        stakes.Children.Add(new Border { Width = 12 });
        stakes.Children.Add(Check("Cọc đường cong (NĐ, TĐ, P, TC, NC)", nameof(StakeTableSession.IncludeCurveStakes)));
        stakes.Children.Add(Check("Điểm hình học của tuyến", nameof(StakeTableSession.IncludeGeometryPoints)));
        top.Children.Add(stakes);

        var extras = Row();
        extras.Children.Add(new TextBlock { Text = "Cọc thêm (lý trình)", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        var extraBox = Input(nameof(StakeTableSession.ExtraStationsText), nameof(StakeTableSession.IsExtrasValid), 300);
        extraBox.ToolTip = "Cách nhau bởi dấu ; hoặc khoảng trắng, ví dụ: Km0+125.5; 0+310; 455,2";
        extras.Children.Add(extraBox);
        top.Children.Add(extras);

        var surface = Row();
        surface.Children.Add(new TextBlock { Text = "Cao độ Z từ mặt phủ", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        var combo = new ComboBox { Width = 220, VerticalContentAlignment = VerticalAlignment.Center };
        combo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(StakeTableSession.SurfaceNames)));
        combo.SetBinding(Selector.SelectedIndexProperty, new Binding(nameof(StakeTableSession.SurfaceIndex)) { Mode = BindingMode.TwoWay });
        surface.Children.Add(combo);
        top.Children.Add(surface);

        var outputs = OutputOptions(
            ("Bảng", nameof(StakeTableSession.WriteTable)),
            ("CSV", nameof(StakeTableSession.WriteCsv)),
            ("Excel", nameof(StakeTableSession.WriteXlsx)),
            ("Điểm COGO", nameof(StakeTableSession.WriteCogo)));
        outputs.Children.Insert(0, new TextBlock { Text = "Xuất ra:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        top.Children.Add(outputs);
        top.Children.Add(new TextBlock
        {
            Text = "Bảng: chọn điểm chèn sau khi bấm Áp dụng. CSV, Excel: ghi cạnh bản vẽ (<tên bản vẽ>_TOADO). Chạy lại cho cùng tuyến sẽ thay bảng và điểm COGO cũ.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.DimGray,
            Margin = new Thickness(0, 2, 0, 6),
        });

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserSortColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            ItemsSource = session.PreviewRows,
        };
        grid.Columns.Add(Column("STT", nameof(StakePreviewRow.Number), 45, number: true));
        grid.Columns.Add(Column("Tên cọc", nameof(StakePreviewRow.Name), 90, number: false));
        grid.Columns.Add(Column("Lý trình", nameof(StakePreviewRow.Station), 100, number: true));
        grid.Columns.Add(Column("X", nameof(StakePreviewRow.X), 0, number: true));
        grid.Columns.Add(Column("Y", nameof(StakePreviewRow.Y), 0, number: true));
        var z = Column("Z", nameof(StakePreviewRow.Z), 80, number: true);
        z.Visibility = session.HasZ ? Visibility.Visible : Visibility.Collapsed;
        grid.Columns.Add(z);

        SetLayout(top, BuildFooter(nameof(StakeTableSession.SummaryText), nameof(StakeTableSession.CanApply)), grid);
    }

    /// <summary>width 0: share the remaining width.</summary>
    private static DataGridTextColumn Column(string header, string path, double width, bool number) =>
        new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(path),
            ElementStyle = number ? NumberCellStyle() : null,
            Width = width > 0 ? new DataGridLength(width) : new DataGridLength(1, DataGridLengthUnitType.Star),
        };

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
