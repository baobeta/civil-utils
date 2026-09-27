using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using C3DTools.Core.Stations;

namespace C3DTools.Civil2021.Ui;

/// <summary>The CTPHATCOC dialog ("Phát sinh cọc"): group, half width, Phát sinh (range and spacings) or Chèn, and the stake list.</summary>
internal sealed class StakeGenerateWindow : ToolWindow
{
    public StakeGenerateWindow(StakeGenerationSession session)
        : base("CTPHATCOC", "Phát sinh cọc", 760, 620, 600, 460)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        DataContext = session;

        var top = new StackPanel();
        top.Children.Add(BuildHeader("Tuyến: " + session.SourceText));

        var group = Row();
        group.Children.Add(Caption("Nhóm cọc (Sample Line Group)", 210));
        group.Children.Add(Combo(nameof(StakeGenerationSession.GroupNames), nameof(StakeGenerationSession.GroupIndex), 200));
        group.Children.Add(Label("Tên nhóm mới"));
        var newName = StakeInputs.Text(nameof(StakeGenerationSession.NewGroupName), null, 150);
        newName.SetBinding(IsEnabledProperty, new Binding(nameof(StakeGenerationSession.IsNewGroup)));
        group.Children.Add(newName);
        top.Children.Add(group);

        var width = Row();
        width.Children.Add(Caption("Bề rộng nửa dải xác định trắc ngang (m)", 260));
        width.Children.Add(StakeInputs.Text(nameof(StakeGenerationSession.HalfWidthText), nameof(StakeGenerationSession.IsHalfWidthValid), 70));
        top.Children.Add(width);

        var generate = new RadioButton { Content = "Phát sinh", GroupName = "Mode", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2) };
        generate.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(StakeGenerationSession.GenerateMode)) { Mode = BindingMode.TwoWay });
        top.Children.Add(generate);
        var generateBox = new StackPanel { Margin = new Thickness(20, 0, 0, 0) };
        generateBox.SetBinding(IsEnabledProperty, new Binding(nameof(StakeGenerationSession.GenerateMode)));
        generateBox.Children.Add(Range("Từ khoảng dồn", nameof(StakeGenerationSession.FromText), DialogAction.PickFrom));
        generateBox.Children.Add(Range("Tới khoảng dồn", nameof(StakeGenerationSession.ToText), DialogAction.PickTo));
        var spacing = Row();
        spacing.Children.Add(Caption("Khoảng cách trong đoạn thẳng", 190));
        spacing.Children.Add(StakeInputs.Text(nameof(StakeGenerationSession.StraightSpacingText), nameof(StakeGenerationSession.IsSpacingValid), 70));
        spacing.Children.Add(Label("trong đoạn cong"));
        spacing.Children.Add(StakeInputs.Text(nameof(StakeGenerationSession.CurveSpacingText), nameof(StakeGenerationSession.IsSpacingValid), 70));
        generateBox.Children.Add(spacing);
        top.Children.Add(generateBox);

        var insert = new RadioButton { Content = "Chèn", GroupName = "Mode", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2) };
        insert.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(StakeGenerationSession.InsertMode)) { Mode = BindingMode.TwoWay });
        top.Children.Add(insert);
        var insertBox = new StackPanel { Margin = new Thickness(20, 0, 0, 0) };
        insertBox.SetBinding(IsEnabledProperty, new Binding(nameof(StakeGenerationSession.InsertMode)));
        var stations = Row();
        stations.Children.Add(Caption("Lý trình cọc chèn", 190));
        var list = StakeInputs.Text(nameof(StakeGenerationSession.InsertStationsText), nameof(StakeGenerationSession.IsInsertValid), 260);
        list.ToolTip = "Cách nhau bởi dấu ; hoặc khoảng trắng, ví dụ: Km0+125.5; 0+310; 455,2";
        stations.Children.Add(list);
        stations.Children.Add(new Border { Width = 8 });
        stations.Children.Add(ActionButton("Chỉ điểm…", DialogAction.PickPoints));
        insertBox.Children.Add(stations);
        insertBox.Children.Add(Check("Kiểu cọc phụ (đặt tên theo cọc trước: C5a, C5b)", nameof(StakeGenerationSession.SubStakeStyle)));
        top.Children.Add(insertBox);

        top.Children.Add(new TextBlock
        {
            Text = "Phát sinh: cọc chi tiết tại lý trình chẵn theo khoảng cách, thêm mọi cọc H, Km và cọc chủ yếu (NĐ, TĐ, P, TC, NC); " +
                   "toàn bộ cọc của nhóm được đặt lại tên theo quy tắc mặc định. Dùng CTDANHCOC để đặt tên theo quy tắc khác.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.DimGray,
            Margin = new Thickness(0, 6, 0, 6),
        });

        var grid = StakeInputs.Grid(nameof(StakeGenerationSession.PreviewRows),
            ("Tên cọc", nameof(StakePreviewLine.Name), 110), ("Lý trình", nameof(StakePreviewLine.Station), 110),
            ("Loại", nameof(StakePreviewLine.Kind), 90), ("Ghi chú", nameof(StakePreviewLine.Status), 0));
        SetLayout(top, BuildFooter(nameof(StakeGenerationSession.SummaryText), nameof(StakeGenerationSession.CanApply)), grid);
    }

    private StackPanel Range(string caption, string path, DialogAction pick)
    {
        var row = Row();
        row.Children.Add(Caption(caption, 190));
        row.Children.Add(StakeInputs.Text(path, nameof(StakeGenerationSession.IsRangeValid), 130));
        var button = ActionButton("…", pick);
        button.MinWidth = 30;
        button.Margin = new Thickness(4, 0, 0, 0);
        button.ToolTip = "Chọn điểm trên bản vẽ";
        row.Children.Add(button);
        return row;
    }

    private static TextBlock Caption(string text, double width) =>
        new TextBlock { Text = text, Width = width, VerticalAlignment = VerticalAlignment.Center };

    private static ComboBox Combo(string items, string index, double width)
    {
        var combo = new ComboBox { Width = width, VerticalContentAlignment = VerticalAlignment.Center };
        combo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(items));
        combo.SetBinding(Selector.SelectedIndexProperty, new Binding(index) { Mode = BindingMode.TwoWay });
        return combo;
    }
}

/// <summary>The CTDANHCOC dialog ("Đánh lại toàn bộ tên cọc"), with AND Design's options and a live old → new list.</summary>
internal sealed class StakeRenameWindow : ToolWindow
{
    public StakeRenameWindow(StakeRenameSession session)
        : base("CTDANHCOC", "Đánh lại toàn bộ tên cọc", 720, 640, 560, 480)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        DataContext = session;

        var top = new StackPanel();
        top.Children.Add(BuildHeader("Nhóm cọc: " + session.SourceText));

        var form = new Grid { Margin = new Thickness(0, 6, 0, 6) };
        for (var c = 0; c < 4; c++) form.ColumnDefinitions.Add(new ColumnDefinition { Width = c % 2 == 0 ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });
        var row = 0;
        void Line(UIElement a, UIElement b = null, UIElement c = null, UIElement d = null)
        {
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var cells = new[] { a, b, c, d };
            for (var i = 0; i < cells.Length; i++)
            {
                if (cells[i] == null) continue;
                if (cells[i] is FrameworkElement f) f.Margin = new Thickness(i % 2 == 0 ? 0 : 4, 3, 12, 3);
                Grid.SetRow(cells[i], row);
                Grid.SetColumn(cells[i], i);
                form.Children.Add(cells[i]);
            }

            row++;
        }

        Line(Text("Từ cọc"), Choice(nameof(StakeRenameSession.FromIndex)));
        Line(Text("Tới cọc"), Choice(nameof(StakeRenameSession.ToIndex)));
        var keep = StakeInputs.Text(nameof(StakeRenameSession.KeepPrefixesText), null, 0);
        keep.ToolTip = "Các tiếp đầu cách nhau bởi dấu ; ví dụ: CT; CONG";
        Line(Text("Để lại các cọc có tiếp đầu"), keep);
        Line(Check("Đánh lại cọc cắm cong, siêu cao", nameof(StakeRenameSession.RenameCurveKeys)), null,
            Text("Số thứ tự đỉnh đầu"), StakeInputs.Text(nameof(StakeRenameSession.FirstPiNumberText), nameof(StakeRenameSession.IsFirstPiValid), 60));
        Line(Check("Tên cọc theo kiểu lý trình", nameof(StakeRenameSession.NameByStation)), null,
            Text("Tiếp đầu của cọc"), StakeInputs.Text(nameof(StakeRenameSession.DetailPrefix), null, 60));
        Line(Text("Số thứ tự cọc đầu"), StakeInputs.Text(nameof(StakeRenameSession.FirstDetailNumberText), nameof(StakeRenameSession.IsFirstDetailValid), 60));
        Line(Check("Không tạo cọc H", nameof(StakeRenameSession.NoHundreds)), null, Check("Cọc H liên tục", nameof(StakeRenameSession.ContinuousThroughH)));
        Line(Check("Thứ tự cọc quay lại theo KM", nameof(StakeRenameSession.RestartPerKm)));
        Line(Check("Không đánh số quay lại khi TT>=100", nameof(StakeRenameSession.NoRestartFrom100)));
        top.Children.Add(form);

        var grid = StakeInputs.Grid(nameof(StakeRenameSession.PreviewRows),
            ("Lý trình", nameof(StakeRenameLine.Station), 120), ("Tên cũ", nameof(StakeRenameLine.OldName), 130), ("Tên mới", nameof(StakeRenameLine.NewName), 0));
        var changed = new Style(typeof(DataGridRow));
        var trigger = new DataTrigger { Binding = new Binding(nameof(StakeRenameLine.Changed)), Value = true };
        trigger.Setters.Add(new Setter(BackgroundProperty, WarningBrush));
        changed.Triggers.Add(trigger);
        grid.RowStyle = changed;
        SetLayout(top, BuildFooter(nameof(StakeRenameSession.SummaryText), nameof(StakeRenameSession.CanApply)), grid);
    }

    private static TextBlock Text(string text) => new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };

    private static ComboBox Choice(string indexPath)
    {
        var combo = new ComboBox { VerticalContentAlignment = VerticalAlignment.Center, MinWidth = 180 };
        combo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(StakeRenameSession.StakeChoices)));
        combo.SetBinding(Selector.SelectedIndexProperty, new Binding(indexPath) { Mode = BindingMode.TwoWay });
        return combo;
    }
}

/// <summary>Inputs shared by the stake dialogs.</summary>
internal static class StakeInputs
{
    /// <summary>A text box bound as the user types; red while validPath is false.</summary>
    public static TextBox Text(string path, string validPath, double width)
    {
        var box = new TextBox { VerticalContentAlignment = VerticalAlignment.Center };
        if (width > 0) box.Width = width;
        box.SetBinding(TextBox.TextProperty, new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        if (validPath == null) return box;
        var style = new Style(typeof(TextBox));
        var invalid = new DataTrigger { Binding = new Binding(validPath), Value = false };
        invalid.Setters.Add(new Setter(Control.BackgroundProperty, ToolWindow.ErrorBrush));
        style.Triggers.Add(invalid);
        box.Style = style;
        return box;
    }

    /// <summary>Read-only grid over a collection; width 0 fills the rest.</summary>
    public static DataGrid Grid(string itemsPath, params (string header, string path, double width)[] columns)
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserSortColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
        };
        grid.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(itemsPath));
        foreach (var (header, path, width) in columns)
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(path),
                Width = width > 0 ? new DataGridLength(width) : new DataGridLength(1, DataGridLengthUnitType.Star),
            });
        return grid;
    }
}
