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
        Assert.Contains("Bản vẽ: Tuyến 1.dwg", entries[SupportBundle.InfoName]);
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

    [Fact]
    public void Reserved_name_is_renamed_and_facts_entry_is_still_thong_tin()
    {
        var zip = Path.Combine(_folder, "out.zip");
        var f = NewFile("THONG-TIN.txt", "user content");

        var written = SupportBundle.Write(zip, null, new[] { f });

        Assert.Contains("THONG-TIN-2.txt", written);
        Assert.Equal(SupportBundle.InfoName, written[written.Count - 1]);
        var entries = Read(zip);
        Assert.True(entries.ContainsKey("THONG-TIN-2.txt"));
        Assert.True(entries.ContainsKey(SupportBundle.InfoName));
        Assert.Equal(1, entries.Keys.Count(k => k.Equals(SupportBundle.InfoName, StringComparison.OrdinalIgnoreCase)));
    }

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

    [Fact]
    public void Existing_zip_untouched_on_failure()
    {
        var zip = Path.Combine(_folder, "out.zip");
        File.WriteAllText(zip, "original");

#pragma warning disable CS0162
        IEnumerable<string> Exploding() { throw new InvalidOperationException("boom"); yield break; }
#pragma warning restore CS0162

        Assert.Throws<InvalidOperationException>(() => SupportBundle.Write(zip, null, Exploding()));
        Assert.Equal("original", File.ReadAllText(zip));
    }

    [Fact]
    public void Large_file_is_truncated_to_last_MaxFileBytes_bytes()
    {
        var zip = Path.Combine(_folder, "out.zip");
        long total = SupportBundle.MaxFileBytes + 10;
        var bytes = new byte[total];
        for (var i = 0; i < total; i++) bytes[i] = i < 10 ? (byte)0xAA : (byte)0xBB;
        var f = NewFileBinary("big.bin", bytes);

        SupportBundle.Write(zip, null, new[] { f });

        var entries = ReadBytes(zip);
        var packed = entries["big.bin"];
        Assert.Equal(SupportBundle.MaxFileBytes, packed.Length);
        Assert.All(packed, b => Assert.Equal(0xBB, b));
        var facts = Read(zip)[SupportBundle.InfoName];
        Assert.Contains("big.bin: chỉ lấy", facts);
        Assert.Contains(SupportBundle.MaxFileBytes.ToString(System.Globalization.CultureInfo.InvariantCulture), facts);
        Assert.Contains(total.ToString(System.Globalization.CultureInfo.InvariantCulture), facts);
    }

    [Fact]
    public void Locked_file_reported_as_IOException_others_still_packed()
    {
        var zip = Path.Combine(_folder, "out.zip");
        var locked = NewFile("locked.log", "secret");
        var other = NewFile("other.log", "ok");

        using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            SupportBundle.Write(zip, null, new[] { locked, other });

        var facts = Read(zip)[SupportBundle.InfoName];
        Assert.Contains("locked.log: không đọc được (IOException)", facts);
        var entries = Read(zip);
        Assert.True(entries.ContainsKey("other.log"));
        Assert.DoesNotContain("locked.log", entries.Keys.Where(k => k != SupportBundle.InfoName));
    }

    [Fact]
    public void Zip_path_in_missing_folder_throws()
    {
        var zip = Path.Combine(_folder, "nonexistent", "out.zip");
        Assert.Throws<DirectoryNotFoundException>(() => SupportBundle.Write(zip, null, null));
    }

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

    // A local last-write time of 1979-12-31 23:30 must not abort the bundle (zip format requires year >= 1980).
    [Fact]
    public void Local_year_1979_timestamp_does_not_abort_bundle()
    {
        var zip = Path.Combine(_folder, "out.zip");
        var old = NewFile("old1979.log", "ancient");
        File.SetLastWriteTime(old, new DateTime(1979, 12, 31, 23, 30, 0, DateTimeKind.Local));
        var normal = NewFile("normal.log", "recent");

        var written = SupportBundle.Write(zip, null, new[] { old, normal });

        Assert.Contains("old1979.log", written);
        Assert.Contains("normal.log", written);
        var entries = Read(zip);
        Assert.Equal("ancient", entries["old1979.log"]);
        Assert.Equal("recent", entries["normal.log"]);
    }

    // Pre-1980 timestamp must not abort the bundle; the entry is still packed, just without a timestamp.
    [Fact]
    public void Pre1980_timestamp_does_not_abort_bundle()
    {
        var zip = Path.Combine(_folder, "out.zip");
        var old = NewFile("old.log", "ancient");
        File.SetLastWriteTime(old, new DateTime(1975, 6, 1));
        var normal = NewFile("normal.log", "recent");

        var written = SupportBundle.Write(zip, null, new[] { old, normal });

        Assert.Contains("old.log", written);
        Assert.Contains("normal.log", written);
        var entries = Read(zip);
        Assert.Equal("ancient", entries["old.log"]);
        Assert.Equal("recent", entries["normal.log"]);
    }

    [Fact]
    public void Successful_run_replaces_existing_zip()
    {
        var zip = Path.Combine(_folder, "out.zip");
        SupportBundle.Write(zip, new[] { "old" }, null);
        var oldEntries = Read(zip);
        Assert.Contains("old", oldEntries[SupportBundle.InfoName]);

        SupportBundle.Write(zip, new[] { "new" }, new[] { NewFile("new.log", "new content", "sub") });

        var newEntries = Read(zip);
        Assert.Contains("new", newEntries[SupportBundle.InfoName]);
        Assert.True(newEntries.ContainsKey("new.log"));
    }

    // zipPath listed in files is skipped; the zip gets new content and no self-reference entry.
    [Fact]
    public void ZipPath_in_files_is_skipped()
    {
        var zip = Path.Combine(_folder, "out.zip");
        File.WriteAllText(zip, "previous report");
        var other = NewFile("ok.log", "data");

        var written = SupportBundle.Write(zip, null, new[] { zip, other });

        Assert.Equal(new[] { "ok.log", SupportBundle.InfoName }, written);
        var entries = Read(zip);
        Assert.False(entries.ContainsKey("out.zip"));
        Assert.DoesNotContain("out.zip", entries[SupportBundle.InfoName]);
        Assert.True(entries.ContainsKey("ok.log"));
    }
}
