using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;

namespace C3DTools.Core.Ui;

/// <summary>
/// Last-used dialog options (window size, output checkboxes), keyed "&lt;command&gt;.&lt;option&gt;", e.g. "CTYTC.Width".
/// Values are stored as InvariantCulture text. Losing it only loses convenience, so loading never throws.
/// </summary>
public sealed class DialogOptionsMemory
{
    private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public int Count => _values.Count;

    public static string Key(string command, string option) => command + "." + option;

    /// <summary>Empty memory for null, empty or invalid JSON.</summary>
    public static DialogOptionsMemory Load(string json)
    {
        var memory = new DialogOptionsMemory();
        if (string.IsNullOrWhiteSpace(json)) return memory;
        try
        {
            var values = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            if (values != null)
                foreach (var pair in values)
                    if (pair.Key != null && pair.Value != null) memory._values[pair.Key] = pair.Value;
        }
        catch (JsonException)
        {
            // A damaged options file is ignored and rewritten on the next save.
        }

        return memory;
    }

    /// <summary>Empty memory when the file is missing or unreadable.</summary>
    public static DialogOptionsMemory LoadFile(string path)
    {
        try
        {
            return File.Exists(path) ? Load(File.ReadAllText(path)) : new DialogOptionsMemory();
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
        {
            return new DialogOptionsMemory();
        }
    }

    public string Save() => JsonConvert.SerializeObject(_values, Formatting.Indented);

    /// <summary>Creates the folder if needed. Throws on I/O errors; callers decide whether that matters.</summary>
    public void SaveFile(string path)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        File.WriteAllText(path, Save());
    }

    /// <summary>The stored value, or fallback when missing or not a valid T. T is bool, double, int or string.</summary>
    public T Get<T>(string command, string option, T fallback)
    {
        if (typeof(T) != typeof(bool) && typeof(T) != typeof(double) && typeof(T) != typeof(int) && typeof(T) != typeof(string))
            throw new NotSupportedException($"DialogOptionsMemory: kiểu {typeof(T).Name} không được hỗ trợ.");
        if (!_values.TryGetValue(Key(command, option), out var text)) return fallback;
        return TryParse<T>(text, out var value) ? value : fallback;
    }

    public void Set<T>(string command, string option, T value) => _values[Key(command, option)] = Format(value);

    private static string Format<T>(T value)
    {
        object o = value;
        switch (o)
        {
            case bool b: return b ? "true" : "false";
            case double d: return d.ToString("R", CultureInfo.InvariantCulture);
            case int i: return i.ToString(CultureInfo.InvariantCulture);
            case string s: return s;
            case null when typeof(T) == typeof(string): return "";
            default: throw new NotSupportedException($"DialogOptionsMemory: kiểu {typeof(T).Name} không được hỗ trợ.");
        }
    }

    private static bool TryParse<T>(string text, out T value)
    {
        value = default;
        object parsed;
        if (typeof(T) == typeof(string)) parsed = text;
        else if (typeof(T) == typeof(bool))
        {
            if (!bool.TryParse(text, out var b)) return false;
            parsed = b;
        }
        else if (typeof(T) == typeof(double))
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || double.IsNaN(d) || double.IsInfinity(d)) return false;
            parsed = d;
        }
        else if (typeof(T) == typeof(int))
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return false;
            parsed = i;
        }
        else return false;

        value = (T)parsed;
        return true;
    }
}
