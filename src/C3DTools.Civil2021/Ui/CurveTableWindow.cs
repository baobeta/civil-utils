using System;
using System.Windows;
using System.Windows.Controls;
using C3DTools.Core.Curves;

namespace C3DTools.Civil2021.Ui;

/// <summary>The CTYTCBANG dialog: the route and the outputs (Bảng / CSV / Excel), bound to CurveTableOptions.</summary>
internal sealed class CurveTableWindow : ToolWindow
{
    public CurveTableWindow(CurveTableOptions options, string sourceText)
        : base("CTYTCBANG", "Bảng yếu tố cong", 520, 210, 420, 190)
    {
        DataContext = options ?? throw new ArgumentNullException(nameof(options));

        var top = BuildHeader("Tuyến: " + (sourceText ?? "chưa chọn"));

        var content = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        content.Children.Add(new TextBlock { Text = "Xuất ra:", Margin = new Thickness(0, 0, 0, 4) });
        content.Children.Add(OutputOptions(
            ("Bảng", nameof(CurveTableOptions.WriteTable)),
            ("CSV", nameof(CurveTableOptions.WriteCsv)),
            ("Excel", nameof(CurveTableOptions.WriteXlsx))));
        content.Children.Add(new TextBlock
        {
            Text = "Bảng: chọn điểm chèn sau khi bấm Xem trước / Áp dụng. CSV, Excel: ghi cạnh bản vẽ (<tên bản vẽ>_YEUTOCONG).",
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.DimGray,
            Margin = new Thickness(0, 6, 0, 0),
        });

        SetLayout(top, BuildFooter(nameof(CurveTableOptions.SummaryText), nameof(CurveTableOptions.CanApply)), content);
    }
}
