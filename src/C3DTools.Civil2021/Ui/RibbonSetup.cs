using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: ExtensionApplication(typeof(C3DTools.Civil2021.Ui.RibbonSetup))]

namespace C3DTools.Civil2021.Ui;

/// <summary>Tab C3DTools, panel Tuyến: Yếu tố cong (CTYTC), Mẫu TCVN (CTYTCMAU), Bảng cong (CTYTCBANG).</summary>
public sealed class RibbonSetup : IExtensionApplication
{
    private const string TabId = "C3DTOOLS_TAB";

    public void Initialize()
    {
        try
        {
            if (ComponentManager.Ribbon != null) AddTab();
            else ComponentManager.ItemInitialized += OnItemInitialized;
        }
        catch (System.Exception ex)
        {
            Say($"Không tạo được tab ribbon C3DTools: {ex.Message}");
        }
    }

    public void Terminate()
    {
    }

    private static void OnItemInitialized(object sender, RibbonItemEventArgs e)
    {
        if (ComponentManager.Ribbon == null) return;
        ComponentManager.ItemInitialized -= OnItemInitialized;
        try
        {
            AddTab();
        }
        catch (System.Exception ex)
        {
            Say($"Không tạo được tab ribbon C3DTools: {ex.Message}");
        }
    }

    private static void AddTab()
    {
        var ribbon = ComponentManager.Ribbon;
        if (ribbon.Tabs.Any(t => t.Id == TabId)) return;

        var panelSource = new RibbonPanelSource { Title = "Tuyến" };
        panelSource.Items.Add(Button("Yếu tố cong", "\u0003\u0003_CTYTC ", large: true));
        var row = new RibbonRowPanel();
        row.Items.Add(Button("Mẫu TCVN", "\u0003\u0003_CTYTCMAU ", large: false));
        row.Items.Add(new RibbonRowBreak());
        row.Items.Add(Button("Bảng cong", "\u0003\u0003_CTYTCBANG ", large: false));
        panelSource.Items.Add(row);

        var tab = new RibbonTab { Id = TabId, Title = "C3DTools" };
        tab.Panels.Add(new RibbonPanel { Source = panelSource });
        ribbon.Tabs.Add(tab);
    }

    private static RibbonButton Button(string text, string command, bool large)
    {
        var button = new RibbonButton
        {
            Text = text,
            ShowText = true,
            ShowImage = true,
            Size = large ? RibbonItemSize.Large : RibbonItemSize.Standard,
            Orientation = large ? Orientation.Vertical : Orientation.Horizontal,
            CommandParameter = command,
            CommandHandler = new SendCommand(),
        };
        var small = Icon("ytc16.png");
        if (small != null) button.Image = small;
        if (large)
        {
            var big = Icon("ytc32.png");
            if (big != null) button.LargeImage = big;
        }

        return button;
    }

    /// <summary>Embedded PNG, or null (the button then shows only its text).</summary>
    private static BitmapImage Icon(string name)
    {
        try
        {
            using (var stream = typeof(RibbonSetup).Assembly.GetManifestResourceStream("C3DTools.Civil2021.Resources." + name))
            {
                if (stream == null) return null;
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;   // read now: the stream is closed after EndInit
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    private static void Say(string message) =>
        AcCoreApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n" + message);

    /// <summary>Runs the button's CommandParameter ("^C^C_CTYTC ": cancel any running command first) in the active drawing.</summary>
    private sealed class SendCommand : ICommand
    {
        public event EventHandler CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            if (parameter is string command)
                AcCoreApp.DocumentManager.MdiActiveDocument?.SendStringToExecute(command, true, false, true);
        }
    }
}
