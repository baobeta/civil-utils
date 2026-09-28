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
internal enum DialogAction { Cancel, Pick, ZoomToPi, ReadWidening, Preview, Apply, Count, PickPoints, PickFrom, PickTo, Reset }

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

    /// <summary>Option names that "Về mặc định" keeps: how the window looks, not what the command does.</summary>
    private const string WidthOption = "Width", HeightOption = "Height", AdvancedOption = "Advanced";

    private static DialogOptionsMemory _options;

    static ToolWindow()
    {
        NotifyGuard.LoopDetected = property =>
        {
            Trace("VÒNG LẶP BINDING tại " + property + " (đã ngắt)");
            LogError("Vòng lặp binding tại " + property, new InvalidOperationException(Environment.StackTrace));
        };
    }

    protected ToolWindow(string command, string title, double width, double height, double minWidth, double minHeight)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        Title = title;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 12;
        MinWidth = minWidth;
        MinHeight = minHeight;
        Width = RememberedSize(WidthOption, width, minWidth, SystemParameters.WorkArea.Width);
        Height = RememberedSize(HeightOption, height, minHeight, SystemParameters.WorkArea.Height);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Closed += (s, e) =>
        {
            RememberSize();
            // A command reopens its dialog on the same view model after every pick or preview. A closed dialog must stop
            // listening, or its controls keep receiving every change and writing values back.
            DataContext = null;
        };
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

    public static string TraceLogPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "C3DTools", "trace.log");

    /// <summary>
    /// Appends one step to %APPDATA%\C3DTools\trace.log at once, so after a crash inside Civil 3D (which no catch
    /// sees) the last line tells where it happened. The file restarts when it passes 1 MB. Never throws.
    /// </summary>
    public static void Trace(string step)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TraceLogPath));
            var file = new FileInfo(TraceLogPath);
            if (file.Exists && file.Length > 1024 * 1024) file.Delete();
            File.AppendAllText(TraceLogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + step + Environment.NewLine);
        }
        catch (Exception)
        {
            // Tracing is a convenience.
        }
    }

    public static string ErrorLogPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "C3DTools", "error.log");

    /// <summary>Appends the exception to %APPDATA%\C3DTools\error.log for bug reports. Never throws.</summary>
    public static void LogError(string where, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ErrorLogPath));
            File.AppendAllText(ErrorLogPath,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " " + where + Environment.NewLine + ex + Environment.NewLine + Environment.NewLine);
        }
        catch (Exception)
        {
            // Logging is a convenience.
        }
    }

    /// <summary>
    /// "Về mặc định": clears all remembered option values for <paramref name="command"/> except window size and the
    /// Nâng cao open/closed state, which reflect how the window looks, not what the command does.
    /// Call this INSTEAD of saving the dialog's values: the command loop must not save after ShowModal() returns
    /// DialogAction.Reset, or the values will be written back and the clear will have no effect.
    /// </summary>
    public static void ResetOptions(string command)
    {
        Options.Clear(command, WidthOption, HeightOption, AdvancedOption);
        SaveOptions();
        Trace(command + ": về mặc định");
    }

    /// <summary>Shows the dialog modal to AutoCAD and returns what the user chose.</summary>
    public DialogAction ShowModal()
    {
        // An exception in a handler or a binding would otherwise leave the dialog through AutoCAD's message loop.
        System.Windows.Threading.DispatcherUnhandledExceptionEventHandler guard = (s, e) =>
        {
            e.Handled = true;
            LogError(Command + " (hộp thoại)", e.Exception);
            Trace(Command + ": lỗi trong hộp thoại: " + e.Exception.Message);
            AcCoreApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                $"\nLỗi C3DTools trong hộp thoại: {e.Exception.Message} Chi tiết: {ErrorLogPath}");
            Action = DialogAction.Cancel;
            try
            {
                Close();
            }
            catch (InvalidOperationException)
            {
                // Already closing.
            }
        };
        Dispatcher.UnhandledException += guard;
        Trace(Command + ": mở hộp thoại");
        try
        {
            AcCoreApp.ShowModalWindow(this);
        }
        finally
        {
            Dispatcher.UnhandledException -= guard;
        }

        Trace(Command + ": hộp thoại đóng, chọn " + Action);
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

    /// <summary>
    /// top at the top, content fills the star row, bottom is always visible at the foot.
    /// A two-row Grid is used: row 1 (Auto) is measured before row 0 (star), so the footer always keeps its height
    /// even when Nâng cao opens and the content grows. Tree order is top → content → bottom, preserving the natural
    /// Tab order the dialogs had before this task.
    /// </summary>
    protected void SetLayout(UIElement top, UIElement bottom, UIElement content)
    {
        var root = new Grid { Margin = new Thickness(10) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var inner = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        inner.Children.Add(top);
        inner.Children.Add(content);
        Grid.SetRow(inner, 0);
        root.Children.Add(inner);

        Grid.SetRow(bottom, 1);
        root.Children.Add(bottom);

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

    /// <summary>
    /// "Nâng cao": the rows a user rarely changes, folded away until asked for. Whether it is open is remembered per command.
    /// A dialog has at most one Nâng cao section; its open/closed state is stored under one key per command.
    /// </summary>
    protected Expander Advanced(params UIElement[] rows)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        foreach (var row in rows) panel.Children.Add(row);
        var scroll = new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 240,
            Focusable = false,
        };
        var expander = new Expander
        {
            Header = "Nâng cao",
            Content = scroll,
            Margin = new Thickness(0, 6, 0, 2),
            IsExpanded = Options.Get(Command, AdvancedOption, false),
        };
        RoutedEventHandler remember = (s, e) =>
        {
            Options.Set(Command, AdvancedOption, expander.IsExpanded);
            SaveOptions();
        };
        expander.Expanded += remember;
        expander.Collapsed += remember;
        return expander;
    }

    /// <summary>
    /// Grid footer: "Về mặc định" (column 0, left, 16 px gap from the summary) when withReset; summary fills (column 1);
    /// Xem trước, Áp dụng, Hủy in columns 2–4. Children are added left to right so the natural Tab order is
    /// Về mặc định → Xem trước → Áp dụng → Hủy. The reset button is kept far from the primary buttons on purpose:
    /// one click discards all of the user's values.
    /// Only use withReset for commands whose loop handles DialogAction.Reset (see ResetOptions).
    /// </summary>
    protected FrameworkElement BuildFooter(string summaryPath, string canApplyPath, bool withReset = false)
    {
        var grid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });           // col 0: reset (or empty)
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // col 1: summary
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });           // col 2: Xem trước
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });           // col 3: Áp dụng
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });           // col 4: Hủy

        if (withReset)
        {
            var reset = ActionButton("Về mặc định", DialogAction.Reset);
            reset.ToolTip = "Đặt lại mọi ô của hộp thoại này về giá trị ban đầu (giữ nguyên tuyến đang chọn)";
            reset.Margin = new Thickness(0, 0, 16, 0);
            Grid.SetColumn(reset, 0);
            grid.Children.Add(reset);
        }

        var summary = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 8, 0),
        };
        summary.SetBinding(TextBlock.TextProperty, new Binding(summaryPath));
        summary.SetBinding(ToolTipProperty, new Binding(summaryPath) { Converter = NullIfEmpty.Instance });
        Grid.SetColumn(summary, 1);
        grid.Children.Add(summary);

        var preview = ActionButton("Xem trước", DialogAction.Preview);
        preview.SetBinding(IsEnabledProperty, new Binding(canApplyPath));
        Grid.SetColumn(preview, 2);
        grid.Children.Add(preview);

        var apply = ActionButton("Áp dụng", DialogAction.Apply);
        apply.SetBinding(IsEnabledProperty, new Binding(canApplyPath));
        Grid.SetColumn(apply, 3);
        grid.Children.Add(apply);

        var cancel = Button("Hủy", (s, e) => Finish(DialogAction.Cancel));
        cancel.IsCancel = true;
        Grid.SetColumn(cancel, 4);
        grid.Children.Add(cancel);

        return grid;
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
        Options.Set(Command, WidthOption, Math.Round(size.Width));
        Options.Set(Command, HeightOption, Math.Round(size.Height));
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

    /// <summary>Returns null for null or empty strings so a bound ToolTip shows no box when the text is empty.</summary>
    protected sealed class NullIfEmpty : IValueConverter
    {
        public static readonly NullIfEmpty Instance = new NullIfEmpty();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is string s && s.Length > 0 ? s : null;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
