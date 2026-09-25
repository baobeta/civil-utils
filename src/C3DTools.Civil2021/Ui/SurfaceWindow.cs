using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using C3DTools.Core.Surfaces;

namespace C3DTools.Civil2021.Ui;

/// <summary>The CTMATDIA dialog: surface, then tab "Xoá tam giác dài" or tab "Ghi cao độ đồng mức".</summary>
internal sealed class SurfaceWindow : ToolWindow
{
    public SurfaceWindow(SurfaceSession session)
        : base("CTMATDIA", "Mặt địa hình", 620, 400, 520, 340)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        DataContext = session;

        var top = new StackPanel();
        var header = BuildHeader("");
        ((TextBlock)header.Children[0]).SetBinding(TextBlock.TextProperty, new Binding(nameof(SurfaceSession.SourceText)));
        top.Children.Add(header);

        var surface = Row();
        surface.Children.Add(new TextBlock { Text = "Mặt phủ (TIN)", Width = 150, VerticalAlignment = VerticalAlignment.Center });
        var combo = new ComboBox { Width = 240, VerticalContentAlignment = VerticalAlignment.Center };
        combo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(SurfaceSession.SurfaceNames)));
        combo.SetBinding(Selector.SelectedIndexProperty, new Binding(nameof(SurfaceSession.SurfaceIndex)) { Mode = BindingMode.TwoWay });
        surface.Children.Add(combo);
        top.Children.Add(surface);

        var tabs = new TabControl { Margin = new Thickness(0, 6, 0, 0) };
        tabs.Items.Add(new TabItem { Header = "Xoá tam giác dài", Content = CleanupTab(session) });
        tabs.Items.Add(new TabItem { Header = "Ghi cao độ đồng mức", Content = LabelTab() });
        tabs.SetBinding(Selector.SelectedIndexProperty, new Binding(nameof(SurfaceSession.TabIndex)) { Mode = BindingMode.TwoWay });

        SetLayout(top, BuildFooter(nameof(SurfaceSession.SummaryText), nameof(SurfaceSession.CanApply)), tabs);
    }

    private StackPanel CleanupTab(SurfaceSession session)
    {
        var panel = new StackPanel { Margin = new Thickness(8) };

        var edge = Row();
        edge.Children.Add(Check("Xoá tam giác có cạnh dài hơn", nameof(SurfaceSession.UseMaxEdge)));
        edge.Children.Add(Input(nameof(SurfaceSession.MaxEdgeText), nameof(SurfaceSession.IsMaxEdgeValid), 70));
        edge.Children.Add(new TextBlock { Text = "m", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
        panel.Children.Add(edge);

        var boundary = Row();
        boundary.Children.Add(new TextBlock
        {
            Text = "Ranh giới (tuỳ chọn): bấm Chọn trên bản vẽ… để chọn polyline kín; tam giác có đỉnh ngoài ranh giới bị xoá.",
            TextWrapping = TextWrapping.Wrap,
            Width = 420,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var clear = Button("Bỏ ranh giới", (s, e) => session.ClearBoundary());
        clear.SetBinding(IsEnabledProperty, new Binding(nameof(SurfaceSession.HasBoundary)));
        boundary.Children.Add(clear);
        panel.Children.Add(boundary);

        var count = Row();
        count.Margin = new Thickness(0, 8, 0, 2);
        var countButton = ActionButton("Đếm", DialogAction.Count);
        countButton.SetBinding(IsEnabledProperty, new Binding(nameof(SurfaceSession.CanApply)));
        count.Children.Add(countButton);
        var countText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        countText.SetBinding(TextBlock.TextProperty, new Binding(nameof(SurfaceSession.CountText)));
        count.Children.Add(countText);
        panel.Children.Add(count);

        panel.Children.Add(Note("Áp dụng xoá các cạnh TIN (TinSurface.DeleteLines), thêm một thao tác vào định nghĩa mặt phủ; một lần Undo để hoàn tác."));
        return panel;
    }

    private static StackPanel LabelTab()
    {
        var panel = new StackPanel { Margin = new Thickness(8) };

        var intervals = Row();
        intervals.Children.Add(new TextBlock { Text = "Đồng mức chính", Width = 110, VerticalAlignment = VerticalAlignment.Center });
        intervals.Children.Add(Input(nameof(SurfaceSession.MajorIntervalText), nameof(SurfaceSession.IsMajorValid), 60));
        intervals.Children.Add(Label("con"));
        intervals.Children.Add(Input(nameof(SurfaceSession.MinorIntervalText), nameof(SurfaceSession.IsMinorValid), 60));
        intervals.Children.Add(new TextBlock { Text = "m", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0) });
        var source = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = System.Windows.Media.Brushes.DimGray };
        source.SetBinding(TextBlock.TextProperty, new Binding(nameof(SurfaceSession.IntervalSourceText)));
        intervals.Children.Add(source);
        panel.Children.Add(intervals);

        var text = Row();
        text.Children.Add(new TextBlock { Text = "Chiều cao chữ", Width = 110, VerticalAlignment = VerticalAlignment.Center });
        text.Children.Add(Input(nameof(SurfaceSession.TextHeightText), nameof(SurfaceSession.IsTextHeightValid), 60));
        text.Children.Add(Label("Nhãn cách nhau ít nhất"));
        text.Children.Add(Input(nameof(SurfaceSession.SpacingText), nameof(SurfaceSession.IsSpacingValid), 60));
        text.Children.Add(new TextBlock { Text = "m", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
        panel.Children.Add(text);

        var minor = Row();
        minor.Children.Add(Check("Ghi cả đồng mức con", nameof(SurfaceSession.LabelMinor)));
        panel.Children.Add(minor);

        panel.Children.Add(Note("Chọn line/polyline cắt ngang các đường đồng mức. Nhãn là TEXT trên layer DH_CAODO, xoay theo đường đồng mức; chạy lại cho cùng đường sẽ thay nhãn cũ."));
        return panel;
    }

    private static TextBlock Note(string text) => new TextBlock
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Foreground = System.Windows.Media.Brushes.DimGray,
        Margin = new Thickness(0, 6, 0, 0),
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
