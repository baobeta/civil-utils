using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using C3DTools.Core.Curves;
using C3DTools.Core.Tables;
using C3DTools.Core.Ui;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace C3DTools.Civil2021.Ui;

/// <summary>What the dialog asks the command to do after it closes.</summary>
internal enum DialogAction { Cancel, Pick, ZoomToPi, ReadWidening, Preview, Apply, Count }

/// <summary>
/// The skeleton every C3DTools dialog shares (UI rule 2), built in code (no XAML): header with the source and
/// "Chọn trên bản vẽ…", the tool's content, footer with the summary and Xem trước / Áp dụng / Hủy (Esc = Hủy).
/// Segoe UI; the last size is remembered per command in %APPDATA%\C3DTools\options.json.
/// </summary>
internal abstract class ToolWindow : Window
{
    public static readonly Brush WarningBrush = Frozen(Color.FromRgb(0xFF, 0xF4, 0xC2));
    public static readonly Brush ErrorBrush = Frozen(Color.FromRgb(0xFF, 0xD6, 0xD6));
    public static readonly Brush ReadOnlyBrush = Frozen(Color.FromRgb(0xEE, 0xEE, 0xEE));

    private static DialogOptionsMemory _options;

    protected ToolWindow(string command, string title, double width, double height, double minWidth, double minHeight)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        Title = title;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 12;
        MinWidth = minWidth;
        MinHeight = minHeight;
        Width = RememberedSize("Width", width, minWidth, SystemParameters.WorkArea.Width);
        Height = RememberedSize("Height", height, minHeight, SystemParameters.WorkArea.Height);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Closed += (s, e) => RememberSize();
    }

    /// <summary>The command name, e.g. "CTYTC"; the prefix of this dialog's remembered options.</summary>
    public string Command { get; }

    public DialogAction Action { get; private set; } = DialogAction.Cancel;

    /// <summary>Last-used options of every dialog, loaded once per session.</summary>
    public static DialogOptionsMemory Options => _options ??= DialogOptionsMemory.LoadFile(OptionsPath);

    public static string OptionsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "C3DTools", "options.json");

    /// <summary>Never throws: remembered options are a convenience.</summary>
    public static void SaveOptions()
    {
        try
        {
            Options.SaveFile(OptionsPath);
        }
        catch (Exception)
        {
            // Read-only profile or full disk: the dialog just opens at its default size next time.
        }
    }

    /// <summary>Shows the dialog modal to AutoCAD and returns what the user chose.</summary>
    public DialogAction ShowModal()
    {
        AcCoreApp.ShowModalWindow(this);
        return Action;
    }

    /// <summary>Called before the dialog closes with any action, e.g. to commit a grid edit in progress.</summary>
    protected virtual void CommitEdits()
    {
    }

    protected void Finish(DialogAction action)
    {
        CommitEdits();
        Action = action;
        Close();
    }

    /// <summary>top and bottom dock to the edges, content fills the rest.</summary>
    protected void SetLayout(UIElement top, UIElement bottom, UIElement content)
    {
        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(content);
        Content = root;
    }

    /// <summary>The source line: its description and "Chọn trên bản vẽ…" (default: close with DialogAction.Pick). Add more controls to the row.</summary>
    protected StackPanel BuildHeader(string sourceText, System.Action onPick = null)
    {
        var row = Row();
        row.Children.Add(new TextBlock { Text = sourceText, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        row.Children.Add(onPick == null
            ? ActionButton("Chọn trên bản vẽ…", DialogAction.Pick)
            : Button("Chọn trên bản vẽ…", (s, e) => onPick()));
        return row;
    }

    /// <summary>Summary text on the left; Xem trước, Áp dụng (both enabled by canApplyPath) and Hủy on the right.</summary>
    protected DockPanel BuildFooter(string summaryPath, string canApplyPath)
    {
        var actions = new DockPanel { Margin = new Thickness(0, 6, 0, 0), LastChildFill = false };
        var summary = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        summary.SetBinding(TextBlock.TextProperty, new Binding(summaryPath));
        actions.Children.Add(summary);
        var cancel = Button("Hủy", (s, e) => Finish(DialogAction.Cancel));
        cancel.IsCancel = true;
        var apply = ActionButton("Áp dụng", DialogAction.Apply);
        apply.SetBinding(IsEnabledProperty, new Binding(canApplyPath));
        var preview = ActionButton("Xem trước", DialogAction.Preview);
        preview.SetBinding(IsEnabledProperty, new Binding(canApplyPath));
        foreach (var b in new[] { cancel, apply, preview })
        {
            DockPanel.SetDock(b, Dock.Right);
            actions.Children.Add(b);
        }

        return actions;
    }

    /// <summary>One row of output checkboxes, each bound two-way to a bool view-model property.</summary>
    protected static StackPanel OutputOptions(params (string label, string bindingPath)[] options)
    {
        var row = Row();
        foreach (var (label, path) in options) row.Children.Add(Check(label, path));
        return row;
    }

    protected Button ActionButton(string text, DialogAction action) => Button(text, (s, e) => Finish(action));

    protected static Button Button(string text, RoutedEventHandler click)
    {
        var b = new Button { Content = text, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 8, 0), MinWidth = 80 };
        b.Click += click;
        return b;
    }

    protected static StackPanel Row() => new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

    protected static TextBlock Label(string text) =>
        new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 4, 0) };

    protected static CheckBox Check(string text, string path)
    {
        var c = new CheckBox { Content = text, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
        c.SetBinding(ToggleButton.IsCheckedProperty, new Binding(path) { Mode = BindingMode.TwoWay });
        return c;
    }

    protected static Binding NumberBinding(string path, UpdateSourceTrigger trigger) =>
        new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = trigger, Converter = NumberText.Instance };

    /// <summary>ElementStyle for a numeric DataGridTextColumn: right-aligned.</summary>
    protected static Style NumberCellStyle()
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        return style;
    }

    /// <summary>Grey, read-only-looking cells.</summary>
    protected static Style GreyCell()
    {
        var style = new Style(typeof(DataGridCell));
        style.Setters.Add(new Setter(BackgroundProperty, ReadOnlyBrush));
        style.Setters.Add(new Setter(ForegroundProperty, Brushes.DimGray));
        return style;
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private double RememberedSize(string option, double fallback, double min, double max)
    {
        var size = Options.Get(Command, option, fallback);
        if (size < min) size = min;
        return max > min && size > max ? max : size;
    }

    private void RememberSize()
    {
        var size = WindowState == WindowState.Normal ? new Size(ActualWidth, ActualHeight) : RestoreBounds.Size;
        if (!(size.Width > 0) || !(size.Height > 0) || double.IsInfinity(size.Width) || double.IsInfinity(size.Height)) return;
        Options.Set(Command, "Width", Math.Round(size.Width));
        Options.Set(Command, "Height", Math.Round(size.Height));
        SaveOptions();
    }

    /// <summary>double ⇄ text with InvariantCulture; accepts "2,5" like the grid does.</summary>
    protected sealed class NumberText : IValueConverter
    {
        public static readonly NumberText Instance = new NumberText();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is double d ? NumberFormat.Trimmed(d, 3) : "";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            NumberInput.TryParse(value as string, out var d) ? d : Binding.DoNothing;
    }

    protected sealed class InverseBool : IValueConverter
    {
        public static readonly InverseBool Instance = new InverseBool();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !(value is bool b && b);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => !(value is bool b && b);
    }
}
