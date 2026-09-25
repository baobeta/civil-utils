using System;
using System.Windows;
using System.Windows.Controls;
using C3DTools.Core.Sections;

namespace C3DTools.Civil2021.Ui;

/// <summary>The CTXEPTRANG dialog: section views, paper size, margins, columns × rows, gap, scale, the Khung output and the sheet count.</summary>
internal sealed class SheetArrangeWindow : ToolWindow
{
    public SheetArrangeWindow(SheetArrangeSession session)
        : base("CTXEPTRANG", "Xếp trắc ngang vào tờ in", 620, 400, 520, 360)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        DataContext = session;

        var top = new StackPanel();
        top.Children.Add(BuildHeader("Trắc ngang: " + session.SourceText));

        var grid = new Grid { Margin = new Thickness(0, 6, 0, 6) };
        for (var c = 0; c < 4; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var row = 0;

        void Line(string label1, string path1, string valid1, string label2, string path2, string valid2)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Cell(new TextBlock { Text = label1, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 6, 2) }, row, 0);
            Cell(SectionTableWindow.Input(path1, valid1, 70), row, 1);
            if (label2 != null)
            {
                Cell(new TextBlock { Text = label2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 2, 6, 2) }, row, 2);
                Cell(SectionTableWindow.Input(path2, valid2, 70), row, 3);
            }

            row++;
        }

        void Cell(FrameworkElement element, int r, int c)
        {
            element.Margin = new Thickness(element.Margin.Left, 2, element.Margin.Right, 2);
            Grid.SetRow(element, r);
            Grid.SetColumn(element, c);
            grid.Children.Add(element);
        }

        var paper = string.IsNullOrEmpty(session.Paper) ? "" : " (" + session.Paper + ")";
        Line("Khổ giấy rộng (mm)" + paper, nameof(SheetArrangeSession.WidthText), nameof(SheetArrangeSession.IsWidthValid),
            "Cao (mm)", nameof(SheetArrangeSession.HeightText), nameof(SheetArrangeSession.IsHeightValid));
        Line("Lề trái (mm)", nameof(SheetArrangeSession.MarginLeftText), nameof(SheetArrangeSession.IsMarginLeftValid),
            "Lề phải (mm)", nameof(SheetArrangeSession.MarginRightText), nameof(SheetArrangeSession.IsMarginRightValid));
        Line("Lề trên (mm)", nameof(SheetArrangeSession.MarginTopText), nameof(SheetArrangeSession.IsMarginTopValid),
            "Lề dưới (mm)", nameof(SheetArrangeSession.MarginBottomText), nameof(SheetArrangeSession.IsMarginBottomValid));
        Line("Số cột mỗi tờ", nameof(SheetArrangeSession.ColumnsText), nameof(SheetArrangeSession.IsColumnsValid),
            "Số hàng mỗi tờ", nameof(SheetArrangeSession.RowsText), nameof(SheetArrangeSession.IsRowsValid));
        Line("Khoảng hở (mm)", nameof(SheetArrangeSession.GapText), nameof(SheetArrangeSession.IsGapValid),
            "Tỷ lệ 1:", nameof(SheetArrangeSession.ScaleText), nameof(SheetArrangeSession.IsScaleValid));
        top.Children.Add(grid);

        var outputs = OutputOptions(("Khung", nameof(SheetArrangeSession.WriteFrames)));
        outputs.Children.Insert(0, new TextBlock { Text = "Xuất ra:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        top.Children.Add(outputs);

        var note = new TextBlock
        {
            Text = "Áp dụng dời các trắc ngang (cùng bảng CTTRACNGANG của chúng) vào các ô của tờ theo thứ tự lý trình, từ trái sang phải, "
                   + "từ trên xuống. Tờ 1 đặt ở góc dưới trái của các trắc ngang đã chọn (hoặc chỗ tờ 1 lần xếp trước), các tờ tiếp theo "
                   + "sang phải. Khung: khung tờ và tên tờ trên layer TN_KHUNG (khối Resources\\A3.dwg nếu có). Đơn vị bản vẽ là mét.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.DimGray,
            Margin = new Thickness(0, 4, 0, 6),
        };

        SetLayout(top, BuildFooter(nameof(SheetArrangeSession.SummaryText), nameof(SheetArrangeSession.CanApply)), note);
    }
}
