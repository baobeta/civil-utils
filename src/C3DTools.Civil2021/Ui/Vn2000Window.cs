using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using C3DTools.Core.Geodesy;

namespace C3DTools.Civil2021.Ui;

/// <summary>The CTVN2000 dialog: target, from/to meridian (province or typed) and zone width, preview of the first points.</summary>
internal sealed class Vn2000Window : ToolWindow
{
    public Vn2000Window(Vn2000Session session)
        : base("CTVN2000", "Chuyển kinh tuyến trục VN-2000", 760, 520, 560, 380)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        DataContext = session;

        var top = new StackPanel();
        var header = BuildHeader("");
        ((TextBlock)header.Children[0]).SetBinding(TextBlock.TextProperty,
            new Binding(nameof(Vn2000Session.SourceText)) { StringFormat = "Đối tượng: {0}" });
        top.Children.Add(header);

        var target = Row();
        target.Children.Add(Radio("Đối tượng chọn (điểm, line, polyline, cung, block, text)", nameof(Vn2000Session.TargetSelection)));
        target.Children.Add(Radio("Tất cả điểm COGO", nameof(Vn2000Session.TargetCogo)));
        top.Children.Add(target);

        top.Children.Add(MeridianRow("Từ kinh tuyến trục", session, nameof(Vn2000Session.FromText), nameof(Vn2000Session.FromIsInvalid), nameof(Vn2000Session.FromZoneWidth)));
        top.Children.Add(MeridianRow("Sang kinh tuyến trục", session, nameof(Vn2000Session.ToText), nameof(Vn2000Session.ToIsInvalid), nameof(Vn2000Session.ToZoneWidth)));

        var note = "Chọn tỉnh hoặc gõ kinh tuyến (105°45', 105 45 hoặc 105.75). Múi 3°: k0 = 0,9999; múi 6°: k0 = 0,9996.";
        if (session.HasUnverifiedProvinces) note += " Kinh tuyến theo tỉnh trong preset chưa được đối chiếu văn bản – hãy kiểm tra trước khi dùng.";
        top.Children.Add(new TextBlock
        {
            Text = note,
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.DimGray,
            Margin = new Thickness(0, 4, 0, 2),
        });
        var distortion = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 6) };
        distortion.SetBinding(TextBlock.TextProperty, new Binding(nameof(Vn2000Session.DistortionText)));
        top.Children.Add(distortion);

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserSortColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            ItemsSource = session.Rows,
        };
        grid.Columns.Add(Column("Điểm", nameof(Vn2000PreviewRow.Name), false));
        grid.Columns.Add(Column("X trước", nameof(Vn2000PreviewRow.XBefore), true));
        grid.Columns.Add(Column("Y trước", nameof(Vn2000PreviewRow.YBefore), true));
        grid.Columns.Add(Column("X sau", nameof(Vn2000PreviewRow.XAfter), true));
        grid.Columns.Add(Column("Y sau", nameof(Vn2000PreviewRow.YAfter), true));
        grid.Columns.Add(Column("Dịch chuyển (m)", nameof(Vn2000PreviewRow.Shift), true));

        SetLayout(top, BuildFooter(nameof(Vn2000Session.SummaryText), nameof(Vn2000Session.CanApply)), grid);
    }

    private static StackPanel MeridianRow(string label, Vn2000Session session, string textPath, string invalidPath, string zonePath)
    {
        var row = Row();
        row.Children.Add(new TextBlock { Text = label, Width = 130, VerticalAlignment = VerticalAlignment.Center });
        var combo = new ComboBox { Width = 240, IsEditable = true, ItemsSource = session.MeridianChoices, VerticalContentAlignment = VerticalAlignment.Center };
        combo.SetBinding(ComboBox.TextProperty, new Binding(textPath) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        var style = new Style(typeof(ComboBox));
        var invalid = new DataTrigger { Binding = new Binding(invalidPath), Value = true };
        invalid.Setters.Add(new Setter(BackgroundProperty, ErrorBrush));
        invalid.Setters.Add(new Setter(BorderBrushProperty, System.Windows.Media.Brushes.Red));
        style.Triggers.Add(invalid);
        combo.Style = style;
        row.Children.Add(combo);
        row.Children.Add(Label("Múi"));
        var zone = new ComboBox { Width = 60, ItemsSource = session.ZoneWidths, ItemStringFormat = "{0}°" };
        zone.SetBinding(Selector.SelectedItemProperty, new Binding(zonePath) { Mode = BindingMode.TwoWay });
        row.Children.Add(zone);
        return row;
    }

    private static RadioButton Radio(string text, string path)
    {
        var radio = new RadioButton { Content = text, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
        radio.SetBinding(ToggleButton.IsCheckedProperty, new Binding(path) { Mode = BindingMode.TwoWay });
        return radio;
    }

    private static DataGridTextColumn Column(string header, string path, bool number) =>
        new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(path),
            ElementStyle = number ? NumberCellStyle() : null,
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
        };
}
