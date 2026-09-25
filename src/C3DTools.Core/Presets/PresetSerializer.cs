using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace C3DTools.Core.Presets;

public sealed class PresetException : Exception
{
    public PresetException(string message, Exception inner = null) : base(message, inner) { }
}

public static class PresetSerializer
{
    public static string Save(ProjectPreset preset)
    {
        if (preset == null) throw new ArgumentNullException(nameof(preset));
        return JsonConvert.SerializeObject(preset, Formatting.Indented);
    }

    public static ProjectPreset Load(string json)
    {
        JObject obj;
        try
        {
            obj = JObject.Parse(json ?? "");
        }
        catch (JsonReaderException ex)
        {
            throw new PresetException("File preset không phải JSON hợp lệ: " + ex.Message, ex);
        }

        var version = obj.Value<int?>("SchemaVersion");
        if (version == null)
            throw new PresetException("File preset thiếu SchemaVersion.");
        if (version > ProjectPreset.CurrentSchemaVersion)
            throw new PresetException(
                $"Preset được tạo bởi phiên bản mới hơn (SchemaVersion {version}). Hãy cập nhật C3DTools.");

        var preset = obj.ToObject<ProjectPreset>();
        // Newtonsoft may replace the dictionary, losing the case-insensitive comparer.
        preset.Styles = new Dictionary<string, string>(
            preset.Styles ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        return preset;
    }

    /// <summary>
    /// The preset JSON with only the top-level key replaced by value (serialised like Save); every other key,
    /// including ones this version does not know, stays as it is. Throws PresetException when json is not a valid preset.
    /// </summary>
    public static string ReplaceSection(string json, string key, object value)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
        Load(json);   // same validation as reading it
        var obj = JObject.Parse(json);
        obj[key] = value == null ? JValue.CreateNull() : JToken.FromObject(value);
        return obj.ToString(Formatting.Indented);
    }
}
