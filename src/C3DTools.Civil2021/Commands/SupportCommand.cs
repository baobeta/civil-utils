using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Ui;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.SupportCommand))]

namespace C3DTools.Civil2021.Commands;

public class SupportCommand
{
    /// <summary>
    /// CTBAOLOI: packs the logs, the remembered options and facts about the machine and the drawing into a zip on the
    /// Desktop. The drawing itself is not included and nothing is sent anywhere.
    /// </summary>
    [CommandMethod("C3DTOOLS", "CTBAOLOI", CommandFlags.Modal)]
    public void Report()
    {
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc?.Editor;
        try
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) folder = Path.GetTempPath();
            var zip = Path.Combine(folder, SupportBundle.FileName(DateTime.Now));
            var written = SupportBundle.Write(zip, Facts(doc),
                new[] { ToolWindow.TraceLogPath, ToolWindow.ErrorLogPath, ToolWindow.OptionsPath });

            Prompts.Say(ed, $"Đã tạo {zip} ({written.Count} tệp).");
            Prompts.Say(ed, "Tệp này không chứa bản vẽ và chưa được gửi đi đâu. Hãy gửi nó kèm mô tả bước đang làm khi gặp lỗi.\n");
            try
            {
                Process.Start("explorer.exe", "/select,\"" + zip + "\"");
            }
            catch (System.Exception)
            {
                // Showing the file is a convenience; its path is on the command line.
            }
        }
        catch (System.Exception ex)
        {
            ToolWindow.LogError("CTBAOLOI", ex);
            Prompts.Say(ed, $"Không tạo được tệp báo lỗi: {ex.Message}");
        }
    }

    /// <summary>One fact per line; a fact that cannot be read says so instead of stopping the report.</summary>
    private static List<string> Facts(Autodesk.AutoCAD.ApplicationServices.Document doc)
    {
        var lines = new List<string>();
        void Add(string name, Func<object> value)
        {
            try
            {
                lines.Add(name + ": " + Convert.ToString(value(), CultureInfo.InvariantCulture));
            }
            catch (System.Exception ex)
            {
                lines.Add(name + ": không đọc được (" + ex.Message + ")");
            }
        }

        Add("Thời điểm", () => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        Add("C3DTools", () => typeof(SupportCommand).Assembly.GetName().Version);
        Add("AutoCAD", () => AcCoreApp.Version);
        Add("AutoCAD _VERNUM", () => AcCoreApp.GetSystemVariable("_VERNUM"));
        Add("Civil 3D (AeccDbMgd)", () => typeof(Alignment).Assembly.GetName().Version);
        Add("Windows", () => Environment.OSVersion.VersionString);
        Add("64-bit", () => Environment.Is64BitProcess);
        Add("Ngôn ngữ", () => CultureInfo.CurrentCulture.Name + ", dấu thập phân '" + CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator + "'");
        Add("Màn hình", () => System.Windows.SystemParameters.PrimaryScreenWidth.ToString("0", CultureInfo.InvariantCulture)
                              + " x " + System.Windows.SystemParameters.PrimaryScreenHeight.ToString("0", CultureInfo.InvariantCulture) + " (đơn vị WPF)");
        if (doc == null) return lines;

        Add("Bản vẽ", () => AcCoreApp.GetSystemVariable("DWGNAME"));
        Add("Bản vẽ đã lưu", () => Convert.ToInt32(AcCoreApp.GetSystemVariable("DWGTITLED"), CultureInfo.InvariantCulture) == 1);
        Add("INSUNITS", () => AcCoreApp.GetSystemVariable("INSUNITS"));
        using (var tr = doc.TransactionManager.StartTransaction())
        {
            var civil = CivilDocument.GetCivilDocument(doc.Database);
            Add("Số alignment", () => civil.GetAlignmentIds().Count);
            Add("Số mặt phủ", () => civil.GetSurfaceIds().Count);
            Add("Số assembly", () => civil.AssemblyCollection.Count);
            Add("Số nhóm cọc", () =>
            {
                var groups = 0;
                foreach (ObjectId id in civil.GetAlignmentIds())
                    groups += ((Alignment)tr.GetObject(id, OpenMode.ForRead)).GetSampleLineGroupIds().Count;
                return groups;
            });
            Add("Tuyến hiện hành", () => Services.ActiveRouteStore.Read(tr, doc.Database) ?? "(chưa có)");
            tr.Commit();
        }

        return lines;
    }
}
