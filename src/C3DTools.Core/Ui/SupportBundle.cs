using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Maximum bytes read from a single source file. Files larger than this are packed as their last
    /// <see cref="MaxFileBytes"/> bytes so the Civil 3D process is never asked to hold more than 2 MB per file.
    /// </summary>
    public const long MaxFileBytes = 2 * 1024 * 1024;

    /// <summary>
    /// Writes the zip to a temporary file in the same directory, then atomically moves it to
    /// <paramref name="zipPath"/> (deleting any existing file there first). If anything fails the
    /// temporary file is deleted and <paramref name="zipPath"/> is left untouched.
    /// <para>
    /// A source file that is missing or cannot be read is skipped and named in thong-tin.txt; only
    /// failures writing the archive propagate. Files larger than <see cref="MaxFileBytes"/> are capped
    /// (last N bytes) to avoid exhausting Civil 3D process memory.
    /// </para>
    /// Returns the entry names in order, thong-tin.txt last.
    /// </summary>
    public static IReadOnlyList<string> Write(string zipPath, IEnumerable<string> info, IEnumerable<string> files)
    {
        if (string.IsNullOrWhiteSpace(zipPath))
            throw new ArgumentException("Thiếu đường dẫn tệp zip.", nameof(zipPath));

        var fullZip = Path.GetFullPath(zipPath);
        var tmp = fullZip + ".tmp";

        try
        {
            var lines = new List<string>(info ?? Enumerable.Empty<string>());
            var written = new List<string>();

            using (var stream = File.Create(tmp))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (var rawPath in (files ?? Enumerable.Empty<string>()).Where(p => !string.IsNullOrWhiteSpace(p)))
                {
                    // E: guard Path.GetFileName against illegal-character exceptions
                    string fileName;
                    try
                    {
                        fileName = Path.GetFileName(rawPath);
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
                    {
                        lines.Add(rawPath + ": không có");
                        continue;
                    }

                    // H: skip if the path is the zip output itself
                    string fullRaw;
                    try { fullRaw = Path.GetFullPath(rawPath); }
                    catch { fullRaw = null; }
                    if (fullRaw != null && string.Equals(fullRaw, fullZip, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!File.Exists(rawPath))
                    {
                        lines.Add(fileName + ": không có");
                        continue;
                    }

                    // D: only reading is inside the try/catch; writing to the archive is outside
                    byte[] bytes;
                    long totalLength;
                    bool wasCapped;
                    try
                    {
                        // trace.log may be open for append by the running add-in (FileShare.ReadWrite)
                        using var source = new FileStream(rawPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        totalLength = source.Length;
                        wasCapped = totalLength > MaxFileBytes;
                        if (wasCapped)
                            source.Seek(totalLength - MaxFileBytes, SeekOrigin.Begin);

                        using var buffer = new MemoryStream();
                        source.CopyTo(buffer);
                        bytes = buffer.ToArray();
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        // F: use exception type name, not message (message may contain the user's account path)
                        lines.Add(fileName + ": không đọc được (" + ex.GetType().Name + ")");
                        continue;
                    }

                    // A: reserve InfoName — rename packed file if it collides
                    var name = UniqueNotReserved(fileName, written);

                    // D: archive write is outside the catch — failure propagates (B cleans up)
                    var entry = zip.CreateEntry(name);
                    // G: set last-write time from the source file
                    entry.LastWriteTime = File.GetLastWriteTime(rawPath);
                    using (var target = entry.Open())
                        target.Write(bytes, 0, bytes.Length);
                    written.Add(name);

                    if (wasCapped)
                        lines.Add(string.Format(CultureInfo.InvariantCulture,
                            "{0}: chỉ lấy {1} byte cuối của {2} byte",
                            fileName, MaxFileBytes, totalLength));
                }

                // facts entry — always InfoName, always last
                var infoEntry = zip.CreateEntry(InfoName);
                using (var writer = new StreamWriter(infoEntry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
                    foreach (var line in lines)
                        writer.WriteLine(line);
                written.Add(InfoName);
            }

            // B: atomic move — delete existing zipPath first, then rename tmp
            if (File.Exists(fullZip))
                File.Delete(fullZip);
            File.Move(tmp, fullZip);

            return written;
        }
        catch
        {
            // B: clean up the temporary file on any failure
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort */ }
            throw;
        }
    }

    /// <summary>"C3DTools-baoloi-20260928-153000.zip".</summary>
    public static string FileName(DateTime now) =>
        "C3DTools-baoloi-" + now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".zip";

    /// <summary>
    /// Returns a unique name that is neither already in <paramref name="used"/> (case-insensitive)
    /// nor equal to <see cref="InfoName"/> (case-insensitive).
    /// </summary>
    private static string UniqueNotReserved(string name, List<string> used)
    {
        // Treat InfoName as if it were already in the used list
        bool Taken(string candidate) =>
            used.Contains(candidate, StringComparer.OrdinalIgnoreCase) ||
            candidate.Equals(InfoName, StringComparison.OrdinalIgnoreCase);

        if (!Taken(name)) return name;
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        for (var k = 2; ; k++)
        {
            var candidate = stem + "-" + k.ToString(CultureInfo.InvariantCulture) + ext;
            if (!Taken(candidate)) return candidate;
        }
    }
}
