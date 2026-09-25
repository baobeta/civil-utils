using System;
using System.IO;
using C3DTools.Core.Presets;

namespace C3DTools.Civil2021.Services;

internal static class PresetService
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "C3DTools",
        "project-preset.json");

    public static ProjectPreset Load()
    {
        if (!File.Exists(DefaultPath)) return new ProjectPreset { Name = "Dự án mặc định" };
        return PresetSerializer.Load(File.ReadAllText(DefaultPath));
    }

    public static string Save(ProjectPreset preset)
    {
        var folder = Path.GetDirectoryName(DefaultPath);
        if (!string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(folder);
        File.WriteAllText(DefaultPath, PresetSerializer.Save(preset), new System.Text.UTF8Encoding(false));
        return DefaultPath;
    }
}
