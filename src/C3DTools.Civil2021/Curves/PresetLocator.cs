using System;
using System.Collections.Generic;
using System.IO;
using C3DTools.Core.Presets;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace C3DTools.Civil2021.Curves;

/// <summary>Minimal preset lookup until the preset service (Plan 2) exists. Never throws.</summary>
internal static class PresetLocator
{
    private const string BundledPreset = "tcvn4054.preset.json";

    /// <param name="messages">Receives a line for each preset that could not be read.</param>
    public static ProjectPreset LoadForDrawing(List<string> messages)
    {
        foreach (var path in Candidates())
        {
            try
            {
                return PresetSerializer.Load(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                messages?.Add($"Không đọc được preset {path}: {ex.Message}");
            }
        }

        return new ProjectPreset();
    }

    private static IEnumerable<string> Candidates()
    {
        var drawingFolder = DrawingFolder();
        if (drawingFolder != null)
        {
            string[] found;
            try { found = Directory.GetFiles(drawingFolder, "*.c3dtools.json"); }
            catch (Exception) { found = new string[0]; }
            if (found.Length == 1) yield return found[0];
        }

        string bundled = null;
        try
        {
            var dllDir = Path.GetDirectoryName(typeof(PresetLocator).Assembly.Location);
            bundled = Path.Combine(dllDir ?? "", "Resources", BundledPreset);
        }
        catch (Exception)
        {
            // Location can be empty for an assembly loaded from bytes.
        }

        if (bundled != null && File.Exists(bundled)) yield return bundled;
    }

    /// <summary>The drawing's folder, or null when the drawing has never been saved.</summary>
    internal static string DrawingFolder()
    {
        try
        {
            if (Convert.ToInt32(AcCoreApp.GetSystemVariable("DWGTITLED")) == 0) return null;
            var prefix = Convert.ToString(AcCoreApp.GetSystemVariable("DWGPREFIX"));
            return string.IsNullOrEmpty(prefix) || !Directory.Exists(prefix) ? null : prefix;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
