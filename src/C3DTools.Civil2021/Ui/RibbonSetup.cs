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

/// <summary>
/// Tab C3DTools (UI rule 6): one panel per work area, the daily command as the large button, the rest small, each with a
/// one-sentence Vietnamese tooltip. Tuyến: Yếu tố cong (CTYTC), Mẫu TCVN (CTYTCMAU), Bảng cong (CTYTCBANG), Toạ độ cọc (CTTOADO).
/// Trắc dọc: Bảng trắc dọc (CTTRACDOC), Cong đứng (CTCONGDUNG). Trắc ngang: Bảng trắc ngang (CTTRACNGANG), Xếp trang (CTXEPTRANG).
/// Địa hình: Mặt địa hình (CTMATDIA), VN-2000 (CTVN2000). Thoát nước: Bảng cống (CTBANGCONG). Bản vẽ: Chuyển font (CTFONT), Chuẩn layer (CTLAYER).
/// </summary>
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

        var tab = new RibbonTab { Id = TabId, Title = "C3DTools" };
        tab.Panels.Add(Panel("Tuyến", "ytc",
            ("Yếu tố cong", "CTYTC", "Thiết kế và cắm cong nằm theo TCVN 4054 cho polyline hoặc alignment: đường cong, khung yếu tố, cọc và bảng."),
            ("Mẫu TCVN", "CTYTCMAU", "Nhập kiểu nhãn, label set và bộ viết tắt TCVN cho alignment vào bản vẽ."),
            ("Bảng cong", "CTYTCBANG", "Xuất bảng tổng hợp yếu tố cong của một tuyến ra AutoCAD Table, CSV hoặc Excel."),
            ("Toạ độ cọc", "CTTOADO", "Lập bảng toạ độ cọc của alignment ra AutoCAD Table, CSV, Excel hoặc điểm COGO.")));
        tab.Panels.Add(Panel("Trắc dọc", "tracdoc",
            ("Bảng trắc dọc", "CTTRACDOC", "Vẽ bảng số liệu trắc dọc kiểu Việt Nam dưới profile view và xuất CSV, Excel."),
            ("Cong đứng", "CTCONGDUNG", "Tính và ghi yếu tố cong đứng (A, R, T, E) của trắc dọc thiết kế, kiểm tra theo preset.")));
        tab.Panels.Add(Panel("Trắc ngang", "tracngang",
            ("Bảng trắc ngang", "CTTRACNGANG", "Vẽ bảng số liệu và diện tích đào đắp dưới mỗi trắc ngang, xuất CSV, Excel."),
            ("Xếp trang", "CTXEPTRANG", "Xếp các trắc ngang vào tờ in theo thứ tự lý trình và vẽ khung tờ.")));
        tab.Panels.Add(Panel("Địa hình", "diahinh",
            ("Mặt địa hình", "CTMATDIA", "Xoá tam giác dài hoặc ngoài ranh giới của mặt phủ TIN, hoặc ghi cao độ đường đồng mức."),
            ("VN-2000", "CTVN2000", "Chuyển toạ độ đối tượng hoặc điểm COGO sang kinh tuyến trục, múi chiếu VN-2000 khác.")));
        tab.Panels.Add(Panel("Thoát nước", "thoatnuoc",
            ("Bảng cống", "CTBANGCONG", "Lập bảng thống kê cống của các mạng cống cắt qua tuyến ra AutoCAD Table, CSV hoặc Excel.")));
        tab.Panels.Add(Panel("Bản vẽ", "banve",
            ("Chuyển font", "CTFONT", "Chuyển chữ tiếng Việt giữa TCVN3, VNI và Unicode cho đối tượng chọn hoặc toàn bản vẽ."),
            ("Chuẩn layer", "CTLAYER", "Chuyển đối tượng sang layer chuẩn theo preset và xoá layer rỗng nếu cần.")));
        ribbon.Tabs.Add(tab);
    }

    /// <summary>The first button large (the panel's daily command), the others small in one row; all with the panel's icon.</summary>
    private static RibbonPanel Panel(string title, string icon, params (string text, string command, string tip)[] buttons)
    {
        var small = Icon(icon + "16.png");
        var large = Icon(icon + "32.png");
        var source = new RibbonPanelSource { Title = title };
        source.Items.Add(Button(buttons[0], large: true, small, large));
        if (buttons.Length > 1)
        {
            var row = new RibbonRowPanel();
            for (var i = 1; i < buttons.Length; i++)
            {
                if (i > 1) row.Items.Add(new RibbonRowBreak());
                row.Items.Add(Button(buttons[i], large: false, small, large));
            }

            source.Items.Add(row);
        }

        return new RibbonPanel { Source = source };
    }

    private static RibbonButton Button((string text, string command, string tip) spec, bool large, BitmapImage small, BitmapImage big)
    {
        var button = new RibbonButton
        {
            Text = spec.text,
            ShowText = true,
            ShowImage = small != null,
            Size = large ? RibbonItemSize.Large : RibbonItemSize.Standard,
            Orientation = large ? Orientation.Vertical : Orientation.Horizontal,
            CommandParameter = "\u0003\u0003_" + spec.command + " ",
            CommandHandler = new SendCommand(),
            ToolTip = new RibbonToolTip { Title = spec.text, Content = spec.tip, Command = spec.command },
        };
        if (small != null) button.Image = small;
        if (large && big != null)
        {
            button.LargeImage = big;
            button.ShowImage = true;
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
