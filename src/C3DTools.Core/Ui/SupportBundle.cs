using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace C3DTools.Core.Ui;

/// <summary>
/// "Gửi báo lỗi": one zip with the logs, the remembered options and a text file of facts about the machine and the
/// drawing. Only the files it is given go in; a drawing is never one of them.
/// </summary>
public static class SupportBundle
{
    public const string InfoName = "thong-tin.txt";

    /// <summary>
    /// Writes the zip. A file that is missing or cannot be read is left out and named in thong-tin.txt; only a zip
    /// that cannot be written throws. Returns the entry names, thong-tin.txt last.
    /// </summary>
    public static IReadOnlyList<string> Write(string zipPath, IEnumerable<string> info, IEnumerable<string> files)
    {
        if (string.IsNullOrWhiteSpace(zipPath)) throw new ArgumentException("Thiếu đường dẫn tệp zip.", nameof(zipPath));
        var lines = new List<string>(info ?? Enumerable.Empty<string>());
        var written = new List<string>();
        using (var stream = File.Create(zipPath))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            foreach (var path in (files ?? Enumerable.Empty<string>()).Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                var fileName = Path.GetFileName(path);
                if (!File.Exists(path))
                {
                    lines.Add(fileName + ": không có");
                    continue;
                }

                try
                {
                    // trace.log may be open for append by the running add-in.
                    byte[] bytes;
                    using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var buffer = new MemoryStream())
                    {
                        source.CopyTo(buffer);
                        bytes = buffer.ToArray();
                    }

                    var name = Unique(fileName, written);
                    using (var target = zip.CreateEntry(name).Open()) target.Write(bytes, 0, bytes.Length);
                    written.Add(name);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    lines.Add(fileName + ": không đọc được (" + ex.Message + ")");
                }
            }

            using (var writer = new StreamWriter(zip.CreateEntry(InfoName).Open(), new UTF8Encoding(true)))
                foreach (var line in lines) writer.WriteLine(line);
            written.Add(InfoName);
        }

        return written;
    }

    /// <summary>"C3DTools-baoloi-20260928-153000.zip".</summary>
    public static string FileName(DateTime now) =>
        "C3DTools-baoloi-" + now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".zip";

    private static string Unique(string name, List<string> used)
    {
        if (!used.Contains(name, StringComparer.OrdinalIgnoreCase)) return name;
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        for (var k = 2; ; k++)
        {
            var candidate = stem + "-" + k.ToString(System.Globalization.CultureInfo.InvariantCulture) + ext;
            if (!used.Contains(candidate, StringComparer.OrdinalIgnoreCase)) return candidate;
        }
    }
}
