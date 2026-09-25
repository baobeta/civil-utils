using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using C3DTools.Core.Profiles;

namespace C3DTools.Civil2021.Ui;

/// <summary>The CTCONGDUNG dialog: profile view, design profile, V, the PVIs with their elements and warnings (read-only), outputs.</summary>
internal sealed class VerticalCurveWindow : ToolWindow
{
    public VerticalCurveWindow(VerticalCurveSession session)
        : base("CTCONGDUNG", "Yếu tố cong đứng", 980, 560, 640, 380)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        DataContext = session;

        var top = new StackPanel();
        top.Children.Add(BuildHeader("Trắc dọc: " + session.SourceText));

        var profile = Row();
        profile.Children.Add(new TextBlock { Text = "Trắc dọc thiết kế", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        var profiles = new ComboBox { Width = 220, VerticalContentAlignment = VerticalAlignment.Center };
        profiles.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(VerticalCurveSession.ProfileNames)));
        profiles.SetBinding(Selector.SelectedIndexProperty, new Binding(nameof(VerticalCurveSession.ProfileIndex)) { Mode = BindingMode.TwoWay });
        profile.Children.Add(profiles);
        profile.Children.Add(Label("V"));
        var speed = new ComboBox { Width = 70, ItemsSource = session.AvailableSpeeds };
        speed.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(VerticalCurveSession.DesignSpeed)) { Mode = BindingMode.TwoWay });
        profile.Children.Add(speed);
        profile.Children.Add(new TextBlock { Text = "km/h", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
        top.Children.Add(profile);

        var rmin = new TextBlock { Margin = new Thickness(0, 4, 0, 4) };
        rmin.SetBinding(TextBlock.TextProperty, new Binding(nameof(VerticalCurveSession.RminText)));
        top.Children.Add(rmin);

        var outputs = OutputOptions(
            ("Khung", nameof(VerticalCurveSession.WriteBox)),
            ("Bảng", nameof(VerticalCurveSession.WriteTable)),
            ("CSV", nameof(VerticalCurveSession.WriteCsv)),
            ("Excel", nameof(VerticalCurveSession.WriteXlsx)));
        outputs.Children.Insert(0, new TextBlock { Text = "Xuất ra:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        top.Children.Add(outputs);
        top.Children.Add(new TextBlock
        {
            Text = "Khung: đặt trên mỗi đỉnh trong trắc dọc (layer TD_YTC). Bảng: chọn điểm chèn sau khi bấm Áp dụng. CSV, Excel: ghi cạnh bản vẽ (<tên bản vẽ>_CONGDUNG). Chạy lại cho cùng trắc dọc sẽ thay khung và bảng cũ.",
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
            ItemsSource = session.Rows,
        };
        var rowStyle = new Style(typeof(DataGridRow));
        var warning = new DataTrigger { Binding = new Binding(nameof(VerticalCurveRow.HasWarning)), Value = true };
        warning.Setters.Add(new Setter(BackgroundProperty, WarningBrush));
        rowStyle.Triggers.Add(warning);
        grid.RowStyle = rowStyle;

        var headers = VerticalCurveTableBuilder.Headers;
        var paths = new[]
        {
            nameof(VerticalCurveRow.Name), nameof(VerticalCurveRow.Station), nameof(VerticalCurveRow.PviElevation),
            nameof(VerticalCurveRow.GradeIn), nameof(VerticalCurveRow.GradeOut), nameof(VerticalCurveRow.A),
            nameof(VerticalCurveRow.R), nameof(VerticalCurveRow.K), nameof(VerticalCurveRow.T), nameof(VerticalCurveRow.E),
            nameof(VerticalCurveRow.StartStation), nameof(VerticalCurveRow.EndStation), nameof(VerticalCurveRow.HighLow),
            nameof(VerticalCurveRow.Warning),
        };
        for (var i = 0; i < paths.Length; i++)
        {
            var isText = i == 0 || i >= paths.Length - 2;
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = headers[i],
                Binding = new Binding(paths[i]),
                ElementStyle = isText ? null : NumberCellStyle(),
                Width = i == paths.Length - 1 ? new DataGridLength(1, DataGridLengthUnitType.Star) : DataGridLength.Auto,
                MinWidth = i == paths.Length - 1 ? 160 : 40,
            });
        }

        SetLayout(top, BuildFooter(nameof(VerticalCurveSession.SummaryText), nameof(VerticalCurveSession.CanApply)), grid);
    }
}
