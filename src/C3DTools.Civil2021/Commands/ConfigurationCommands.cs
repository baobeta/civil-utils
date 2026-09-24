using System;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using C3DTools.Core.Drainage;
using C3DTools.Civil2021.Services;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.ConfigurationCommands))]

namespace C3DTools.Civil2021.Commands;

public sealed class ConfigurationCommands
{
    [CommandMethod("C3DTOOLS", "CTCONFIG", CommandFlags.Modal)]
    public void ConfigureProject()
    {
        if (!HostServices.TryGet(out _, out var editor, out _, out _)) return;
        CommandRunner.Run(editor, () =>
        {
            var preset = PresetService.Load();
            preset.Name = HostServices.PromptText(editor, "Tên dự án", preset.Name);
            preset.StationDecimals = HostServices.PromptInt(editor, "Số lẻ lý trình", preset.StationDecimals, 0, 6);
            preset.ElevationDecimals = HostServices.PromptInt(editor, "Số lẻ cao độ", preset.ElevationDecimals, 0, 6);
            preset.VolumeDecimals = HostServices.PromptInt(editor, "Số lẻ khối lượng", preset.VolumeDecimals, 0, 6);

            var keywordOptions = new PromptKeywordOptions("\nQuy tắc cống [Set/Keep/Remove]: ")
            {
                AllowNone = false
            };
            HostServices.AddKeyword(keywordOptions, "Set", "Set");
            HostServices.AddKeyword(keywordOptions, "Keep", "Keep");
            HostServices.AddKeyword(keywordOptions, "Remove", "Remove");
            keywordOptions.Keywords.Default = preset.PipeRules == null ? "Set" : "Keep";
            var choice = editor.GetKeywords(keywordOptions);
            if (choice.Status != PromptStatus.OK) throw new OperationCanceledException();
            if (choice.StringResult.Equals("Set", StringComparison.OrdinalIgnoreCase))
            {
                var slopePercent = HostServices.PromptDouble(editor, "Độ dốc tối thiểu (%)", 0.30, false, true);
                var minCover = HostServices.PromptDouble(editor, "Chiều sâu chôn tối thiểu (m)", 0.70, false, true);
                preset.PipeRules = new PipeRules(slopePercent / 100.0, minCover);
            }
            else if (choice.StringResult.Equals("Remove", StringComparison.OrdinalIgnoreCase))
            {
                preset.PipeRules = null;
            }

            var path = PresetService.Save(preset);
            editor.WriteMessage($"\nĐã lưu preset: {path}");
            editor.WriteMessage(preset.PipeRules == null
                ? "\nCTCONG sẽ từ chối chạy cho đến khi cấu hình quy tắc cống."
                : "\nĐã cấu hình quy tắc cho CTCONG.");
        });
    }
}
