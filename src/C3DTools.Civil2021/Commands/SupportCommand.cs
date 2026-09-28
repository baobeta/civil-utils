using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Services;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Ui;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Document = Autodesk.AutoCAD.ApplicationServices.Document;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.SupportCommand))]

namespace C3DTools.Civil2021.Commands;

public class SupportCommand
{
    private const string Contents =
        "Tệp zip này gồm: thong-tin.txt (phiên bản, máy, tên bản vẽ, số đối tượng), trace.log (các bước đã chạy, có tên tuyến, "
        + "tên nhóm cọc, tên cọc), error.log (lỗi đã gặp, có thể có đường dẫn thư mục trên máy), options.json (giá trị đã nhớ "
        + "của các hộp thoại, có tên mặt phủ và tên trắc dọc đã chọn). Nhật ký gồm mọi bản vẽ đã làm, không riêng bản vẽ này. Không có bản vẽ.";

    /// <summary>
    /// CTBAOLOI: packs the logs, the remembered options and facts about the machine and the drawing into a zip on the
    /// Desktop (the temp folder when the Desktop is missing or refuses the file). The drawing itself is not included and nothing is sent anywhere.
    /// </summary>
    [CommandMethod("C3DTOOLS", "CTBAOLOI", CommandFlags.Modal)]
    public void Report()
    {
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc?.Editor;
        try
        {
            ToolWindow.Trace("CTBAOLOI");
            var facts = new List<string> { Contents };
            try
            {
                facts.AddRange(Facts(doc));
            }
            catch (System.Exception ex)
            {
                // The logs are worth sending even when nothing about the machine can be read.
                facts.Add("Thông tin máy và bản vẽ: không đọc được (" + ex.GetType().Name + ": " + ex.Message + ")");
            }

            var files = new[] { ToolWindow.TraceLogPath, ToolWindow.ErrorLogPath, ToolWindow.OptionsPath };
            var name = SupportBundle.FileName(DateTime.Now);
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) folder = Path.GetTempPath();
            var zip = Path.Combine(folder, name);
            IReadOnlyList<string> written;
            try
            {
                written = SupportBundle.Write(zip, facts, files);
            }
            catch (System.Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                // A Desktop that exists but refuses the file (protected folders, locked-down profiles).
                zip = Path.Combine(Path.GetTempPath(), name);
                written = SupportBundle.Write(zip, facts, files);
            }

            Prompts.Say(ed, $"Đã tạo {zip} ({written.Count} tệp).");
            var missing = files.Where(f => !File.Exists(f)).Select(Path.GetFileName).ToList();
            if (missing.Count > 0) Prompts.Say(ed, "Chưa có trên máy này nên không kèm: " + string.Join(", ", missing) + ".");
            Prompts.Say(ed, "Tệp không chứa bản vẽ và C3DTools không tự gửi nó đi đâu. Tệp có tên bản vẽ, tên tuyến, tên nhóm cọc, tên cọc, tên mặt phủ, tên trắc dọc và nhật ký lỗi "
                            + "(có thể có đường dẫn thư mục trên máy); có thể mở ra xem trước khi gửi.");
            Prompts.Say(ed, "Hãy gửi tệp kèm mô tả bước đang làm khi gặp lỗi.\n");
            try
            {
                Process.Start("explorer.exe", "/select,\"" + zip + "\"")?.Dispose();
            }
            catch (System.Exception)
            {
                // Showing the file is a convenience; its path is on the command line.
            }
        }
        catch (System.Exception ex)
        {
            ToolWindow.LogError("CTBAOLOI", ex);
            Prompts.Say(ed, $"Không tạo được tệp báo lỗi: {ex.Message} Chi tiết: {ToolWindow.ErrorLogPath}");
        }
    }

    /// <summary>
    /// One fact per line; a fact that cannot be read says so instead of stopping the report.
    /// Names no Civil 3D type itself, so it still runs when the Civil 3D assemblies cannot be loaded.
    /// </summary>
    private static List<string> Facts(Document doc)
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

        Add("Thời điểm", () => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture));
        Add("C3DTools", () => typeof(SupportCommand).Assembly.GetName().Version);
        Add("AutoCAD", () => AcCoreApp.Version);
        Add("AutoCAD _VERNUM", () => AcCoreApp.GetSystemVariable("_VERNUM"));
        Add("Windows", () => Environment.OSVersion.VersionString);
        Add("64-bit", () => Environment.Is64BitProcess);
        Add("Ngôn ngữ", () => CultureInfo.CurrentCulture.Name + ", dấu thập phân '" + CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator + "'");
        Add("Màn hình", () => System.Windows.SystemParameters.PrimaryScreenWidth.ToString("0", CultureInfo.InvariantCulture)
                              + " x " + System.Windows.SystemParameters.PrimaryScreenHeight.ToString("0", CultureInfo.InvariantCulture) + " (đơn vị WPF)");
        if (doc != null)
        {
            Add("Bản vẽ", () => AcCoreApp.GetSystemVariable("DWGNAME"));
            Add("Bản vẽ đã lưu", () => Convert.ToInt32(AcCoreApp.GetSystemVariable("DWGTITLED"), CultureInfo.InvariantCulture) == 1);
            Add("INSUNITS", () => AcCoreApp.GetSystemVariable("INSUNITS"));
        }

        try
        {
            CivilFacts(doc, Add);
        }
        catch (System.Exception ex)
        {
            lines.Add("Civil 3D: không đọc được (" + ex.GetType().Name + ": " + ex.Message + ")");
        }

        return lines;
    }

    /// <summary>
    /// Everything that names a Civil 3D type. Not inlined: when the Civil 3D assemblies cannot be loaded, the failure
    /// happens on entering this method, where the caller catches it.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CivilFacts(Document doc, Action<string, Func<object>> add)
    {
        add("Civil 3D (AeccDbMgd)", () => typeof(Alignment).Assembly.GetName().Version);
        if (doc == null) return;

        var civil = CivilDocument.GetCivilDocument(doc.Database);
        using (var tr = doc.TransactionManager.StartTransaction())
        {
            add("Số alignment", () => civil.GetAlignmentIds().Count);
            add("Số mặt phủ", () => civil.GetSurfaceIds().Count);
            add("Số assembly", () => civil.AssemblyCollection.Count);
            add("Số nhóm cọc", () =>
            {
                var groups = 0;
                var unreadable = 0;
                foreach (ObjectId id in civil.GetAlignmentIds())
                {
                    try
                    {
                        if (id.IsErased) continue;
                        groups += ((Alignment)tr.GetObject(id, OpenMode.ForRead)).GetSampleLineGroupIds().Count;
                    }
                    catch (System.Exception)
                    {
                        unreadable++;
                    }
                }

                return unreadable == 0 ? groups.ToString(CultureInfo.InvariantCulture) : $"{groups} ({unreadable} tuyến không đọc được)";
            });
            add("Tuyến hiện hành ghi trong bản vẽ (handle)", () => ActiveRouteStore.Read(tr, doc.Database) ?? "(chưa có)");
            add("Tuyến hiện hành nhớ trong phiên (handle)", () => RoutePicker.Remembered(doc) ?? "(chưa có)");
            tr.Commit();
        }
    }
}
