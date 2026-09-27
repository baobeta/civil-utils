using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using C3DTools.Core.Curves;

namespace C3DTools.Civil2021.Ui;

/// <summary>The CTTUYEN dialog ("Tạo tuyến cho bình đồ mới"), bound to a RouteCreationSession.</summary>
internal sealed class RouteCreateWindow : ToolWindow
{
    private readonly RouteCreationSession _session;
    private readonly Func<string, IList<string>> _readAssemblies;
    private readonly TextBlock _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.DarkRed };

    /// <param name="readAssemblies">Assembly names of a DWG ("Tệp mặt cắt"); throws with a Vietnamese message.</param>
    public RouteCreateWindow(RouteCreationSession session, Func<string, IList<string>> readAssemblies)
        : base("CTTUYEN", "Tạo tuyến cho bình đồ mới", 640, 560, 560, 480)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _readAssemblies = readAssemblies;
        DataContext = session;

        var top = Row();
        top.Children.Add(new TextBlock { Text = "Tim tuyến: " + session.SourceText, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        top.Children.Add(ActionButton("Theo polyline…", DialogAction.Pick));
        top.Children.Add(ActionButton("Chỉ điểm…", DialogAction.PickPoints));

        var form = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var row = 0;
        void Add(string label, UIElement input)
        {
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 10, 3) };
            Grid.SetRow(text, row);
            form.Children.Add(text);
            if (input is FrameworkElement f) f.Margin = new Thickness(0, 3, 0, 3);
            Grid.SetRow(input, row);
            Grid.SetColumn(input, 1);
            form.Children.Add(input);
            row++;
        }

        var nameAndScale = Row();
        nameAndScale.Margin = new Thickness(0);
        nameAndScale.Children.Add(Text(nameof(RouteCreationSession.Name), nameof(RouteCreationSession.IsNameValid), 200));
        nameAndScale.Children.Add(Label("Tỉ lệ bình đồ 1/"));
        nameAndScale.Children.Add(Text(nameof(RouteCreationSession.ScaleText), nameof(RouteCreationSession.IsScaleValid), 70));
        Add("Tên đường tuyến", nameAndScale);
        Add("Mô tả", Text(nameof(RouteCreationSession.Description), null, 0));

        var startAndSpeed = Row();
        startAndSpeed.Margin = new Thickness(0);
        startAndSpeed.Children.Add(Text(nameof(RouteCreationSession.StartStationText), nameof(RouteCreationSession.IsStartValid), 110));
        startAndSpeed.Children.Add(Label("Vận tốc thiết kế"));
        var speed = new ComboBox { Width = 70, ItemsSource = session.AvailableSpeeds };
        speed.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(RouteCreationSession.DesignSpeed)) { Mode = BindingMode.TwoWay });
        startAndSpeed.Children.Add(speed);
        startAndSpeed.Children.Add(new TextBlock { Text = "km/h", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
        Add("Lý trình đầu", startAndSpeed);

        Add("Kiểu Alignment", Combo(nameof(RouteCreationSession.StyleNames), nameof(RouteCreationSession.StyleIndex)));
        Add("Bộ nhãn", Combo(nameof(RouteCreationSession.LabelSetNames), nameof(RouteCreationSession.LabelSetIndex)));
        Add("Layer", Text(nameof(RouteCreationSession.LayerName), nameof(RouteCreationSession.IsLayerValid), 200));
        Add("Trắc dọc tự nhiên từ", Combo(nameof(RouteCreationSession.SurfaceNames), nameof(RouteCreationSession.SurfaceIndex)));

        var file = new DockPanel();
        var browse = Button("…", (s, e) => Browse());
        browse.MinWidth = 30;
        browse.Margin = new Thickness(4, 0, 0, 0);
        DockPanel.SetDock(browse, Dock.Right);
        file.Children.Add(browse);
        var path = new TextBox { IsReadOnly = true, Background = ReadOnlyBrush, VerticalContentAlignment = VerticalAlignment.Center };
        path.SetBinding(TextBox.TextProperty, new Binding(nameof(RouteCreationSession.SectionFile)) { Mode = BindingMode.OneWay });
        file.Children.Add(path);
        Add("Tệp mặt cắt (DWG)", file);
        Add("", Check("Tải toàn bộ mặt cắt trong tệp", nameof(RouteCreationSession.LoadAllAssemblies)));
        Add("Mặt cắt cho tuyến", Combo(nameof(RouteCreationSession.AssemblyNames), nameof(RouteCreationSession.AssemblyIndex)));

        var height = new TextBlock { Foreground = System.Windows.Media.Brushes.DimGray };
        height.SetBinding(TextBlock.TextProperty, new Binding(nameof(RouteCreationSession.TextHeightText)));
        Add("", height);
        Add("", Check("Bố trí cong ngay sau khi tạo (mở CTYTC)", nameof(RouteCreationSession.OpenCurveDesign)));
        Add("", _status);

        var content = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        SetLayout(top, BuildFooter(nameof(RouteCreationSession.SummaryText), nameof(RouteCreationSession.CanApply)), content);
    }

    private void Browse()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Bản vẽ AutoCAD (*.dwg)|*.dwg", Title = "Chọn tệp mặt cắt (DWG chứa Assembly)" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var names = _readAssemblies?.Invoke(dialog.FileName) ?? new List<string>();
            _session.SetFileAssemblies(dialog.FileName, names);
            _status.Text = names.Count == 0 ? "Tệp không có Assembly nào." : "";
        }
        catch (Exception ex)
        {
            _status.Text = "Không đọc được tệp mặt cắt: " + ex.Message;
        }
    }

    private static TextBox Text(string path, string validPath, double width)
    {
        var box = new TextBox { VerticalContentAlignment = VerticalAlignment.Center };
        if (width > 0) box.Width = width;
        box.SetBinding(TextBox.TextProperty, new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        if (validPath != null)
        {
            var style = new Style(typeof(TextBox));
            var invalid = new DataTrigger { Binding = new Binding(validPath), Value = false };
            invalid.Setters.Add(new Setter(BackgroundProperty, ErrorBrush));
            style.Triggers.Add(invalid);
            box.Style = style;
        }

        return box;
    }

    private static ComboBox Combo(string itemsPath, string indexPath)
    {
        var combo = new ComboBox { VerticalContentAlignment = VerticalAlignment.Center };
        combo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(itemsPath));
        combo.SetBinding(Selector.SelectedIndexProperty, new Binding(indexPath) { Mode = BindingMode.TwoWay });
        return combo;
    }
}
