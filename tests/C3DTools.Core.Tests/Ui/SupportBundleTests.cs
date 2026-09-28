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

    private string File_(string name, string text, string sub = null)
    {
        var folder = sub == null ? _folder : Path.Combine(_folder, sub);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, text);
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

    [Fact]
    public void Packs_the_files_and_the_facts()
    {
        var zip = Path.Combine(_folder, "out.zip");

        var written = SupportBundle.Write(zip, new[] { "C3DTools 0.6.0", "Bản vẽ: Tuyến 1.dwg" },
            new[] { File_("trace.log", "mở hộp thoại"), File_("options.json", "{}") });

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

        var written = SupportBundle.Write(zip, new[] { "x" }, new[] { Path.Combine(_folder, "error.log"), File_("trace.log", "t") });

        Assert.Equal(new[] { "trace.log", SupportBundle.InfoName }, written);
        Assert.Contains("error.log: không có", Read(zip)[SupportBundle.InfoName]);
    }

    [Fact]
    public void Two_files_with_the_same_name_both_go_in()
    {
        var zip = Path.Combine(_folder, "out.zip");

        var written = SupportBundle.Write(zip, null, new[] { File_("a.json", "1"), File_("a.json", "2", "other") });

        Assert.Equal(new[] { "a.json", "a-2.json", SupportBundle.InfoName }, written);
        Assert.Equal("2", Read(zip)["a-2.json"]);
    }

    [Fact]
    public void A_file_being_written_by_the_add_in_can_still_be_packed()
    {
        var log = File_("trace.log", "đang ghi");
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
}
