using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using AutoCADDbObject = Autodesk.AutoCAD.DatabaseServices.DBObject;

namespace C3DTools.Civil2021.Services;

internal static class HostServices
{
    public static bool TryGet(out Document document, out Editor editor, out Database database, out CivilDocument civil)
    {
        document = Application.DocumentManager.MdiActiveDocument;
        editor = document?.Editor;
        database = document?.Database;
        civil = database == null ? null : CivilApplication.ActiveDocument;
        return document != null && editor != null && database != null && civil != null;
    }

    public static ObjectId PromptEntityId(Editor editor, Type type, string message)
    {
        var options = new PromptEntityOptions("\n" + message + ": ")
        {
            AllowNone = false
        };
        options.AddAllowedClass(type, true);
        var result = editor.GetEntity(options);
        return result.Status == PromptStatus.OK ? result.ObjectId : ObjectId.Null;
    }

    public static T PromptEntity<T>(Transaction transaction, Editor editor, string message)
        where T : AutoCADDbObject
    {
        var id = PromptEntityId(editor, typeof(T), message);
        if (id.IsNull) return null;
        return transaction.GetObject(id, OpenMode.ForRead) as T;
    }

    public static string PromptText(Editor editor, string message, string defaultValue = null, bool allowSpaces = true)
    {
        var options = new PromptStringOptions("\n" + message +
            (defaultValue == null ? ": " : $" [<{defaultValue}>]: "))
        {
            AllowSpaces = allowSpaces
        };
        if (defaultValue != null) options.DefaultValue = defaultValue;
        var result = editor.GetString(options);
        if (result.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(result.StringResult))
            return result.StringResult.Trim();
        return defaultValue ?? throw new OperationCanceledException();
    }

    public static double PromptDouble(
        Editor editor,
        string message,
        double defaultValue,
        bool allowNegative = true,
        bool allowZero = true)
    {
        var options = new PromptDoubleOptions("\n" + message +
            $" [<{defaultValue.ToString("0.###", CultureInfo.InvariantCulture)}>]: ")
        {
            AllowNegative = allowNegative,
            AllowZero = allowZero,
            DefaultValue = defaultValue
        };
        var result = editor.GetDouble(options);
        return result.Status == PromptStatus.OK ? result.Value : defaultValue;
    }

    public static int PromptInt(Editor editor, string message, int defaultValue, int minimum, int maximum)
    {
        var options = new PromptIntegerOptions("\n" + message + $" [<{defaultValue}>]: ")
        {
            AllowNegative = false,
            AllowZero = minimum == 0,
            DefaultValue = defaultValue,
            LowerLimit = minimum,
            UpperLimit = maximum
        };
        var result = editor.GetInteger(options);
        return result.Status == PromptStatus.OK ? result.Value : defaultValue;
    }

    public static bool PromptYesNo(Editor editor, string message, bool defaultValue)
    {
        var options = new PromptKeywordOptions("\n" + message + " [Yes/No]: ")
        {
            AllowNone = false
        };
        AddKeyword(options, "Yes", "Yes");
        AddKeyword(options, "No", "No");
        options.Keywords.Default = defaultValue ? "Yes" : "No";
        var result = editor.GetKeywords(options);
        if (result.Status != PromptStatus.OK) throw new OperationCanceledException();
        return result.StringResult.Equals("Yes", StringComparison.OrdinalIgnoreCase);
    }

    public static Point3d PromptPoint(Editor editor, string message, Point3d? basePoint = null)
    {
        var options = new PromptPointOptions("\n" + message + ": ")
        {
            UseBasePoint = basePoint.HasValue
        };
        if (basePoint.HasValue) options.BasePoint = basePoint.Value;
        var result = editor.GetPoint(options);
        return result.Status == PromptStatus.OK ? result.Value : throw new OperationCanceledException();
    }

    public static bool Confirm(Editor editor, string action, string cancel = "Cancel")
    {
        var options = new PromptKeywordOptions("\n" + action + " [Apply/Cancel]: ")
        {
            AllowNone = false
        };
        AddKeyword(options, "Apply", "Apply");
        AddKeyword(options, cancel, "Cancel");
        options.Keywords.Default = "Apply";
        return editor.GetKeywords(options).StringResult.Equals("Apply", StringComparison.OrdinalIgnoreCase);
    }

    public static ObjectId PromptNamedObject(Editor editor, Transaction transaction, string role, ObjectIdCollection ids)
    {
        var choices = ids.Cast<ObjectId>()
            .Where(id => !id.IsNull && id.IsValid && !id.IsErased)
            .Select(id => new NamedObject(id, ReadObjectName(transaction.GetObject(id, OpenMode.ForRead))))
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Id.Handle)
            .ToArray();
        if (choices.Length == 0)
            throw new InvalidOperationException($"Bản vẽ chưa có {role}.");

        var options = new PromptKeywordOptions($"\nChọn {role} [Enter dùng mục đầu]: ")
        {
            AllowNone = false
        };
        for (var i = 0; i < choices.Length; i++)
            AddKeyword(options, choices[i].Name, "Choice" + i.ToString(CultureInfo.InvariantCulture));
        options.Keywords.Default = choices[0].Name;
        var result = editor.GetKeywords(options);
        if (result.Status != PromptStatus.OK) throw new OperationCanceledException();
        var selected = result.StringResult.Trim().Trim('"');
        return choices.First(x => x.Name.Equals(selected, StringComparison.CurrentCultureIgnoreCase)).Id;
    }

    public static string ReadObjectName(AutoCADDbObject value)
    {
        if (value == null) return "";
        var property = value.GetType().GetProperty("Name");
        return property?.GetValue(value, null) as string ?? "";
    }

    public static ObjectId LayerId(Transaction transaction, Database database, string preferredName)
    {
        var layers = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        var preferredId = FindNamedRecord(layers, preferredName, transaction);
        if (!preferredId.IsNull) return preferredId;
        var zeroId = FindNamedRecord(layers, "0", transaction);
        if (!zeroId.IsNull) return zeroId;
        foreach (ObjectId id in layers) return id;
        throw new InvalidOperationException("Bản vẽ không có layer.");
    }

    public static ObjectId FirstId(IEnumerable<ObjectId> ids)
    {
        foreach (var id in ids)
            if (!id.IsNull && id.IsValid && !id.IsErased) return id;
        return ObjectId.Null;
    }

    public static ObjectId TextStyleId(Database database)
    {
        var current = database.Textstyle;
        if (!current.IsNull) return current;
        var standard = SymbolUtilityServices.GetTextStyleStandardId(database);
        if (!standard.IsNull) return standard;
        throw new InvalidOperationException("Bản vẽ không có text style.");
        throw new InvalidOperationException("Bản vẽ không có text style.");
    }

    public static string DrawingFolder(Database database)
    {
        if (!string.IsNullOrWhiteSpace(database.Filename))
        {
            var folder = Path.GetDirectoryName(database.Filename);
            if (!string.IsNullOrWhiteSpace(folder)) return folder;
        }
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    public static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = (value ?? "C3DTools").Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var result = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(result) ? "C3DTools" : result;
    }

    public static string Format(double value, int decimals = 3) =>
        value.ToString("0." + new string('#', decimals), CultureInfo.InvariantCulture);

    public static void AddKeyword(PromptKeywordOptions options, string display, string key)
    {
        options.Keywords.Add('"' + display + '"', key, display);
    }

    public static string PromptCsvPath(Editor editor, string defaultPath, bool optional)
    {
        var full = Path.GetFullPath(defaultPath);
        if (!string.Equals(Path.GetExtension(full), ".csv", StringComparison.OrdinalIgnoreCase))
            full += ".csv";
        var options = new PromptSaveFileOptions(optional
            ? "\nLưu CSV (Enter để bỏ qua): "
            : "\nLưu CSV: ")
        {
            Filter = "CSV (*.csv)|*.csv",
            InitialDirectory = Path.GetDirectoryName(full) ?? string.Empty,
            InitialFileName = Path.GetFileName(full)
        };
        var result = editor.GetFileNameForSave(options);
        if (result.Status != PromptStatus.OK) return optional ? null : throw new OperationCanceledException();
        var path = result.StringResult;
        return string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase) ? path : path + ".csv";
    }

    public static string UniqueName(string desired, Func<string, bool> exists)
    {
        desired = string.IsNullOrWhiteSpace(desired) ? "C3DTools" : desired.Trim();
        if (!exists(desired)) return desired;
        for (var i = 2; i < 10000; i++)
        {
            var candidate = $"{desired}-{i.ToString(CultureInfo.InvariantCulture)}";
            if (!exists(candidate)) return candidate;
        }
        return $"{desired}-{Guid.NewGuid():N}";
    }

    private static ObjectId FindNamedRecord(
        System.Collections.IEnumerable records,
        string name,
        Transaction transaction)
    {
        if (string.IsNullOrWhiteSpace(name)) return ObjectId.Null;
        foreach (ObjectId id in records)
        {
            var record = transaction.GetObject(id, OpenMode.ForRead);
            if (ReadObjectName(record).Equals(name, StringComparison.CurrentCultureIgnoreCase))
                return id;
        }
        return ObjectId.Null;
    }

    private sealed class NamedObject
    {
        public NamedObject(ObjectId id, string name)
        {
            Id = id;
            Name = name;
        }

        public ObjectId Id { get; }
        public string Name { get; }
    }
}
