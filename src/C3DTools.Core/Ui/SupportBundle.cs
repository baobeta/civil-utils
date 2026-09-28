using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
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
    /// Writes the archive to a temporary file next to <paramref name="zipPath"/> and puts it in place only when
    /// complete. If anything fails the temporary file is deleted and <paramref name="zipPath"/> is left untouched.
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
                    // Guard Path.GetFileName against illegal-character exceptions (.NET Framework throws
                    // ArgumentException or NotSupportedException for paths with illegal characters).
                    string fileName;
                    try
                    {
                        fileName = Path.GetFileName(rawPath);
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
                    {
                        // Cannot extract a file name — skip silently with a generic message, no path printed.
                        lines.Add("đường dẫn không hợp lệ");
                        continue;
                    }

                    // Skip if the path resolves to the zip output itself.
                    string fullRaw;
                    try { fullRaw = Path.GetFullPath(rawPath); }
                    catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException ||
                                               ex is PathTooLongException || ex is SecurityException)
                    { fullRaw = null; }
                    if (fullRaw != null && string.Equals(fullRaw, fullZip, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!File.Exists(rawPath))
                    {
                        lines.Add(fileName + ": không có");
                        continue;
                    }

                    // Only reading is inside the try/catch; writing to the archive is outside so a write
                    // failure propagates and the cleanup block removes the temporary file.
                    byte[] bytes;
                    int bytesRead;
                    long totalLength;
                    bool wasCapped;
                    DateTime? lastWrite = null;
                    try
                    {
                        // trace.log may be open for append by the running add-in (FileShare.ReadWrite).
                        using var source = new FileStream(rawPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        totalLength = source.Length;
                        wasCapped = totalLength > MaxFileBytes;
                        if (wasCapped)
                            source.Seek(totalLength - MaxFileBytes, SeekOrigin.Begin);

                        // Allocate a buffer of at most MaxFileBytes. An empty-at-open file still gets a buffer
                        // because it may be written while we read.
                        var cap = (int)Math.Min(MaxFileBytes, totalLength > 0 ? totalLength : MaxFileBytes);
                        var buf = new byte[cap];
                        var offset = 0;
                        int n;
                        while (offset < buf.Length && (n = source.Read(buf, offset, buf.Length - offset)) > 0)
                            offset += n;
                        bytesRead = offset;
                        if (offset == buf.Length)
                        {
                            bytes = buf;
                        }
                        else
                        {
                            bytes = new byte[offset];
                            Array.Copy(buf, bytes, offset);
                        }

                        // Read the timestamp while still inside the guarded block so a failure here is
                        // treated like any other read error rather than aborting the whole bundle.
                        // The zip format only accepts years 1980–2107; compare the LOCAL calendar year
                        // (the value File.GetLastWriteTime returns) so the check is correct in every time zone.
                        var raw = File.GetLastWriteTime(rawPath);
                        if (raw.Year >= 1980 && raw.Year <= 2107)
                            lastWrite = raw;
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        // Use the exception type name only — the message may contain the user's account path.
                        lines.Add(fileName + ": không đọc được (" + ex.GetType().Name + ")");
                        continue;
                    }

                    // Reserve InfoName — rename the packed file if its name collides with the facts entry.
                    var name = UniqueNotReserved(fileName, written);

                    // Archive writes are outside the catch — failures propagate and cleanup removes the tmp file.
                    var entry = zip.CreateEntry(name);
                    if (lastWrite.HasValue)
                        entry.LastWriteTime = lastWrite.Value;
                    using (var target = entry.Open())
                        target.Write(bytes, 0, bytesRead);
                    written.Add(name);

                    // Write the truncation line only when bytes were actually left out.
                    if (wasCapped && bytesRead < totalLength)
                        lines.Add(string.Format(CultureInfo.InvariantCulture,
                            "{0}: chỉ lấy {1} byte cuối của {2} byte",
                            fileName, bytesRead, totalLength));
                }

                // Facts entry — always InfoName, always last.
                var infoEntry = zip.CreateEntry(InfoName);
                using (var writer = new StreamWriter(infoEntry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
                    foreach (var line in lines)
                        writer.WriteLine(line);
                written.Add(InfoName);
            }

            // Replaces the target in one step, so a failure leaves the old file in place.
            if (File.Exists(fullZip))
                File.Replace(tmp, fullZip, null);
            else
                File.Move(tmp, fullZip);

            return written;
        }
        catch
        {
            // Clean up the temporary file on any failure so no partial zip is left behind.
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
        // Treat InfoName as if it were already in the used list.
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
