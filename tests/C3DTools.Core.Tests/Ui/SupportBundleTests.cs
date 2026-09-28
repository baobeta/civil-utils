using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using C3DTools.Core.Ui;
using Xunit;

namespace C3DTools.Core.Tests.Ui;

public sealed class SupportBundleTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "c3dtools-bundle-" + Guid.NewGuid().ToString("N"));

    public SupportBundleTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, true);

    private string NewFile(string name, string text, string sub = null)
    {
        var folder = sub == null ? _folder : Path.Combine(_folder, sub);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, text);
        return path;
    }

    private string NewFileBinary(string name, byte[] bytes, string sub = null)
    {
        var folder = sub == null ? _folder : Path.Combine(_folder, sub);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static Dictionary<string, string> Read(string zip)
    {
        using (var archive = ZipFile.OpenRead(zip))
            return archive.Entries.ToDictionary(e => e.FullName, e =>
            {
                using (var reader = new StreamReader(e.Open())) return reader.ReadToEnd();
            });
    }

    private static Dictionary<string, byte[]> ReadBytes(string zip)
    {
        using (var archive = ZipFile.OpenRead(zip))
            return archive.Entries.ToDictionary(e => e.FullName, e =>
            {
                using (var ms = new MemoryStream()) { e.Open().CopyTo(ms); return ms.ToArray(); }
            });
    }

    // ── existing tests (File_ renamed to NewFile) ────────────────────────────

    [Fact]
    public void Packs_the_files_and_the_facts()
    {
        var zip = Path.Combine(_folder, "out.zip");

        var written = SupportBundle.Write(zip, new[] { "C3DTools 0.6.0", "Bản vẽ: Tuyến 1.dwg" },
            new[] { NewFile("trace.log", "mở hộp thoại"), NewFile("options.json", "{}") });

        Assert.Equal(new[] { "trace.log", "options.json", SupportBundle.InfoName }, written);
        var entries = Read(zip);
        Assert.Equal("mở hộp thoại", entries["trace.log"]);
        Assert.Equal("{}", entries["options.json"]);
        Assert.Contains("C3DTools 0.6.0", entries[SupportBundle.InfoName]);
        Assert.Contains("Bản vẽ: Tuyến 1.dwg", entries[SupportBundle.InfoName]);   // Vietnamese text survives
    }

    [Fact]
    public void A_missing_file_is_named_in_the_facts_and_does_not_stop_the_bundle()
    {
        var zip = Path.Combine(_folder, "out.zip");

        var written = SupportBundle.Write(zip, new[] { "x" }, new[] { Path.Combine(_folder, "error.log"), NewFile("trace.log", "t") });

        Assert.Equal(new[] { "trace.log", SupportBundle.InfoName }, written);
        Assert.Contains("error.log: không có", Read(zip)[SupportBundle.InfoName]);
    }

    [Fact]
    public void Two_files_with_the_same_name_both_go_in()
    {
        var zip = Path.Combine(_folder, "out.zip");

        var written = SupportBundle.Write(zip, null, new[] { NewFile("a.json", "1"), NewFile("a.json", "2", "other") });

        Assert.Equal(new[] { "a.json", "a-2.json", SupportBundle.InfoName }, written);
        Assert.Equal("2", Read(zip)["a-2.json"]);
    }

    [Fact]
    public void A_file_being_written_by_the_add_in_can_still_be_packed()
    {
        var log = NewFile("trace.log", "đang ghi");
        var zip = Path.Combine(_folder, "out.zip");

        using (new FileStream(log, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            SupportBundle.Write(zip, null, new[] { log });

        Assert.Equal("đang ghi", Read(zip)["trace.log"]);
    }

    [Fact]
    public void Nothing_to_pack_still_gives_the_facts_file()
    {
        var zip = Path.Combine(_folder, "out.zip");

        Assert.Equal(new[] { SupportBundle.InfoName }, SupportBundle.Write(zip, null, null));
        Assert.Throws<ArgumentException>(() => SupportBundle.Write(" ", null, null));
    }

    [Fact]
    public void File_name_carries_the_time() =>
        Assert.Equal("C3DTools-baoloi-20260928-153007.zip", SupportBundle.FileName(new DateTime(2026, 9, 28, 15, 30, 7)));

    // ── new tests ─────────────────────────────────────────────────────────────

    // 1. Reserved name: THONG-TIN.txt is packed as thong-tin-2.txt
    [Fact]
    public void Reserved_name_is_renamed_and_facts_entry_is_still_thong_tin()
    {
        var zip = Path.Combine(_folder, "out.zip");
        var f = NewFile("THONG-TIN.txt", "user content");

        var written = SupportBundle.Write(zip, null, new[] { f });

        // Packed file renamed (casing preserved from source); facts entry is exactly InfoName
        Assert.Contains("THONG-TIN-2.txt", written);
        Assert.Equal(SupportBundle.InfoName, written[written.Count - 1]);
        var entries = Read(zip);
        Assert.True(entries.ContainsKey("THONG-TIN-2.txt"));
        Assert.True(entries.ContainsKey(SupportBundle.InfoName));
        Assert.Equal(1, entries.Keys.Count(k => k.Equals(SupportBundle.InfoName, StringComparison.OrdinalIgnoreCase)));
    }

    // 2. Lazy enumerable that throws: no zipPath, no .tmp file left
    [Fact]
    public void Lazy_enumerable_throws_no_partial_zip_left()
    {
        var zip = Path.Combine(_folder, "out.zip");
        var good = NewFile("ok.log", "data");

        IEnumerable<string> Exploding()
        {
            yield return good;
            throw new InvalidOperationException("boom");
        }

        Assert.Throws<InvalidOperationException>(() => SupportBundle.Write(zip, null, Exploding()));
        Assert.False(File.Exists(zip));
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
    }

    // 3. Existing file at zipPath is left untouched when run fails
    [Fact]
    public void Existing_zip_untouched_on_failure()
    {
        var zip = Path.Combine(_folder, "out.zip");
        File.WriteAllText(zip, "original");

#pragma warning disable CS0162 // Unreachable code — yield break forces this to be an iterator
        IEnumerable<string> Exploding() { throw new InvalidOperationException("boom"); yield break; }
#pragma warning restore CS0162

        Assert.Throws<InvalidOperationException>(() => SupportBundle.Write(zip, null, Exploding()));
        Assert.Equal("original", File.ReadAllText(zip));
    }

    // 4. File larger than MaxFileBytes is packed as last MaxFileBytes bytes
    [Fact]
    public void Large_file_is_truncated_to_last_MaxFileBytes_bytes()
    {
        var zip = Path.Combine(_folder, "out.zip");
        long total = SupportBundle.MaxFileBytes + 10;
        var bytes = new byte[total];
        // Fill head with 0xAA, tail with 0xBB so we can detect which part was kept
        for (var i = 0; i < total; i++) bytes[i] = i < 10 ? (byte)0xAA : (byte)0xBB;
        var f = NewFileBinary("big.bin", bytes);

        SupportBundle.Write(zip, null, new[] { f });

        var entries = ReadBytes(zip);
        var packed = entries["big.bin"];
        Assert.Equal(SupportBundle.MaxFileBytes, packed.Length);
        Assert.All(packed, b => Assert.Equal(0xBB, b));  // tail only
        var facts = Read(zip)[SupportBundle.InfoName];
        Assert.Contains("big.bin: chỉ lấy", facts);
        Assert.Contains(SupportBundle.MaxFileBytes.ToString(System.Globalization.CultureInfo.InvariantCulture), facts);
        Assert.Contains(total.ToString(System.Globalization.CultureInfo.InvariantCulture), facts);
    }

    // 5. File held open with FileShare.None → không đọc được (IOException), others still packed
    [Fact]
    public void Locked_file_reported_as_IOException_others_still_packed()
    {
        var zip = Path.Combine(_folder, "out.zip");
        var locked = NewFile("locked.log", "secret");
        var other = NewFile("other.log", "ok");

        using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            SupportBundle.Write(zip, null, new[] { locked, other });
        }

        var facts = Read(zip)[SupportBundle.InfoName];
        Assert.Contains("locked.log: không đọc được (IOException)", facts);
        var entries = Read(zip);
        Assert.True(entries.ContainsKey("other.log"));
        Assert.DoesNotContain("locked.log", entries.Keys.Where(k => k != SupportBundle.InfoName));
    }

    // 6. Zip path in non-existent folder throws
    [Fact]
    public void Zip_path_in_missing_folder_throws()
    {
        var zip = Path.Combine(_folder, "nonexistent", "out.zip");
        Assert.Throws<DirectoryNotFoundException>(() => SupportBundle.Write(zip, null, null));
    }

    // 7. Third file with same name becomes a-3.json
    [Fact]
    public void Three_files_with_same_name_get_distinct_entries()
    {
        var zip = Path.Combine(_folder, "out.zip");

        var written = SupportBundle.Write(zip, null, new[]
        {
            NewFile("a.json", "1"),
            NewFile("a.json", "2", "sub2"),
            NewFile("a.json", "3", "sub3"),
        });

        Assert.Equal(new[] { "a.json", "a-2.json", "a-3.json", SupportBundle.InfoName }, written);
    }

    // 8. thong-tin.txt starts with UTF-8 BOM (EF BB BF)
    [Fact]
    public void Facts_entry_starts_with_utf8_bom()
    {
        var zip = Path.Combine(_folder, "out.zip");
        SupportBundle.Write(zip, new[] { "x" }, null);

        var raw = ReadBytes(zip)[SupportBundle.InfoName];
        Assert.True(raw.Length >= 3);
        Assert.Equal(0xEF, raw[0]);
        Assert.Equal(0xBB, raw[1]);
        Assert.Equal(0xBF, raw[2]);
    }

    // 9. Packed entry's LastWriteTime equals the source file's (even second, zip resolution)
    [Fact]
    public void Entry_last_write_time_matches_source_file()
    {
        var zip = Path.Combine(_folder, "out.zip");
        var f = NewFile("stamp.log", "data");
        var stamp = new DateTime(2026, 1, 2, 10, 20, 30, DateTimeKind.Local);
        File.SetLastWriteTime(f, stamp);

        SupportBundle.Write(zip, null, new[] { f });

        using (var archive = ZipFile.OpenRead(zip))
        {
            var entry = archive.Entries.First(e => e.Name == "stamp.log");
            Assert.Equal(stamp, entry.LastWriteTime.LocalDateTime);
        }
    }

    // 10. zipPath listed in files is skipped silently
    [Fact]
    public void ZipPath_in_files_is_skipped()
    {
        var zip = Path.Combine(_folder, "out.zip");
        var other = NewFile("ok.log", "data");

        var written = SupportBundle.Write(zip, null, new[] { zip, other });

        Assert.Equal(new[] { "ok.log", SupportBundle.InfoName }, written);
        var entries = Read(zip);
        Assert.False(entries.ContainsKey("out.zip"));
    }
}
