using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using C3DTools.Core.Text;

namespace C3DTools.Civil2021.Ui;

/// <summary>The CTFONT dialog: scope, source/target encoding, options, detected encodings and the first 20 before/after pairs.</summary>
internal sealed class FontWindow : ToolWindow
{
    public FontWindow(FontConversionSession session, string sourceText)
        : base("CTFONT", "Chuyển mã font tiếng Việt", 760, 520, 560, 380)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        DataContext = session;

        var top = new StackPanel();
        var header = BuildHeader(sourceText);
        var selection = new RadioButton { Content = "Đối tượng đã chọn", GroupName = "Scope", Margin = new Thickness(8, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        selection.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(FontConversionSession.WholeDrawing)) { Mode = BindingMode.TwoWay, Converter = InverseBool.Instance });
        var whole = new RadioButton { Content = "Toàn bản vẽ", GroupName = "Scope", VerticalAlignment = VerticalAlignment.Center };
        whole.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(FontConversionSession.WholeDrawing)) { Mode = BindingMode.TwoWay });
        header.Children.Add(selection);
        header.Children.Add(whole);
        top.Children.Add(header);

        var encodings = Row();
        encodings.Children.Add(new TextBlock { Text = "Bảng mã nguồn", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        encodings.Children.Add(Combo(nameof(FontConversionSession.SourceNames), nameof(FontConversionSession.SourceIndex)));
        encodings.Children.Add(Label("Chuyển sang"));
        encodings.Children.Add(Combo(nameof(FontConversionSession.TargetNames), nameof(FontConversionSession.TargetIndex)));
        top.Children.Add(encodings);

        var options = Row();
        var styleFont = Check("Đổi font kiểu chữ (Text Style) sang " + session.TargetFont, nameof(FontConversionSession.ReplaceStyleFont));
        styleFont.SetBinding(IsEnabledProperty, new Binding(nameof(FontConversionSession.CanReplaceStyleFont)));
        options.Children.Add(styleFont);
        options.Children.Add(Check("Chuyển cả chữ trong định nghĩa block", nameof(FontConversionSession.ConvertBlockDefinitions)));
        top.Children.Add(options);

        var detected = new TextBlock { Margin = new Thickness(0, 4, 0, 6), TextWrapping = TextWrapping.Wrap };
        detected.SetBinding(TextBlock.TextProperty, new Binding(nameof(FontConversionSession.DetectedText)) { StringFormat = "Nhận dạng:\n{0}" });
        top.Children.Add(detected);

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserSortColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            ItemsSource = session.Preview,
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Loại", Binding = new Binding(nameof(FontPreviewRow.Kind)), Width = new DataGridLength(130) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Trước", Binding = new Binding(nameof(FontPreviewRow.Before)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Sau", Binding = new Binding(nameof(FontPreviewRow.After)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });

        SetLayout(top, BuildFooter(nameof(FontConversionSession.SummaryText), nameof(FontConversionSession.CanApply)), grid);
    }

    private static ComboBox Combo(string itemsPath, string indexPath)
    {
        var combo = new ComboBox { Width = 140, VerticalContentAlignment = VerticalAlignment.Center };
        combo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(itemsPath));
        combo.SetBinding(Selector.SelectedIndexProperty, new Binding(indexPath) { Mode = BindingMode.TwoWay });
        return combo;
    }
}
