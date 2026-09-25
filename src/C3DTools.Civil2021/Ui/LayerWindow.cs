using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using C3DTools.Core.Drawing;

namespace C3DTools.Civil2021.Ui;

/// <summary>The CTLAYER dialog: one row per layer (objects, target layer, colour), options and "Lưu vào preset".</summary>
internal sealed class LayerWindow : ToolWindow
{
    private readonly DataGrid _grid;

    /// <param name="savePreset">Writes the mapping to the drawing's preset file and returns the message to show.</param>
    public LayerWindow(LayerMapSession session, string sourceText, Func<string> savePreset)
        : base("CTLAYER", "Chuẩn hoá layer theo preset", 760, 540, 560, 380)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        DataContext = session;

        var top = new StackPanel();
        top.Children.Add(BuildHeader(sourceText));
        var options = Row();
        options.Children.Add(Check("Áp dụng cho block", nameof(LayerMapSession.ApplyToBlocks)));
        options.Children.Add(Check("Xoá layer rỗng", nameof(LayerMapSession.PurgeEmpty)));
        var saved = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.DimGray };
        var save = Button("Lưu vào preset", (s, e) =>
        {
            CommitGrid();
            saved.Text = savePreset?.Invoke() ?? "";
        });
        save.SetBinding(IsEnabledProperty, new Binding(nameof(LayerMapSession.IsValid)));
        options.Children.Add(save);
        options.Children.Add(saved);
        top.Children.Add(options);
        top.Children.Add(new TextBlock
        {
            Text = "Layer đích để trống: giữ nguyên. Màu (1–255) chỉ dùng khi phải tạo layer đích mới.",
            Foreground = System.Windows.Media.Brushes.DimGray,
            Margin = new Thickness(0, 2, 0, 6),
            TextWrapping = TextWrapping.Wrap,
        });

        _grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserSortColumns = true,
            SelectionMode = DataGridSelectionMode.Single,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            ItemsSource = session.Rows,
        };
        _grid.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty,
            new Binding(nameof(LayerMapSession.SelectedRow)) { Mode = BindingMode.TwoWay });
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Layer",
            Binding = new Binding(nameof(LayerMapRow.Name)),
            IsReadOnly = true,
            CellStyle = GreyCell(),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
        });
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Số đối tượng",
            Binding = new Binding(nameof(LayerMapRow.ObjectCount)),
            IsReadOnly = true,
            CellStyle = GreyCell(),
            ElementStyle = NumberCellStyle(),
            Width = new DataGridLength(90),
        });
        _grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Layer đích",
            CellTemplate = TargetTemplate(),
            SortMemberPath = nameof(LayerMapRow.Target),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
        });
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Màu",
            Binding = new Binding(nameof(LayerMapRow.ColorText)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
            ElementStyle = NumberCellStyle(),
            Width = new DataGridLength(60),
        });
        _grid.RowStyle = RowStyle();
        _grid.Loaded += (s, e) =>
        {
            if (session.SelectedRow != null) _grid.ScrollIntoView(session.SelectedRow);
        };

        SetLayout(top, BuildFooter(nameof(LayerMapSession.SummaryText), nameof(LayerMapSession.CanApply)), _grid);
    }

    protected override void CommitEdits() => CommitGrid();

    private void CommitGrid()
    {
        _grid.CommitEdit(DataGridEditingUnit.Cell, true);
        _grid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    /// <summary>An editable combo (preset layers + free text) shown in every row, bound to Target as the user types.</summary>
    private static DataTemplate TargetTemplate()
    {
        var combo = new FrameworkElementFactory(typeof(ComboBox));
        combo.SetValue(ComboBox.IsEditableProperty, true);
        combo.SetValue(ComboBox.IsTextSearchEnabledProperty, false);
        combo.SetValue(BorderThicknessProperty, new Thickness(0));
        combo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("DataContext." + nameof(LayerMapSession.TargetNames))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGrid), 1),
        });
        combo.SetBinding(ComboBox.TextProperty, new Binding(nameof(LayerMapRow.Target))
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        });
        return new DataTemplate { VisualTree = combo };
    }

    private static Style RowStyle()
    {
        var style = new Style(typeof(DataGridRow));
        var invalid = new DataTrigger { Binding = new Binding(nameof(LayerMapRow.IsValid)), Value = false };
        invalid.Setters.Add(new Setter(BackgroundProperty, ErrorBrush));
        style.Triggers.Add(invalid);
        var moving = new DataTrigger { Binding = new Binding(nameof(LayerMapRow.WillMove)), Value = true };
        moving.Setters.Add(new Setter(FontWeightProperty, FontWeights.SemiBold));
        style.Triggers.Add(moving);
        return style;
    }
}
