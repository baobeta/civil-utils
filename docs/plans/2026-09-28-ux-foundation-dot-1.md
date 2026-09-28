# UX Foundation (Đợt 1) Implementation Plan

> **Status (2026-09-28): implemented and released as 0.6.0.** The code in Tasks 3–7 below is the first draft. Review changed it: the active route is remembered in session memory on a successful load (`RoutePicker.Remember`) and written to the drawing only inside a command's own write transaction (`RoutePicker.Save`); `RoutePicker.Use` does not exist; the dialog footer is a Grid; `SupportBundle` writes through a temporary file and caps large logs; CTBAOLOI isolates the Civil 3D facts. The source is the reference, not this document.


> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** make every C3DTools command easier to use without changing what it computes: an active route chosen once, dialogs that show few fields first, a way back to defaults, a ribbon ordered like the work, and one-command bug reports.

**Architecture:** unchanged. Rules and dialog state in Core (netstandard2.0, xUnit on macOS); thin code-only WPF windows on `ToolWindow` and drawing access in the host (net48). Everything here lives in the shared shell (`ToolWindow`, `RibbonSetup`, a new `RoutePicker`), so existing commands gain it with small edits to their loops and windows. The only AutoCAD feature used for the first time is an Xrecord in the Named Object Dictionary.

**Tech Stack:** as 0.5 — net48 x64 host, `AutoCAD.NET` 24.0.0 and `Civil3D2021.Base` 1.0.0 (compile-only), xUnit for Core, `System.IO.Compression` (already used by `MinimalXlsxWriter`), Pillow for the two icon pairs.

---

## Read this first

- **Repo rules:** `CLAUDE.md`. User-facing text is Vietnamese; identifiers and comments are English; numbers use `CultureInfo.InvariantCulture`; business rules go in Core.
- **No Civil 3D on the dev machine.** The host is only compiled here. "Done" for a host task means: Release build at **0 warnings, 0 errors**, Core tests green, and the runtime checks written into `docs/testing.md` (Task 9) for the tester.
- **Commands used in every task** (run from the repo root):

  | Purpose | Command | Expected |
  | --- | --- | --- |
  | Core tests | `dotnet test C3DTools.Core.slnf -v q --nologo` | `Passed!  - Failed: 0` |
  | One test class | `dotnet test C3DTools.Core.slnf -v q --nologo --filter <name>` | `Passed!` with the count given in the task |
  | Host build | `dotnet build src/C3DTools.Civil2021 -c Release -v q --nologo` | `0 Warning(s)` and `0 Error(s)` |

- **Code blocks.** A block headed **Create** is the whole file. A block headed **Modify** is a unified diff: lines starting with `-` are removed, `+` added, the rest is context to find the place. Apply it by hand or with `git apply`.
- **Provenance.** Every block below was written and checked on 2026-09-28 against `main` at `f5130c5` (v0.5.12): each task was committed on its own and, at that commit, the host built at 0 warnings and the Core suite passed (918 → 935 tests). If `main` has moved, the diffs' context lines may need adjusting.
- **Why this đợt:** evidence from the 0.5 trial — a remembered unticked box removed the station from Km stakes; CTDANHCOC showed 14 options at once; every command asked for the alignment again; the Tuyến panel stacked 7 buttons; logs had to be found by hand. Scope decision (product-wide, in stages) and research: `docs/research/2026-09-28-trac-doc-trac-ngang.md`.
- **Not in this đợt:** Quy ước văn phòng dialog (đợt 2), workflow palette and "Cập nhật tất cả" (đợt 3), one-click profile and section commands (đợt 4), live preview (đợt 5).

## Task order

| Task | Item | Where |
| --- | --- | --- |
| T1 | Forget a command's remembered options | Core |
| T2 | Which alignment a command works on | Core |
| T3 | Support bundle | Core |
| T4 | Dialog shell: Nâng cao and Về mặc định | Host |
| T5 | Active route in the host | Host |
| T6 | Two-tier dialogs and reset in CTTUYEN, CTPHATCOC, CTDANHCOC | Host + Core test |
| T7 | CTBAOLOI | Host |
| T8 | Ribbon by stage | Host |
| T9 | Docs, packaging, release 0.6.0 | Host |

---

### Task 1: Forget a command's remembered options

`Về mặc định` has to erase what a dialog remembered, but not how its window looks. Today a remembered value can only be overwritten, never removed.

**Files:**
- Modify: `src/C3DTools.Core/Ui/DialogOptionsMemory.cs`
- Test (modify): `tests/C3DTools.Core.Tests/Ui/DialogOptionsMemoryTests.cs`

**Step 1: Write the failing test**

**Modify** `tests/C3DTools.Core.Tests/Ui/DialogOptionsMemoryTests.cs`

```diff
--- a/tests/C3DTools.Core.Tests/Ui/DialogOptionsMemoryTests.cs
+++ b/tests/C3DTools.Core.Tests/Ui/DialogOptionsMemoryTests.cs
@@ -121,4 +121,25 @@ public class DialogOptionsMemoryTests
 
         Assert.Equal(0, DialogOptionsMemory.LoadFile(path).Count);
     }
+
+    [Fact]
+    public void Clear_removes_one_commands_options_and_keeps_the_named_ones()
+    {
+        var m = new DialogOptionsMemory();
+        m.Set("CTPHATCOC", "Width", 760.0);
+        m.Set("CTPHATCOC", "Straight", "100");
+        m.Set("CTPHATCOC", "Labels", false);
+        m.Set("CTPHATCOCX", "Straight", "5");   // another command that starts with the same letters
+        m.Set("CTDANHCOC", "Prefix", "D");
+
+        var removed = m.Clear("CTPHATCOC", "Width", "Height");
+
+        Assert.Equal(2, removed);
+        Assert.Equal(760.0, m.Get("CTPHATCOC", "Width", 0.0));
+        Assert.Equal("20", m.Get("CTPHATCOC", "Straight", "20"));
+        Assert.True(m.Get("CTPHATCOC", "Labels", true));
+        Assert.Equal("5", m.Get("CTPHATCOCX", "Straight", ""));
+        Assert.Equal("D", m.Get("CTDANHCOC", "Prefix", ""));
+        Assert.Equal(0, m.Clear("CTKHONGCO"));
+    }
 }
```

**Step 2: Run the test to see it fail**

Run: `dotnet test C3DTools.Core.slnf -v q --nologo --filter Clear_removes`
Expected: the build fails with `does not contain a definition for 'Clear'`.

**Step 3: Write the implementation**

**Modify** `src/C3DTools.Core/Ui/DialogOptionsMemory.cs`

```diff
--- a/src/C3DTools.Core/Ui/DialogOptionsMemory.cs
+++ b/src/C3DTools.Core/Ui/DialogOptionsMemory.cs
@@ -2,6 +2,7 @@ using System;
 using System.Collections.Generic;
 using System.Globalization;
 using System.IO;
+using System.Linq;
 using Newtonsoft.Json;
 
 namespace C3DTools.Core.Ui;
@@ -72,6 +73,16 @@ public sealed class DialogOptionsMemory
 
     public void Set<T>(string command, string option, T value) => _values[Key(command, option)] = Format(value);
 
+    /// <summary>Forgets every option of the command except the named ones (e.g. the window size). Returns how many were removed.</summary>
+    public int Clear(string command, params string[] keep)
+    {
+        var prefix = command + ".";
+        var kept = new HashSet<string>((keep ?? new string[0]).Select(k => Key(command, k)), StringComparer.OrdinalIgnoreCase);
+        var doomed = _values.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !kept.Contains(k)).ToList();
+        foreach (var key in doomed) _values.Remove(key);
+        return doomed.Count;
+    }
+
     private static string Format<T>(T value)
     {
         object o = value;
```

**Step 4: Run the test to see it pass**

Run: `dotnet test C3DTools.Core.slnf -v q --nologo --filter Clear_removes`
Expected: `Passed!  - Failed: 0, Passed: 1`.

**Step 5: Run the whole suite**

Run: `dotnet test C3DTools.Core.slnf -v q --nologo`
Expected: `Passed!  - Failed: 0`.

**Step 6: Commit**

```bash
git add src/C3DTools.Core/Ui/DialogOptionsMemory.cs tests/C3DTools.Core.Tests/Ui/DialogOptionsMemoryTests.cs
git commit -m "feat(core): forget a command's remembered options"
```

### Task 2: Which alignment a command works on

Rule, in order: the alignment selected before the command → the drawing's active route, if it still exists → the drawing's only alignment → none (the command asks). Pure logic on handles, so it is tested without AutoCAD.

**Files:**
- Create: `src/C3DTools.Core/Ui/ActiveRoute.cs`
- Test (create): `tests/C3DTools.Core.Tests/Ui/ActiveRouteTests.cs`

**Step 1: Write the failing test**

**Create** `tests/C3DTools.Core.Tests/Ui/ActiveRouteTests.cs`

```csharp
using C3DTools.Core.Ui;
using Xunit;

namespace C3DTools.Core.Tests.Ui;

public class ActiveRouteTests
{
    private static readonly string[] Three = { "1A", "2B", "3C" };

    [Fact]
    public void Preselected_wins() =>
        Assert.Equal(new RouteChoice("2B", RouteChoiceReason.Preselected), ActiveRoute.Resolve("2B", "1A", Three));

    [Fact]
    public void Then_the_stored_route_when_it_still_exists() =>
        Assert.Equal(new RouteChoice("1A", RouteChoiceReason.Active), ActiveRoute.Resolve(null, "1a", Three));

    [Fact]
    public void A_stored_route_that_was_erased_is_ignored() =>
        Assert.Equal(RouteChoice.None, ActiveRoute.Resolve(null, "9Z", Three));

    [Fact]
    public void A_single_alignment_needs_no_choice() =>
        Assert.Equal(new RouteChoice("1A", RouteChoiceReason.OnlyOne), ActiveRoute.Resolve(null, null, new[] { "1A" }));

    [Fact]
    public void Preselected_must_be_an_alignment_of_the_drawing() =>
        Assert.Equal(new RouteChoice("1A", RouteChoiceReason.Active), ActiveRoute.Resolve("77", "1A", Three));

    [Fact]
    public void Nothing_to_choose_from()
    {
        Assert.Equal(RouteChoice.None, ActiveRoute.Resolve(null, null, new string[0]));
        Assert.Equal(RouteChoice.None, ActiveRoute.Resolve(null, null, null));
        Assert.False(RouteChoice.None.Found);
    }

    [Theory]
    [InlineData(RouteChoiceReason.Active, "Tuyến hiện hành: T1")]
    [InlineData(RouteChoiceReason.OnlyOne, "Bản vẽ có một tuyến: T1")]
    [InlineData(RouteChoiceReason.Preselected, null)]
    [InlineData(RouteChoiceReason.None, null)]
    public void Describe(RouteChoiceReason reason, string text) => Assert.Equal(text, ActiveRoute.Describe(reason, "T1"));
}
```

**Step 2: Run the test to see it fail**

Run: `dotnet test C3DTools.Core.slnf -v q --nologo --filter ActiveRouteTests`
Expected: the build fails with `The type or namespace name 'RouteChoice' could not be found`.

**Step 3: Write the implementation**

**Create** `src/C3DTools.Core/Ui/ActiveRoute.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace C3DTools.Core.Ui;

public enum RouteChoiceReason { None, Preselected, Active, OnlyOne }

/// <summary>The alignment (by handle) a command works on and why; Handle is null when the user has to pick.</summary>
public readonly struct RouteChoice : IEquatable<RouteChoice>
{
    public static readonly RouteChoice None = new RouteChoice(null, RouteChoiceReason.None);

    public RouteChoice(string handle, RouteChoiceReason reason)
    {
        Handle = handle;
        Reason = reason;
    }

    public string Handle { get; }
    public RouteChoiceReason Reason { get; }
    public bool Found => Handle != null;

    public bool Equals(RouteChoice other) =>
        string.Equals(Handle, other.Handle, StringComparison.OrdinalIgnoreCase) && Reason == other.Reason;

    public override bool Equals(object obj) => obj is RouteChoice other && Equals(other);
    public override int GetHashCode() => (Handle ?? "").ToUpperInvariant().GetHashCode() ^ (int)Reason;
    public override string ToString() => Reason + ":" + Handle;
}

/// <summary>"Tuyến hiện hành": commands stop asking for the alignment once the drawing has one in use.</summary>
public static class ActiveRoute
{
    /// <summary>
    /// In order: the alignment selected before the command, the route remembered in the drawing if it still exists,
    /// the drawing's only alignment. Otherwise None and the command asks.
    /// </summary>
    /// <param name="preselected">Handle of the alignment selected before the command, or null.</param>
    /// <param name="stored">Handle remembered in the drawing, or null.</param>
    /// <param name="available">Handles of the drawing's alignments.</param>
    public static RouteChoice Resolve(string preselected, string stored, IEnumerable<string> available)
    {
        var all = (available ?? Enumerable.Empty<string>()).Where(h => !string.IsNullOrEmpty(h)).ToList();
        string Find(string h) => h == null ? null : all.FirstOrDefault(a => string.Equals(a, h, StringComparison.OrdinalIgnoreCase));

        var picked = Find(preselected);
        if (picked != null) return new RouteChoice(picked, RouteChoiceReason.Preselected);
        var active = Find(stored);
        if (active != null) return new RouteChoice(active, RouteChoiceReason.Active);
        return all.Count == 1 ? new RouteChoice(all[0], RouteChoiceReason.OnlyOne) : RouteChoice.None;
    }

    /// <summary>What the command line says about the choice; null when there is nothing to say.</summary>
    public static string Describe(RouteChoiceReason reason, string name) => reason switch
    {
        RouteChoiceReason.Active => "Tuyến hiện hành: " + name,
        RouteChoiceReason.OnlyOne => "Bản vẽ có một tuyến: " + name,
        _ => null,
    };
}
```

**Step 4: Run the test to see it pass**

Run: `dotnet test C3DTools.Core.slnf -v q --nologo --filter ActiveRouteTests`
Expected: `Passed!  - Failed: 0, Passed: 10`.

**Step 5: Run the whole suite**

Run: `dotnet test C3DTools.Core.slnf -v q --nologo`
Expected: `Passed!  - Failed: 0`.

**Step 6: Commit**

```bash
git add src/C3DTools.Core/Ui/ActiveRoute.cs tests/C3DTools.Core.Tests/Ui/ActiveRouteTests.cs
git commit -m "feat(core): resolve the active route"
```

### Task 3: Support bundle

One zip for a bug report: logs, remembered options, and a text file of facts. A missing or locked file must not stop it; `trace.log` is usually open for append while the add-in runs. Only the files passed in are packed, so a drawing can never end up in it.

**Files:**
- Create: `src/C3DTools.Core/Ui/SupportBundle.cs`
- Test (create): `tests/C3DTools.Core.Tests/Ui/SupportBundleTests.cs`

**Step 1: Write the failing test**

**Create** `tests/C3DTools.Core.Tests/Ui/SupportBundleTests.cs`

```csharp
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
```

**Step 2: Run the test to see it fail**

Run: `dotnet test C3DTools.Core.slnf -v q --nologo --filter SupportBundleTests`
Expected: the build fails with `The name 'SupportBundle' does not exist`.

**Step 3: Write the implementation**

**Create** `src/C3DTools.Core/Ui/SupportBundle.cs`

```csharp
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
```

**Step 4: Run the test to see it pass**

Run: `dotnet test C3DTools.Core.slnf -v q --nologo --filter SupportBundleTests`
Expected: `Passed!  - Failed: 0, Passed: 6`.

**Step 5: Run the whole suite**

Run: `dotnet test C3DTools.Core.slnf -v q --nologo`
Expected: `Passed!  - Failed: 0`.

**Step 6: Commit**

```bash
git add src/C3DTools.Core/Ui/SupportBundle.cs tests/C3DTools.Core.Tests/Ui/SupportBundleTests.cs
git commit -m "feat(core): support bundle for bug reports"
```

### Task 4: Dialog shell: Nâng cao and Về mặc định

Both live in `ToolWindow`, the base of all 14 dialogs, so a dialog opts in with one call each. `DialogAction.Reset` is returned like any other action; only windows that pass `withReset: true` show the button, because their command loop has to handle it (Task 6). Width, Height and the Nâng cao state survive a reset: they are how the window looks, not what the command does.

**Files:**
- Modify: `src/C3DTools.Civil2021/Ui/ToolWindow.cs`

**Step 1: Edit `ToolWindow.cs`**

**Modify** `src/C3DTools.Civil2021/Ui/ToolWindow.cs`

```diff
--- a/src/C3DTools.Civil2021/Ui/ToolWindow.cs
+++ b/src/C3DTools.Civil2021/Ui/ToolWindow.cs
@@ -1,4 +1,5 @@
 using System;
+using System.Collections.Generic;
 using System.Globalization;
 using System.IO;
 using System.Windows;
@@ -14,7 +15,7 @@ using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
 namespace C3DTools.Civil2021.Ui;
 
 /// <summary>What the dialog asks the command to do after it closes.</summary>
-internal enum DialogAction { Cancel, Pick, ZoomToPi, ReadWidening, Preview, Apply, Count, PickPoints, PickFrom, PickTo }
+internal enum DialogAction { Cancel, Pick, ZoomToPi, ReadWidening, Preview, Apply, Count, PickPoints, PickFrom, PickTo, Reset }
 
 /// <summary>
 /// The skeleton every C3DTools dialog shares (UI rule 2), built in code (no XAML): header with the source and
@@ -27,6 +28,9 @@ internal abstract class ToolWindow : Window
     public static readonly Brush ErrorBrush = Frozen(Color.FromRgb(0xFF, 0xD6, 0xD6));
     public static readonly Brush ReadOnlyBrush = Frozen(Color.FromRgb(0xEE, 0xEE, 0xEE));
 
+    /// <summary>Option names that "Về mặc định" keeps: how the window looks, not what the command does.</summary>
+    private const string WidthOption = "Width", HeightOption = "Height", AdvancedOption = "Advanced";
+
     private static DialogOptionsMemory _options;
 
     static ToolWindow()
@@ -46,8 +50,8 @@ internal abstract class ToolWindow : Window
         FontSize = 12;
         MinWidth = minWidth;
         MinHeight = minHeight;
-        Width = RememberedSize("Width", width, minWidth, SystemParameters.WorkArea.Width);
-        Height = RememberedSize("Height", height, minHeight, SystemParameters.WorkArea.Height);
+        Width = RememberedSize(WidthOption, width, minWidth, SystemParameters.WorkArea.Width);
+        Height = RememberedSize(HeightOption, height, minHeight, SystemParameters.WorkArea.Height);
         WindowStartupLocation = WindowStartupLocation.CenterOwner;
         ShowInTaskbar = false;
         Closed += (s, e) =>
@@ -123,6 +127,14 @@ internal abstract class ToolWindow : Window
         }
     }
 
+    /// <summary>"Về mặc định": forgets the command's remembered values; window size and the Nâng cao state stay.</summary>
+    public static void ResetOptions(string command)
+    {
+        Options.Clear(command, WidthOption, HeightOption, AdvancedOption);
+        SaveOptions();
+        Trace(command + ": về mặc định");
+    }
+
     /// <summary>Shows the dialog modal to AutoCAD and returns what the user chose.</summary>
     public DialogAction ShowModal()
     {
@@ -194,8 +206,35 @@ internal abstract class ToolWindow : Window
         return row;
     }
 
-    /// <summary>Summary text on the left; Xem trước, Áp dụng (both enabled by canApplyPath) and Hủy on the right.</summary>
-    protected DockPanel BuildFooter(string summaryPath, string canApplyPath)
+    /// <summary>
+    /// "Nâng cao": the rows a user rarely changes, folded away until asked for. Whether it is open is remembered per command.
+    /// </summary>
+    protected Expander Advanced(params UIElement[] rows)
+    {
+        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
+        foreach (var row in rows) panel.Children.Add(row);
+        var expander = new Expander
+        {
+            Header = "Nâng cao",
+            Content = panel,
+            Margin = new Thickness(0, 6, 0, 2),
+            IsExpanded = Options.Get(Command, AdvancedOption, false),
+        };
+        RoutedEventHandler remember = (s, e) =>
+        {
+            Options.Set(Command, AdvancedOption, expander.IsExpanded);
+            SaveOptions();
+        };
+        expander.Expanded += remember;
+        expander.Collapsed += remember;
+        return expander;
+    }
+
+    /// <summary>
+    /// Summary text on the left; Xem trước, Áp dụng (both enabled by canApplyPath) and Hủy on the right.
+    /// withReset adds "Về mặc định" (DialogAction.Reset): only for commands whose loop handles it.
+    /// </summary>
+    protected DockPanel BuildFooter(string summaryPath, string canApplyPath, bool withReset = false)
     {
         var actions = new DockPanel { Margin = new Thickness(0, 6, 0, 0), LastChildFill = false };
         var summary = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
@@ -207,7 +246,15 @@ internal abstract class ToolWindow : Window
         apply.SetBinding(IsEnabledProperty, new Binding(canApplyPath));
         var preview = ActionButton("Xem trước", DialogAction.Preview);
         preview.SetBinding(IsEnabledProperty, new Binding(canApplyPath));
-        foreach (var b in new[] { cancel, apply, preview })
+        var buttons = new List<Button> { cancel, apply, preview };
+        if (withReset)
+        {
+            var reset = ActionButton("Về mặc định", DialogAction.Reset);
+            reset.ToolTip = "Đặt lại mọi ô của hộp thoại này về giá trị ban đầu (giữ nguyên tuyến đang chọn)";
+            buttons.Add(reset);
+        }
+
+        foreach (var b in buttons)
         {
             DockPanel.SetDock(b, Dock.Right);
             actions.Children.Add(b);
```

**Step 2: Build the host**

Run: `dotnet build src/C3DTools.Civil2021 -c Release -v q --nologo`
Expected: `0 Warning(s)` and `0 Error(s)`.

**Step 3: Run the Core suite**

Run: `dotnet test C3DTools.Core.slnf -v q --nologo`
Expected: `Passed!  - Failed: 0`.

**Step 4: Commit**

```bash
git add src/C3DTools.Civil2021/Ui/ToolWindow.cs
git commit -m "feat(host): dialog shell — Nâng cao section, Về mặc định button"
```

### Task 5: Active route in the host

`ActiveRouteStore` keeps the handle in an Xrecord of the drawing's Named Object Dictionary, so it is saved with the drawing. `RoutePicker.Resolve` applies Task 2's rule; `RoutePicker.Use` is called whenever the user picks an alignment, so picking is how the active route changes and no extra step is forced on anyone. `CTTUYENHH` shows or changes it. CTTOADO and CTBANGCONG lose their private `PickFirst`. Not changed here: CTYTC and CTYTCBANG (their source may be a polyline), the profile and section commands (they pick a view), the 0.1 prompt commands.

**Files:**
- Create: `src/C3DTools.Civil2021/Commands/ActiveRouteCommand.cs`
- Modify: `src/C3DTools.Civil2021/Commands/CulvertTableCommand.cs`
- Modify: `src/C3DTools.Civil2021/Commands/StakeTableCommand.cs`
- Create: `src/C3DTools.Civil2021/Services/RoutePicker.cs`

**Step 1: Create `RoutePicker.cs`**

**Create** `src/C3DTools.Civil2021/Services/RoutePicker.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Ui;

namespace C3DTools.Civil2021.Services;

/// <summary>
/// The active route of a drawing: an Xrecord in the Named Object Dictionary holding the alignment's handle, so it is
/// saved with the drawing.
/// </summary>
internal static class ActiveRouteStore
{
    public const string Key = "C3DTOOLS_ACTIVE_ROUTE";

    /// <summary>The stored handle, or null. Never throws: a missing or damaged record means no active route.</summary>
    public static string Read(Transaction tr, Database db)
    {
        try
        {
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!nod.Contains(Key)) return null;
            if (!(tr.GetObject(nod.GetAt(Key), OpenMode.ForRead) is Xrecord record)) return null;
            using (var data = record.Data)
            {
                var values = data?.AsArray();
                return values != null && values.Length > 0 ? values[0].Value as string : null;
            }
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    public static void Write(Transaction tr, Database db, string handle)
    {
        var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
        Xrecord record;
        if (nod.Contains(Key))
        {
            record = (Xrecord)tr.GetObject(nod.GetAt(Key), OpenMode.ForWrite);
        }
        else
        {
            record = new Xrecord();
            nod.SetAt(Key, record);
            tr.AddNewlyCreatedDBObject(record, true);
        }

        using (var data = new ResultBuffer(new TypedValue((int)DxfCode.Text, handle)))
            record.Data = data;
    }
}

/// <summary>"Tuyến hiện hành": which alignment a command starts with, and remembering the one the user picks.</summary>
internal static class RoutePicker
{
    /// <summary>
    /// The alignment to start with (ActiveRoute.Resolve): selected before the command, else the drawing's active
    /// route, else its only alignment; ObjectId.Null when the user has to pick. message: what to tell the user, or null.
    /// </summary>
    public static ObjectId Resolve(Document doc, out string message)
    {
        message = null;
        var ed = doc.Editor;
        var preselected = Preselected(ed);
        try
        {
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var civil = CivilDocument.GetCivilDocument(doc.Database);
                var alignments = civil.GetAlignmentIds().Cast<ObjectId>()
                    .Select(id => tr.GetObject(id, OpenMode.ForRead) as Alignment)
                    .Where(a => a != null)
                    .ToList();
                var choice = ActiveRoute.Resolve(
                    preselected.IsNull ? null : preselected.Handle.ToString(),
                    ActiveRouteStore.Read(tr, doc.Database),
                    alignments.Select(a => a.Handle.ToString()));
                tr.Commit();
                if (!choice.Found) return ObjectId.Null;

                var chosen = alignments.First(a => string.Equals(a.Handle.ToString(), choice.Handle, StringComparison.OrdinalIgnoreCase));
                message = ActiveRoute.Describe(choice.Reason, chosen.Name);
                ToolWindow.Trace($"tuyến: {choice.Reason} {chosen.Name}");
                return chosen.ObjectId;
            }
        }
        catch (System.Exception ex)
        {
            ToolWindow.LogError("RoutePicker.Resolve", ex);
            return preselected;
        }
    }

    /// <summary>Makes the alignment the drawing's active route. A failure only means the next command asks again.</summary>
    public static void Use(Document doc, ObjectId alignmentId)
    {
        if (alignmentId.IsNull) return;
        try
        {
            using (doc.LockDocument())
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                if (!(tr.GetObject(alignmentId, OpenMode.ForRead) is Alignment alignment)) return;
                var handle = alignment.Handle.ToString();
                if (!string.Equals(ActiveRouteStore.Read(tr, doc.Database), handle, StringComparison.OrdinalIgnoreCase))
                    ActiveRouteStore.Write(tr, doc.Database, handle);
                tr.Commit();
            }
        }
        catch (System.Exception ex)
        {
            ToolWindow.LogError("RoutePicker.Use", ex);
        }
    }

    /// <summary>The first alignment selected before the command; the selection is consumed.</summary>
    private static ObjectId Preselected(Editor ed)
    {
        var implied = ed.SelectImplied();
        if (implied.Status != PromptStatus.OK || implied.Value == null) return ObjectId.Null;
        ed.SetImpliedSelection(new ObjectId[0]);
        var alignmentClass = RXObject.GetClass(typeof(Alignment));
        return implied.Value.GetObjectIds().FirstOrDefault(id => id.ObjectClass.IsDerivedFrom(alignmentClass));
    }
}
```


**Step 2: Create `ActiveRouteCommand.cs`**

**Create** `src/C3DTools.Civil2021/Commands/ActiveRouteCommand.cs`

```csharp
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Services;
using C3DTools.Civil2021.Ui;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.ActiveRouteCommand))]

namespace C3DTools.Civil2021.Commands;

public class ActiveRouteCommand
{
    /// <summary>CTTUYENHH: picks the drawing's active route; Enter shows the current one.</summary>
    [CommandMethod("C3DTOOLS", "CTTUYENHH", CommandFlags.Modal | CommandFlags.UsePickSet)]
    public void SetActiveRoute()
    {
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        try
        {
            var current = RoutePicker.Resolve(doc, out var message);
            Prompts.Say(ed, message ?? (current.IsNull ? "Chưa có tuyến hiện hành." : "Tuyến đã chọn."));
            var picked = Prompts.PickEntity<Alignment>(ed, "Chọn alignment làm tuyến hiện hành <Enter: giữ nguyên>: ");
            if (picked.IsNull) picked = current;
            if (picked.IsNull) return;
            RoutePicker.Use(doc, picked);
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                Prompts.Say(ed, "Tuyến hiện hành: " + ((Alignment)tr.GetObject(picked, OpenMode.ForRead)).Name + "\n");
                tr.Commit();
            }
        }
        catch (System.Exception ex)
        {
            ToolWindow.LogError("CTTUYENHH", ex);
            Prompts.Say(ed, $"Lỗi C3DTools: {ex.Message}");
        }
    }
}
```


**Step 3: Edit `CulvertTableCommand.cs`**

**Modify** `src/C3DTools.Civil2021/Commands/CulvertTableCommand.cs`

```diff
--- a/src/C3DTools.Civil2021/Commands/CulvertTableCommand.cs
+++ b/src/C3DTools.Civil2021/Commands/CulvertTableCommand.cs
@@ -10,6 +10,7 @@ using Autodesk.Civil.DatabaseServices;
 using C3DTools.Civil2021.Curves;
 using C3DTools.Civil2021.Drainage;
 using C3DTools.Civil2021.Drawing;
+using C3DTools.Civil2021.Services;
 using C3DTools.Civil2021.Ui;
 using C3DTools.Core.Drainage;
 using C3DTools.Core.Presets;
@@ -76,7 +77,8 @@ public class CulvertTableCommand
         if (networks.Count == 0) Prompts.Say(ed, "Bản vẽ không có mạng cống (pipe network).");
 
         Route route = null;
-        var first = PickFirst(ed);
+        var first = RoutePicker.Resolve(doc, out var why);
+        if (why != null) Prompts.Say(ed, why);
         if (!first.IsNull) route = Load(doc, first, session);
         if (route != null && session.CanApply) Preview(doc, route, session, networks, surfaces, preset);
 
@@ -96,6 +98,7 @@ public class CulvertTableCommand
                 case DialogAction.Pick:
                     var picked = Prompts.PickEntity<Alignment>(ed, "Chọn alignment: ");
                     if (picked.IsNull) continue;
+                    RoutePicker.Use(doc, picked);
                     var loaded = Load(doc, picked, session);
                     if (loaded == null) continue;
                     route = loaded;
@@ -121,16 +124,6 @@ public class CulvertTableCommand
         }
     }
 
-    /// <summary>The first alignment of the selection made before the command, or ObjectId.Null.</summary>
-    private static ObjectId PickFirst(Editor ed)
-    {
-        var implied = ed.SelectImplied();
-        if (implied.Status != PromptStatus.OK || implied.Value == null) return ObjectId.Null;
-        ed.SetImpliedSelection(new ObjectId[0]);
-        var alignmentClass = RXObject.GetClass(typeof(Alignment));
-        return implied.Value.GetObjectIds().FirstOrDefault(id => id.ObjectClass.IsDerivedFrom(alignmentClass));
-    }
-
     /// <summary>The drawing's pipe networks by name.</summary>
     private static List<(ObjectId id, string name)> ReadNetworks(Document doc)
     {
```


**Step 4: Edit `StakeTableCommand.cs`**

**Modify** `src/C3DTools.Civil2021/Commands/StakeTableCommand.cs`

```diff
--- a/src/C3DTools.Civil2021/Commands/StakeTableCommand.cs
+++ b/src/C3DTools.Civil2021/Commands/StakeTableCommand.cs
@@ -11,6 +11,7 @@ using Autodesk.Civil.ApplicationServices;
 using Autodesk.Civil.DatabaseServices;
 using C3DTools.Civil2021.Curves;
 using C3DTools.Civil2021.Drawing;
+using C3DTools.Civil2021.Services;
 using C3DTools.Civil2021.Ui;
 using C3DTools.Core.Curves;
 using C3DTools.Core.Presets;
@@ -87,7 +88,8 @@ public class StakeTableCommand
         if (lastSurface.Length > 0 && remembered > 0) session.SurfaceIndex = remembered;
 
         Route route = null;
-        var first = PickFirst(ed);
+        var first = RoutePicker.Resolve(doc, out var why);
+        if (why != null) Prompts.Say(ed, why);
         if (!first.IsNull) route = Load(doc, preset, first, session) ?? route;
         if (route != null) Preview(doc, route, session, surfaces);
 
@@ -110,6 +112,7 @@ public class StakeTableCommand
                 case DialogAction.Pick:
                     var picked = Prompts.PickEntity<Alignment>(ed, "Chọn alignment: ");
                     if (picked.IsNull) continue;
+                    RoutePicker.Use(doc, picked);
                     var loaded = Load(doc, preset, picked, session);
                     if (loaded == null) continue;
                     route = loaded;
@@ -130,16 +133,6 @@ public class StakeTableCommand
         }
     }
 
-    /// <summary>The first alignment of the selection made before the command, or ObjectId.Null.</summary>
-    private static ObjectId PickFirst(Editor ed)
-    {
-        var implied = ed.SelectImplied();
-        if (implied.Status != PromptStatus.OK || implied.Value == null) return ObjectId.Null;
-        ed.SetImpliedSelection(new ObjectId[0]);
-        var alignmentClass = RXObject.GetClass(typeof(Alignment));
-        return implied.Value.GetObjectIds().FirstOrDefault(id => id.ObjectClass.IsDerivedFrom(alignmentClass));
-    }
-
     /// <summary>TIN and grid surfaces of the drawing (volume surfaces have no terrain elevation).</summary>
     private static List<(ObjectId id, string name)> ReadSurfaces(Document doc)
     {
```

**Step 5: Build the host**

Run: `dotnet build src/C3DTools.Civil2021 -c Release -v q --nologo`
Expected: `0 Warning(s)` and `0 Error(s)`.

**Step 6: Run the Core suite**

Run: `dotnet test C3DTools.Core.slnf -v q --nologo`
Expected: `Passed!  - Failed: 0`.

**Step 7: Commit**

```bash
git add src/C3DTools.Civil2021/Commands/ActiveRouteCommand.cs src/C3DTools.Civil2021/Commands/CulvertTableCommand.cs src/C3DTools.Civil2021/Commands/StakeTableCommand.cs src/C3DTools.Civil2021/Services/RoutePicker.cs
git commit -m "feat(host): active route — commands stop asking for the alignment"
```

### Task 6: Two-tier dialogs and reset in CTTUYEN, CTPHATCOC, CTDANHCOC

What stays visible; everything else moves into `Advanced(...)` with the same controls and bindings:

| Dialog | Always visible | Nâng cao |
| --- | --- | --- |
| CTTUYEN | Tim tuyến, Tên, Tỉ lệ, Lý trình đầu, Vận tốc, cỡ chữ, Bố trí cong ngay | Mô tả, Kiểu Alignment, Bộ nhãn, Layer, Trắc dọc tự nhiên từ, Tệp mặt cắt, Tải toàn bộ, Mặt cắt cho tuyến |
| CTPHATCOC | Tuyến, Nhóm cọc, Phát sinh (Từ, Tới, Khoảng cách cọc C), Chèn (lý trình, Chỉ điểm…), danh sách cọc | Bề rộng nửa dải, Cọc C bắt đầu từ lý trình, Chêm cong, Cọc C bỏ qua vị trí cọc H, Không tạo cọc H, Tên không dấu, Kiểu cọc phụ, Ghi tên cọc + Lý trình + Xen kẽ |
| CTDANHCOC | Nhóm cọc, Từ cọc, Tới cọc, Tiếp đầu, Số thứ tự cọc đầu, bảng tên cũ → mới | the other 11 options |

The grey paragraph of CTPHATCOC becomes the tooltip of "Phát sinh": it took four lines and is read once. In the command loops the session is built by a local `NewSession()`; on `Reset` the options are cleared **before** anything is saved (or the values just cleared would be written back), the session is rebuilt, and the route or group in use is loaded again. CTDANHCOC also starts from the active route when nothing is selected. The Core test pins the defaults the button promises.

**Files:**
- Modify: `src/C3DTools.Civil2021/Commands/RouteCreateCommand.cs`
- Modify: `src/C3DTools.Civil2021/Commands/StakeCommands.cs`
- Modify: `src/C3DTools.Civil2021/Ui/RouteCreateWindow.cs`
- Modify: `src/C3DTools.Civil2021/Ui/StakeWindows.cs`
- Test (modify): `tests/C3DTools.Core.Tests/Stations/StakeSessionsTests.cs`

**Step 1: Pin the defaults with a test**

This test passes as soon as it is written: it describes behaviour Core already has and guards it from now on.

**Modify** `tests/C3DTools.Core.Tests/Stations/StakeSessionsTests.cs`

```diff
--- a/tests/C3DTools.Core.Tests/Stations/StakeSessionsTests.cs
+++ b/tests/C3DTools.Core.Tests/Stations/StakeSessionsTests.cs
@@ -9,6 +9,42 @@ namespace C3DTools.Core.Tests.Stations;
 
 public class StakeGenerationSessionTests
 {
+    /// <summary>"Về mặc định" rebuilds the session with nothing remembered: these are the values it promises.</summary>
+    [Fact]
+    public void A_new_session_has_the_defaults_reset_promises()
+    {
+        var s = new StakeGenerationSession(new ProjectPreset());
+
+        Assert.Equal("20", s.StraightSpacingText);
+        Assert.Equal("20", s.DetailStartText);
+        Assert.Equal("10", s.CurveSpacingText);
+        Assert.Equal("60", s.HalfWidthText);
+        Assert.False(s.DensifyCurves);
+        Assert.False(s.InsertMode);
+        Assert.False(s.SubStakeStyle);
+        Assert.True(s.SkipHundredPositions);
+        Assert.False(s.NoHundreds);
+        Assert.False(s.PlainCurveNames);
+        Assert.True(s.WriteLabels);
+        Assert.Equal(1, s.StationModeIndex);
+        Assert.False(s.AlternateSides);
+
+        var r = new StakeRenameSession(new ProjectPreset());
+        Assert.Equal("C", r.DetailPrefix);
+        Assert.Equal("1", r.FirstDetailNumberText);
+        Assert.Equal("1", r.FirstPiNumberText);
+        Assert.Equal("", r.KeepPrefixesText);
+        Assert.True(r.RenameCurveKeys);
+        Assert.False(r.NameByStation);
+        Assert.False(r.NoHundreds);
+        Assert.True(r.ContinuousThroughH);
+        Assert.False(r.RestartPerKm);
+        Assert.True(r.NoRestartFrom100);
+        Assert.True(r.SkipHundredPositions);
+        Assert.True(r.WriteLabels);
+        Assert.Equal(1, r.StationModeIndex);
+    }
+
     private static StakeGenerationSession Loaded(params string[] groups)
     {
         var s = new StakeGenerationSession(new ProjectPreset());
```

Run: `dotnet test C3DTools.Core.slnf -v q --nologo --filter A_new_session_has_the_defaults`
Expected: `Passed!  - Failed: 0, Passed: 1`.


**Step 2: Edit `RouteCreateWindow.cs`**

**Modify** `src/C3DTools.Civil2021/Ui/RouteCreateWindow.cs`

```diff
--- a/src/C3DTools.Civil2021/Ui/RouteCreateWindow.cs
+++ b/src/C3DTools.Civil2021/Ui/RouteCreateWindow.cs
@@ -13,6 +13,7 @@ internal sealed class RouteCreateWindow : ToolWindow
 {
     private readonly RouteCreationSession _session;
     private readonly Func<string, IList<string>> _readAssemblies;
+    private readonly Expander _advanced;
     private readonly TextBlock _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.DarkRed };
 
     /// <param name="readAssemblies">Assembly names of a DWG ("Tệp mặt cắt"); throws with a Vietnamese message.</param>
@@ -28,30 +29,32 @@ internal sealed class RouteCreateWindow : ToolWindow
         top.Children.Add(ActionButton("Theo polyline…", DialogAction.Pick));
         top.Children.Add(ActionButton("Chỉ điểm…", DialogAction.PickPoints));
 
-        var form = new Grid { Margin = new Thickness(0, 8, 0, 0) };
-        form.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
-        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
-        var row = 0;
-        void Add(string label, UIElement input)
+        // Two forms with the same label column: what every route needs, and what usually keeps its default.
+        var form = NewForm();
+        var more = NewForm();
+        void AddTo(Grid grid, string label, UIElement input)
         {
-            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
+            var row = grid.RowDefinitions.Count;
+            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
             var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 10, 3) };
             Grid.SetRow(text, row);
-            form.Children.Add(text);
+            grid.Children.Add(text);
             if (input is FrameworkElement f) f.Margin = new Thickness(0, 3, 0, 3);
             Grid.SetRow(input, row);
             Grid.SetColumn(input, 1);
-            form.Children.Add(input);
-            row++;
+            grid.Children.Add(input);
         }
 
+        void Add(string label, UIElement input) => AddTo(form, label, input);
+        void AddMore(string label, UIElement input) => AddTo(more, label, input);
+
         var nameAndScale = Row();
         nameAndScale.Margin = new Thickness(0);
         nameAndScale.Children.Add(Text(nameof(RouteCreationSession.Name), nameof(RouteCreationSession.IsNameValid), 200));
         nameAndScale.Children.Add(Label("Tỉ lệ bình đồ 1/"));
         nameAndScale.Children.Add(Text(nameof(RouteCreationSession.ScaleText), nameof(RouteCreationSession.IsScaleValid), 70));
         Add("Tên đường tuyến", nameAndScale);
-        Add("Mô tả", Text(nameof(RouteCreationSession.Description), null, 0));
+        AddMore("Mô tả", Text(nameof(RouteCreationSession.Description), null, 0));
 
         var startAndSpeed = Row();
         startAndSpeed.Margin = new Thickness(0);
@@ -63,10 +66,10 @@ internal sealed class RouteCreateWindow : ToolWindow
         startAndSpeed.Children.Add(new TextBlock { Text = "km/h", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
         Add("Lý trình đầu", startAndSpeed);
 
-        Add("Kiểu Alignment", Combo(nameof(RouteCreationSession.StyleNames), nameof(RouteCreationSession.StyleIndex)));
-        Add("Bộ nhãn", Combo(nameof(RouteCreationSession.LabelSetNames), nameof(RouteCreationSession.LabelSetIndex)));
-        Add("Layer", Text(nameof(RouteCreationSession.LayerName), nameof(RouteCreationSession.IsLayerValid), 200));
-        Add("Trắc dọc tự nhiên từ", Combo(nameof(RouteCreationSession.SurfaceNames), nameof(RouteCreationSession.SurfaceIndex)));
+        AddMore("Kiểu Alignment", Combo(nameof(RouteCreationSession.StyleNames), nameof(RouteCreationSession.StyleIndex)));
+        AddMore("Bộ nhãn", Combo(nameof(RouteCreationSession.LabelSetNames), nameof(RouteCreationSession.LabelSetIndex)));
+        AddMore("Layer", Text(nameof(RouteCreationSession.LayerName), nameof(RouteCreationSession.IsLayerValid), 200));
+        AddMore("Trắc dọc tự nhiên từ", Combo(nameof(RouteCreationSession.SurfaceNames), nameof(RouteCreationSession.SurfaceIndex)));
 
         var file = new DockPanel();
         var browse = Button("…", (s, e) => Browse());
@@ -77,18 +80,31 @@ internal sealed class RouteCreateWindow : ToolWindow
         var path = new TextBox { IsReadOnly = true, Background = ReadOnlyBrush, VerticalContentAlignment = VerticalAlignment.Center };
         path.SetBinding(TextBox.TextProperty, new Binding(nameof(RouteCreationSession.SectionFile)) { Mode = BindingMode.OneWay });
         file.Children.Add(path);
-        Add("Tệp mặt cắt (DWG)", file);
-        Add("", Check("Tải toàn bộ mặt cắt trong tệp", nameof(RouteCreationSession.LoadAllAssemblies)));
-        Add("Mặt cắt cho tuyến", Combo(nameof(RouteCreationSession.AssemblyNames), nameof(RouteCreationSession.AssemblyIndex)));
+        AddMore("Tệp mặt cắt (DWG)", file);
+        AddMore("", Check("Tải toàn bộ mặt cắt trong tệp", nameof(RouteCreationSession.LoadAllAssemblies)));
+        AddMore("Mặt cắt cho tuyến", Combo(nameof(RouteCreationSession.AssemblyNames), nameof(RouteCreationSession.AssemblyIndex)));
 
         var height = new TextBlock { Foreground = System.Windows.Media.Brushes.DimGray };
         height.SetBinding(TextBlock.TextProperty, new Binding(nameof(RouteCreationSession.TextHeightText)));
         Add("", height);
         Add("", Check("Bố trí cong ngay sau khi tạo (mở CTYTC)", nameof(RouteCreationSession.OpenCurveDesign)));
-        Add("", _status);
+        AddMore("", _status);
+
+        var body = new StackPanel();
+        body.Children.Add(form);
+        var advanced = Advanced(more);
+        body.Children.Add(advanced);
+        var content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
+        SetLayout(top, BuildFooter(nameof(RouteCreationSession.SummaryText), nameof(RouteCreationSession.CanApply), withReset: true), content);
+        _advanced = advanced;
+    }
 
-        var content = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
-        SetLayout(top, BuildFooter(nameof(RouteCreationSession.SummaryText), nameof(RouteCreationSession.CanApply)), content);
+    private static Grid NewForm()
+    {
+        var grid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
+        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
+        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
+        return grid;
     }
 
     private void Browse()
@@ -104,6 +120,7 @@ internal sealed class RouteCreateWindow : ToolWindow
         catch (Exception ex)
         {
             _status.Text = "Không đọc được tệp mặt cắt: " + ex.Message;
+            _advanced.IsExpanded = true;   // the message is inside Nâng cao
         }
     }
```


**Step 3: Edit `StakeWindows.cs`**

**Modify** `src/C3DTools.Civil2021/Ui/StakeWindows.cs`

```diff
--- a/src/C3DTools.Civil2021/Ui/StakeWindows.cs
+++ b/src/C3DTools.Civil2021/Ui/StakeWindows.cs
@@ -31,9 +31,10 @@ internal sealed class StakeGenerateWindow : ToolWindow
         var width = Row();
         width.Children.Add(Caption("Bề rộng nửa dải xác định trắc ngang (m)", 260));
         width.Children.Add(StakeInputs.Text(nameof(StakeGenerationSession.HalfWidthText), nameof(StakeGenerationSession.IsHalfWidthValid), 70));
-        top.Children.Add(width);
 
         var generate = new RadioButton { Content = "Phát sinh", GroupName = "Mode", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2) };
+        generate.ToolTip = "Cọc Km mỗi 1000 m, cọc H mỗi 100 m (H1–H9, lặp lại sau mỗi Km), cọc C theo khoảng cách, chạy và đánh số liên tục tới hết tuyến, " +
+                           "qua cả đường cong; cọc C, H không đặt trùng cọc Km; cọc đặc biệt tại mỗi đường cong: TĐ, P, TC (thêm NĐ, NC nếu có chuyển tiếp).";
         generate.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(StakeGenerationSession.GenerateMode)) { Mode = BindingMode.TwoWay });
         top.Children.Add(generate);
         var generateBox = new StackPanel { Margin = new Thickness(20, 0, 0, 0) };
@@ -48,26 +49,26 @@ internal sealed class StakeGenerateWindow : ToolWindow
         spacing.Children.Add(choice);
         generateBox.Children.Add(spacing);
         var start = Row();
-        start.Children.Add(Caption("Cọc C bắt đầu từ lý trình", 190));
+        start.SetBinding(IsEnabledProperty, new Binding(nameof(StakeGenerationSession.GenerateMode)));
+        start.Children.Add(Caption("Cọc C bắt đầu từ lý trình", 210));
         var startBox = StakeInputs.Text(nameof(StakeGenerationSession.DetailStartText), nameof(StakeGenerationSession.IsDetailStartValid), 130);
         startBox.ToolTip = "Để trống: theo khoảng cách (20 m → Km0+020, 100 m → Km0+050)";
         start.Children.Add(startBox);
-        generateBox.Children.Add(start);
         var curves = Row();
+        curves.SetBinding(IsEnabledProperty, new Binding(nameof(StakeGenerationSession.GenerateMode)));
         var densify = Check("Chêm thêm cọc trong đoạn cong, khoảng cách (m)", nameof(StakeGenerationSession.DensifyCurves));
         densify.ToolTip = "Thêm cọc ở các lý trình chẵn theo khoảng cách này giữa NĐ và NC, ngoài các cọc C";
         curves.Children.Add(densify);
         var curveBox = StakeInputs.Text(nameof(StakeGenerationSession.CurveSpacingText), nameof(StakeGenerationSession.IsCurveSpacingValid), 70);
         curveBox.SetBinding(IsEnabledProperty, new Binding(nameof(StakeGenerationSession.DensifyCurves)));
         curves.Children.Add(curveBox);
-        generateBox.Children.Add(curves);
         var hundreds = Row();
+        hundreds.SetBinding(IsEnabledProperty, new Binding(nameof(StakeGenerationSession.GenerateMode)));
         hundreds.Children.Add(StakeInputs.SkipHundreds(nameof(StakeGenerationSession.SkipHundredPositions)));
         var noH = Check("Không tạo cọc H", nameof(StakeGenerationSession.NoHundreds));
         noH.ToolTip = "Cọc tại lý trình chẵn trăm cũng là cọc C: C4 (80), C5 (100), C6 (120)";
         hundreds.Children.Add(noH);
         hundreds.Children.Add(StakeInputs.PlainCurveNames(nameof(StakeGenerationSession.PlainCurveNames)));
-        generateBox.Children.Add(hundreds);
         top.Children.Add(generateBox);
 
         var insert = new RadioButton { Content = "Chèn", GroupName = "Mode", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2) };
@@ -83,25 +84,19 @@ internal sealed class StakeGenerateWindow : ToolWindow
         stations.Children.Add(new Border { Width = 8 });
         stations.Children.Add(ActionButton("Chỉ điểm…", DialogAction.PickPoints));
         insertBox.Children.Add(stations);
-        insertBox.Children.Add(Check("Kiểu cọc phụ (đặt tên theo cọc trước: C5a, C5b)", nameof(StakeGenerationSession.SubStakeStyle)));
         top.Children.Add(insertBox);
 
-        top.Children.Add(StakeInputs.LabelOptions(nameof(StakeGenerationSession.WriteLabels), nameof(StakeGenerationSession.AlternateSides),
-            nameof(StakeGenerationSession.StationModeIndex)));
-        top.Children.Add(new TextBlock
-        {
-            Text = "Phát sinh: cọc Km mỗi 1000 m, cọc H mỗi 100 m (H1–H9, lặp lại sau mỗi Km), cọc C theo khoảng cách từ lý trình bắt đầu, " +
-                   "chạy và đánh số liên tục tới hết tuyến, qua cả đường cong; cọc C, H không đặt trùng cọc Km; cọc đặc biệt tại mỗi đường cong: TĐ, P, TC (thêm NĐ, NC nếu có chuyển tiếp), đánh số theo thứ tự đường cong dọc tuyến. " +
-                   "Dùng CTDANHCOC để đặt tên theo quy tắc khác.",
-            TextWrapping = TextWrapping.Wrap,
-            Foreground = System.Windows.Media.Brushes.DimGray,
-            Margin = new Thickness(0, 6, 0, 6),
-        });
+        var subStake = Check("Chèn: kiểu cọc phụ (đặt tên theo cọc trước: C5a, C5b)", nameof(StakeGenerationSession.SubStakeStyle));
+        subStake.Margin = new Thickness(0, 2, 0, 2);
+        top.Children.Add(Advanced(
+            width, start, curves, hundreds, subStake,
+            StakeInputs.LabelOptions(nameof(StakeGenerationSession.WriteLabels), nameof(StakeGenerationSession.AlternateSides),
+                nameof(StakeGenerationSession.StationModeIndex))));
 
         var grid = StakeInputs.Grid(nameof(StakeGenerationSession.PreviewRows),
             ("Tên cọc", nameof(StakePreviewLine.Name), 110), ("Lý trình", nameof(StakePreviewLine.Station), 110),
             ("Loại", nameof(StakePreviewLine.Kind), 90), ("Ghi chú", nameof(StakePreviewLine.Status), 0));
-        SetLayout(top, BuildFooter(nameof(StakeGenerationSession.SummaryText), nameof(StakeGenerationSession.CanApply)), grid);
+        SetLayout(top, BuildFooter(nameof(StakeGenerationSession.SummaryText), nameof(StakeGenerationSession.CanApply), withReset: true), grid);
     }
 
     private StackPanel Range(string caption, string path, DialogAction pick)
@@ -141,42 +136,30 @@ internal sealed class StakeRenameWindow : ToolWindow
         var top = new StackPanel();
         top.Children.Add(BuildHeader("Nhóm cọc: " + session.SourceText));
 
-        var form = new Grid { Margin = new Thickness(0, 6, 0, 6) };
-        for (var c = 0; c < 4; c++) form.ColumnDefinitions.Add(new ColumnDefinition { Width = c % 2 == 0 ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });
-        var row = 0;
-        void Line(UIElement a, UIElement b = null, UIElement c = null, UIElement d = null)
-        {
-            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
-            var cells = new[] { a, b, c, d };
-            for (var i = 0; i < cells.Length; i++)
-            {
-                if (cells[i] == null) continue;
-                if (cells[i] is FrameworkElement f) f.Margin = new Thickness(i % 2 == 0 ? 0 : 4, 3, 12, 3);
-                Grid.SetRow(cells[i], row);
-                Grid.SetColumn(cells[i], i);
-                form.Children.Add(cells[i]);
-            }
-
-            row++;
-        }
+        // Always visible: the range and how C stakes are named.
+        var form = new Form();
+        form.Line(Text("Từ cọc"), Choice(nameof(StakeRenameSession.FromIndex)),
+            Text("Tiếp đầu của cọc"), StakeInputs.Text(nameof(StakeRenameSession.DetailPrefix), null, 60));
+        form.Line(Text("Tới cọc"), Choice(nameof(StakeRenameSession.ToIndex)),
+            Text("Số thứ tự cọc đầu"), StakeInputs.Text(nameof(StakeRenameSession.FirstDetailNumberText), nameof(StakeRenameSession.IsFirstDetailValid), 60));
+        top.Children.Add(form.Grid);
 
-        Line(Text("Từ cọc"), Choice(nameof(StakeRenameSession.FromIndex)));
-        Line(Text("Tới cọc"), Choice(nameof(StakeRenameSession.ToIndex)));
+        var more = new Form();
         var keep = StakeInputs.Text(nameof(StakeRenameSession.KeepPrefixesText), null, 0);
         keep.ToolTip = "Các tiếp đầu cách nhau bởi dấu ; ví dụ: CT; CONG";
-        Line(Text("Để lại các cọc có tiếp đầu"), keep);
-        Line(Check("Đánh lại cọc cắm cong, siêu cao", nameof(StakeRenameSession.RenameCurveKeys)), null,
+        more.Line(Text("Để lại các cọc có tiếp đầu"), keep);
+        more.Line(Check("Đánh lại cọc cắm cong, siêu cao", nameof(StakeRenameSession.RenameCurveKeys)), null,
             Text("Số thứ tự đỉnh đầu"), StakeInputs.Text(nameof(StakeRenameSession.FirstPiNumberText), nameof(StakeRenameSession.IsFirstPiValid), 60));
-        Line(Check("Tên cọc theo kiểu lý trình", nameof(StakeRenameSession.NameByStation)), null,
-            Text("Tiếp đầu của cọc"), StakeInputs.Text(nameof(StakeRenameSession.DetailPrefix), null, 60));
-        Line(Text("Số thứ tự cọc đầu"), StakeInputs.Text(nameof(StakeRenameSession.FirstDetailNumberText), nameof(StakeRenameSession.IsFirstDetailValid), 60));
-        Line(Check("Không tạo cọc H", nameof(StakeRenameSession.NoHundreds)), null, Check("Cọc H liên tục", nameof(StakeRenameSession.ContinuousThroughH)));
-        Line(StakeInputs.SkipHundreds(nameof(StakeRenameSession.SkipHundredPositions)), null, StakeInputs.PlainCurveNames(nameof(StakeRenameSession.PlainCurveNames)));
-        Line(Check("Thứ tự cọc quay lại theo KM", nameof(StakeRenameSession.RestartPerKm)));
-        Line(Check("Không đánh số quay lại khi TT>=100", nameof(StakeRenameSession.NoRestartFrom100)));
-        top.Children.Add(form);
-        top.Children.Add(StakeInputs.LabelOptions(nameof(StakeRenameSession.WriteLabels), nameof(StakeRenameSession.AlternateSides),
-            nameof(StakeRenameSession.StationModeIndex)));
+        more.Line(Check("Tên cọc theo kiểu lý trình", nameof(StakeRenameSession.NameByStation)), null,
+            StakeInputs.PlainCurveNames(nameof(StakeRenameSession.PlainCurveNames)));
+        more.Line(Check("Không tạo cọc H", nameof(StakeRenameSession.NoHundreds)), null, Check("Cọc H liên tục", nameof(StakeRenameSession.ContinuousThroughH)));
+        more.Line(StakeInputs.SkipHundreds(nameof(StakeRenameSession.SkipHundredPositions)));
+        more.Line(Check("Thứ tự cọc quay lại theo KM", nameof(StakeRenameSession.RestartPerKm)), null,
+            Check("Không đánh số quay lại khi TT>=100", nameof(StakeRenameSession.NoRestartFrom100)));
+        top.Children.Add(Advanced(
+            more.Grid,
+            StakeInputs.LabelOptions(nameof(StakeRenameSession.WriteLabels), nameof(StakeRenameSession.AlternateSides),
+                nameof(StakeRenameSession.StationModeIndex))));
 
         var grid = StakeInputs.Grid(nameof(StakeRenameSession.PreviewRows),
             ("Lý trình", nameof(StakeRenameLine.Station), 120), ("Tên cũ", nameof(StakeRenameLine.OldName), 130), ("Tên mới", nameof(StakeRenameLine.NewName), 0));
@@ -185,7 +168,38 @@ internal sealed class StakeRenameWindow : ToolWindow
         trigger.Setters.Add(new Setter(BackgroundProperty, WarningBrush));
         changed.Triggers.Add(trigger);
         grid.RowStyle = changed;
-        SetLayout(top, BuildFooter(nameof(StakeRenameSession.SummaryText), nameof(StakeRenameSession.CanApply)), grid);
+        SetLayout(top, BuildFooter(nameof(StakeRenameSession.SummaryText), nameof(StakeRenameSession.CanApply), withReset: true), grid);
+    }
+
+    /// <summary>A four-column grid: label, input, label, input.</summary>
+    private sealed class Form
+    {
+        private int _row;
+
+        public Form()
+        {
+            Grid = new Grid { Margin = new Thickness(0, 6, 0, 6) };
+            for (var c = 0; c < 4; c++)
+                Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = c % 2 == 0 ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });
+        }
+
+        public Grid Grid { get; }
+
+        public void Line(UIElement a, UIElement b = null, UIElement c = null, UIElement d = null)
+        {
+            Grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
+            var cells = new[] { a, b, c, d };
+            for (var i = 0; i < cells.Length; i++)
+            {
+                if (cells[i] == null) continue;
+                if (cells[i] is FrameworkElement f) f.Margin = new Thickness(i % 2 == 0 ? 0 : 4, 3, 12, 3);
+                System.Windows.Controls.Grid.SetRow(cells[i], _row);
+                System.Windows.Controls.Grid.SetColumn(cells[i], i);
+                Grid.Children.Add(cells[i]);
+            }
+
+            _row++;
+        }
     }
 
     private static TextBlock Text(string text) => new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
@@ -202,7 +216,6 @@ internal sealed class StakeRenameWindow : ToolWindow
 /// <summary>Inputs shared by the stake dialogs.</summary>
 internal static class StakeInputs
 {
-    /// <summary>"Ghi tên cọc lên bình đồ" with its two options, which are enabled only while it is ticked.</summary>
     /// <summary>"Cọc C bỏ qua vị trí cọc H", with what each state gives in the tooltip.</summary>
     public static CheckBox SkipHundreds(string path)
     {
@@ -219,6 +232,7 @@ internal static class StakeInputs
         return box;
     }
 
+    /// <summary>"Ghi tên cọc lên bình đồ" with its station choice and the alternate option, enabled only while it is ticked.</summary>
     public static StackPanel LabelOptions(string writePath, string alternatePath, string stationModePath)
     {
         var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 2) };
```


**Step 4: Edit `RouteCreateCommand.cs`**

**Modify** `src/C3DTools.Civil2021/Commands/RouteCreateCommand.cs`

```diff
--- a/src/C3DTools.Civil2021/Commands/RouteCreateCommand.cs
+++ b/src/C3DTools.Civil2021/Commands/RouteCreateCommand.cs
@@ -51,15 +51,20 @@ public class RouteCreateCommand
         foreach (var m in messages) Prompts.Say(ed, m);
 
         var memory = ToolWindow.Options;
-        var session = new RouteCreationSession(preset)
+        RouteCreationSession NewSession()
         {
-            ScaleText = memory.Get(Command, "Scale", "1000"),
-            LayerName = memory.Get(Command, "Layer", "TUYEN"),
-            OpenCurveDesign = memory.Get(Command, "OpenCurveDesign", true),
-            LoadAllAssemblies = memory.Get(Command, "LoadAllAssemblies", true),
-        };
-        ReadDrawing(doc, session);
+            var fresh = new RouteCreationSession(preset)
+            {
+                ScaleText = memory.Get(Command, "Scale", "1000"),
+                LayerName = memory.Get(Command, "Layer", "TUYEN"),
+                OpenCurveDesign = memory.Get(Command, "OpenCurveDesign", true),
+                LoadAllAssemblies = memory.Get(Command, "LoadAllAssemblies", true),
+            };
+            ReadDrawing(doc, fresh);
+            return fresh;
+        }
 
+        var session = NewSession();
         var polylineId = PickFirst(ed);
         if (!polylineId.IsNull) session.SetPolylineSource(Describe(doc, polylineId));
 
@@ -67,6 +72,17 @@ public class RouteCreateCommand
         {
             var window = new RouteCreateWindow(session, ReadAssemblies);
             var action = window.ShowModal();
+            if (action == DialogAction.Reset)
+            {
+                // Not saved first: the values being reset must not be written back. The centreline stays.
+                ToolWindow.ResetOptions(Command);
+                var points = session.PickedPoints;
+                session = NewSession();
+                if (!polylineId.IsNull) session.SetPolylineSource(Describe(doc, polylineId));
+                else if (points.Count >= 2) session.SetPickedPoints(points);
+                continue;
+            }
+
             memory.Set(Command, "Scale", session.ScaleText);
             memory.Set(Command, "Layer", session.LayerName);
             memory.Set(Command, "OpenCurveDesign", session.OpenCurveDesign);
@@ -82,16 +98,17 @@ public class RouteCreateCommand
                     session.SetPolylineSource(Describe(doc, picked));
                     continue;
                 case DialogAction.PickPoints:
-                    var points = Prompts.PickPoints(ed, "Điểm đầu tuyến: ", "Đỉnh tiếp theo [Lui] <Enter: kết thúc>: ");
-                    if (points == null || points.Count < 2) continue;
+                    var clicked = Prompts.PickPoints(ed, "Điểm đầu tuyến: ", "Đỉnh tiếp theo [Lui] <Enter: kết thúc>: ");
+                    if (clicked == null || clicked.Count < 2) continue;
                     polylineId = ObjectId.Null;
-                    session.SetPickedPoints(points.Select(p => new PlanPoint(p.X, p.Y)));
+                    session.SetPickedPoints(clicked.Select(p => new PlanPoint(p.X, p.Y)));
                     continue;
                 case DialogAction.Preview:
                 case DialogAction.Apply:
                     if (!session.CanApply) continue;
                     var created = Write(doc, session, polylineId, askToKeep: action == DialogAction.Preview);
                     if (created.IsNull) continue;   // not kept or failed: back to the dialog
+                    RoutePicker.Use(doc, created);   // the new route is the one the next commands work on
                     if (session.OpenCurveDesign)
                     {
                         // Runs after this command returns; the implied selection survives until then and CTYTC's PickFirst takes it.
```


**Step 5: Edit `StakeCommands.cs`**

**Modify** `src/C3DTools.Civil2021/Commands/StakeCommands.cs`

```diff
--- a/src/C3DTools.Civil2021/Commands/StakeCommands.cs
+++ b/src/C3DTools.Civil2021/Commands/StakeCommands.cs
@@ -71,7 +71,7 @@ public class StakeCommands
         var ed = doc.Editor;
         var preset = LoadPreset(ed);
         var memory = ToolWindow.Options;
-        var session = new StakeGenerationSession(preset)
+        StakeGenerationSession NewSession() => new StakeGenerationSession(preset)
         {
             StraightSpacingText = memory.Get(command, "Straight", "20"),
             CurveSpacingText = memory.Get(command, "Curve", "10"),
@@ -86,13 +86,24 @@ public class StakeCommands
             PlainCurveNames = memory.Get(command, "PlainCurveNames", false),
         };
 
+        var session = NewSession();
         Route route = null;
-        var first = PickFirst<Alignment>(ed);
+        var first = RoutePicker.Resolve(doc, out var why);
+        if (why != null) Prompts.Say(ed, why);
         if (!first.IsNull) route = LoadRoute(doc, preset, first, session);
 
         while (true)
         {
             var action = new StakeGenerateWindow(session).ShowModal();
+            if (action == DialogAction.Reset)
+            {
+                // Not saved first: the values being reset must not be written back.
+                ToolWindow.ResetOptions(command);
+                session = NewSession();
+                if (route != null) route = LoadRoute(doc, preset, route.Id, session) ?? route;
+                continue;
+            }
+
             memory.Set(command, "Straight", session.StraightSpacingText);
             memory.Set(command, "Curve", session.CurveSpacingText);
             memory.Set(command, "Densify", session.DensifyCurves);
@@ -110,7 +121,9 @@ public class StakeCommands
             {
                 case DialogAction.Pick:
                     var picked = Prompts.PickEntity<Alignment>(ed, "Chọn alignment: ");
-                    if (!picked.IsNull) route = LoadRoute(doc, preset, picked, session) ?? route;
+                    if (picked.IsNull) continue;
+                    RoutePicker.Use(doc, picked);
+                    route = LoadRoute(doc, preset, picked, session) ?? route;
                     continue;
                 case DialogAction.PickFrom:
                 case DialogAction.PickTo:
@@ -300,7 +313,7 @@ public class StakeCommands
         var ed = doc.Editor;
         var preset = LoadPreset(ed);
         var memory = ToolWindow.Options;
-        var session = new StakeRenameSession(preset)
+        StakeRenameSession NewSession() => new StakeRenameSession(preset)
         {
             DetailPrefix = memory.Get(command, "Prefix", "C"),
             KeepPrefixesText = memory.Get(command, "Keep", ""),
@@ -317,13 +330,29 @@ public class StakeCommands
             PlainCurveNames = memory.Get(command, "PlainCurveNames", false),
         };
 
+        var session = NewSession();
         StakeGroup ids = null;
-        var first = PickFirst<Autodesk.AutoCAD.DatabaseServices.Entity>(ed);
-        if (!first.IsNull) ids = LoadGroup(doc, preset, first, session) ?? ids;
+        // A sample line or alignment selected before the command; else the active route.
+        var source = PickFirst<Autodesk.AutoCAD.DatabaseServices.Entity>(ed);
+        if (source.IsNull)
+        {
+            source = RoutePicker.Resolve(doc, out var why);
+            if (why != null) Prompts.Say(ed, why);
+        }
+
+        if (!source.IsNull) ids = LoadGroup(doc, preset, source, session) ?? ids;
 
         while (true)
         {
             var action = new StakeRenameWindow(session).ShowModal();
+            if (action == DialogAction.Reset)
+            {
+                ToolWindow.ResetOptions(command);
+                session = NewSession();
+                if (ids != null) ids = LoadGroup(doc, preset, source, session) ?? ids;
+                continue;
+            }
+
             memory.Set(command, "Prefix", session.DetailPrefix);
             memory.Set(command, "Keep", session.KeepPrefixesText);
             memory.Set(command, "CurveKeys", session.RenameCurveKeys);
@@ -343,7 +372,12 @@ public class StakeCommands
             {
                 case DialogAction.Pick:
                     var picked = PickGroupObject(ed);
-                    if (!picked.IsNull) ids = LoadGroup(doc, preset, picked, session) ?? ids;
+                    if (picked.IsNull) continue;
+                    var loaded = LoadGroup(doc, preset, picked, session);
+                    if (loaded == null) continue;
+                    ids = loaded;
+                    source = picked;
+                    RoutePicker.Use(doc, ids.AlignmentId);
                     continue;
                 case DialogAction.Preview:
                 case DialogAction.Apply:
```

**Step 6: Build the host**

Run: `dotnet build src/C3DTools.Civil2021 -c Release -v q --nologo`
Expected: `0 Warning(s)` and `0 Error(s)`.

**Step 7: Run the Core suite**

Run: `dotnet test C3DTools.Core.slnf -v q --nologo`
Expected: `Passed!  - Failed: 0`.

**Step 8: Commit**

```bash
git add src/C3DTools.Civil2021/Commands/RouteCreateCommand.cs src/C3DTools.Civil2021/Commands/StakeCommands.cs src/C3DTools.Civil2021/Ui/RouteCreateWindow.cs src/C3DTools.Civil2021/Ui/StakeWindows.cs tests/C3DTools.Core.Tests/Stations/StakeSessionsTests.cs
git commit -m "feat(host): fewer fields first and a reset button in CTTUYEN, CTPHATCOC, CTDANHCOC"
```

### Task 7: CTBAOLOI

Writes `C3DTools-baoloi-<yyyyMMdd-HHmmss>.zip` to the Desktop (the temp folder if there is no Desktop) and shows it in Explorer. Every fact is read on its own, so one that fails says so instead of stopping the report. The drawing is not packed and nothing is sent; the command line says both.

**Files:**
- Create: `src/C3DTools.Civil2021/Commands/SupportCommand.cs`

**Step 1: Create `SupportCommand.cs`**

**Create** `src/C3DTools.Civil2021/Commands/SupportCommand.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Ui;
using C3DTools.Core.Ui;
using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.SupportCommand))]

namespace C3DTools.Civil2021.Commands;

public class SupportCommand
{
    /// <summary>
    /// CTBAOLOI: packs the logs, the remembered options and facts about the machine and the drawing into a zip on the
    /// Desktop. The drawing itself is not included and nothing is sent anywhere.
    /// </summary>
    [CommandMethod("C3DTOOLS", "CTBAOLOI", CommandFlags.Modal)]
    public void Report()
    {
        var doc = AcCoreApp.DocumentManager.MdiActiveDocument;
        var ed = doc?.Editor;
        try
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) folder = Path.GetTempPath();
            var zip = Path.Combine(folder, SupportBundle.FileName(DateTime.Now));
            var written = SupportBundle.Write(zip, Facts(doc),
                new[] { ToolWindow.TraceLogPath, ToolWindow.ErrorLogPath, ToolWindow.OptionsPath });

            Prompts.Say(ed, $"Đã tạo {zip} ({written.Count} tệp).");
            Prompts.Say(ed, "Tệp này không chứa bản vẽ và chưa được gửi đi đâu. Hãy gửi nó kèm mô tả bước đang làm khi gặp lỗi.\n");
            try
            {
                Process.Start("explorer.exe", "/select,\"" + zip + "\"");
            }
            catch (System.Exception)
            {
                // Showing the file is a convenience; its path is on the command line.
            }
        }
        catch (System.Exception ex)
        {
            ToolWindow.LogError("CTBAOLOI", ex);
            Prompts.Say(ed, $"Không tạo được tệp báo lỗi: {ex.Message}");
        }
    }

    /// <summary>One fact per line; a fact that cannot be read says so instead of stopping the report.</summary>
    private static List<string> Facts(Autodesk.AutoCAD.ApplicationServices.Document doc)
    {
        var lines = new List<string>();
        void Add(string name, Func<object> value)
        {
            try
            {
                lines.Add(name + ": " + Convert.ToString(value(), CultureInfo.InvariantCulture));
            }
            catch (System.Exception ex)
            {
                lines.Add(name + ": không đọc được (" + ex.Message + ")");
            }
        }

        Add("Thời điểm", () => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        Add("C3DTools", () => typeof(SupportCommand).Assembly.GetName().Version);
        Add("AutoCAD", () => AcCoreApp.Version);
        Add("AutoCAD _VERNUM", () => AcCoreApp.GetSystemVariable("_VERNUM"));
        Add("Civil 3D (AeccDbMgd)", () => typeof(Alignment).Assembly.GetName().Version);
        Add("Windows", () => Environment.OSVersion.VersionString);
        Add("64-bit", () => Environment.Is64BitProcess);
        Add("Ngôn ngữ", () => CultureInfo.CurrentCulture.Name + ", dấu thập phân '" + CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator + "'");
        Add("Màn hình", () => System.Windows.SystemParameters.PrimaryScreenWidth.ToString("0", CultureInfo.InvariantCulture)
                              + " x " + System.Windows.SystemParameters.PrimaryScreenHeight.ToString("0", CultureInfo.InvariantCulture) + " (đơn vị WPF)");
        if (doc == null) return lines;

        Add("Bản vẽ", () => AcCoreApp.GetSystemVariable("DWGNAME"));
        Add("Bản vẽ đã lưu", () => Convert.ToInt32(AcCoreApp.GetSystemVariable("DWGTITLED"), CultureInfo.InvariantCulture) == 1);
        Add("INSUNITS", () => AcCoreApp.GetSystemVariable("INSUNITS"));
        using (var tr = doc.TransactionManager.StartTransaction())
        {
            var civil = CivilDocument.GetCivilDocument(doc.Database);
            Add("Số alignment", () => civil.GetAlignmentIds().Count);
            Add("Số mặt phủ", () => civil.GetSurfaceIds().Count);
            Add("Số assembly", () => civil.AssemblyCollection.Count);
            Add("Số nhóm cọc", () =>
            {
                var groups = 0;
                foreach (ObjectId id in civil.GetAlignmentIds())
                    groups += ((Alignment)tr.GetObject(id, OpenMode.ForRead)).GetSampleLineGroupIds().Count;
                return groups;
            });
            Add("Tuyến hiện hành", () => Services.ActiveRouteStore.Read(tr, doc.Database) ?? "(chưa có)");
            tr.Commit();
        }

        return lines;
    }
}
```

**Step 2: Build the host**

Run: `dotnet build src/C3DTools.Civil2021 -c Release -v q --nologo`
Expected: `0 Warning(s)` and `0 Error(s)`.

**Step 3: Run the Core suite**

Run: `dotnet test C3DTools.Core.slnf -v q --nologo`
Expected: `Passed!  - Failed: 0`.

**Step 4: Commit**

```bash
git add src/C3DTools.Civil2021/Commands/SupportCommand.cs
git commit -m "feat(host): CTBAOLOI packs logs and version for a bug report"
```

### Task 8: Ribbon by stage

Left to right in the order of the work; one large button and at most three small ones per panel, so no panel is taller than the ribbon.

| Panel | Large | Small |
| --- | --- | --- |
| Tuyến | Tạo tuyến | Yếu tố cong, Bảng cong, Tuyến hiện hành |
| Cọc | Phát sinh cọc | Đánh tên cọc, Toạ độ cọc |
| Trắc dọc | Bảng trắc dọc | Cong đứng |
| Trắc ngang | Bảng trắc ngang | Xếp trang |
| Địa hình | Mặt địa hình | VN-2000, Bảng cống |
| Tiện ích | Gửi báo lỗi | Chuyển font, Chuẩn layer, Mẫu TCVN |

"Khối lượng" and "Bản in" panels come with their commands in đợt 4. Two new icon pairs, `coc` and `tienich`, are drawn by the script below (Pillow), four times oversized then reduced, like the six existing pairs.

**Files:**
- Create: `src/C3DTools.Civil2021/Resources/coc16.png`
- Create: `src/C3DTools.Civil2021/Resources/coc32.png`
- Create: `src/C3DTools.Civil2021/Resources/tienich16.png`
- Create: `src/C3DTools.Civil2021/Resources/tienich32.png`
- Modify: `src/C3DTools.Civil2021/Ui/RibbonSetup.cs`

**Step 1: Draw the icons**

Save as `make_icons.py` in the repo root, run it, then delete it.

```python
# scripts run once from the repo root: python3 make_icons.py   (needs: pip install pillow)
import math
from PIL import Image, ImageDraw

def coc(n):
    img = Image.new('RGBA', (n, n), (0, 0, 0, 0)); d = ImageDraw.Draw(img); k = n / 16
    d.line([(1*k, 12*k), (15*k, 6*k)], fill=(200, 60, 60, 255), width=max(1, round(1.4*k)))      # the route
    for x in (4, 8, 12):                                                                          # three stakes across it
        y = 12 - (x - 1) * 6 / 14
        d.line([(x*k - 1.6*k, (y - 4.2)*k), (x*k + 1.6*k, (y + 4.2)*k)], fill=(60, 140, 220, 255), width=max(1, round(1.2*k)))
    return img

def tienich(n):
    img = Image.new('RGBA', (n, n), (0, 0, 0, 0)); d = ImageDraw.Draw(img); k = n / 16; c = (8*k, 8*k)
    for i in range(8):                                                                            # a gear
        a = i * math.pi / 4
        d.line([(c[0] + 3.5*k*math.cos(a), c[1] + 3.5*k*math.sin(a)), (c[0] + 6.5*k*math.cos(a), c[1] + 6.5*k*math.sin(a))],
               fill=(120, 120, 130, 255), width=max(1, round(2.2*k)))
    d.ellipse([c[0] - 4.6*k, c[1] - 4.6*k, c[0] + 4.6*k, c[1] + 4.6*k], fill=(120, 120, 130, 255))
    d.ellipse([c[0] - 2*k, c[1] - 2*k, c[0] + 2*k, c[1] + 2*k], fill=(0, 0, 0, 0))
    return img

for name, draw in (('coc', coc), ('tienich', tienich)):
    for n in (16, 32):
        draw(n * 4).resize((n, n), Image.LANCZOS).save(f'src/C3DTools.Civil2021/Resources/{name}{n}.png')
```

Run: `python3 make_icons.py && file src/C3DTools.Civil2021/Resources/coc16.png src/C3DTools.Civil2021/Resources/tienich32.png && rm make_icons.py`
Expected: `PNG image data, 16 x 16, 8-bit/color RGBA` and `PNG image data, 32 x 32, 8-bit/color RGBA`.


**Step 2: Edit `RibbonSetup.cs`**

**Modify** `src/C3DTools.Civil2021/Ui/RibbonSetup.cs`

```diff
--- a/src/C3DTools.Civil2021/Ui/RibbonSetup.cs
+++ b/src/C3DTools.Civil2021/Ui/RibbonSetup.cs
@@ -12,11 +12,9 @@ using AcCoreApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
 namespace C3DTools.Civil2021.Ui;
 
 /// <summary>
-/// Tab C3DTools (UI rule 6): one panel per work area, the daily command as the large button, the rest small, each with a
-/// one-sentence Vietnamese tooltip. Tuyến: Yếu tố cong (CTYTC), Tạo tuyến (CTTUYEN), Phát sinh cọc (CTPHATCOC), Đánh tên cọc (CTDANHCOC),
-/// Mẫu TCVN (CTYTCMAU), Bảng cong (CTYTCBANG), Toạ độ cọc (CTTOADO).
-/// Trắc dọc: Bảng trắc dọc (CTTRACDOC), Cong đứng (CTCONGDUNG). Trắc ngang: Bảng trắc ngang (CTTRACNGANG), Xếp trang (CTXEPTRANG).
-/// Địa hình: Mặt địa hình (CTMATDIA), VN-2000 (CTVN2000). Thoát nước: Bảng cống (CTBANGCONG). Bản vẽ: Chuyển font (CTFONT), Chuẩn layer (CTLAYER).
+/// Tab C3DTools (UI rule 6): one panel per stage of the work, left to right — Tuyến, Cọc, Trắc dọc, Trắc ngang,
+/// Địa hình, Tiện ích. The stage's main command is the large button; at most three small ones beside it; each has a
+/// one-sentence Vietnamese tooltip.
 /// </summary>
 public sealed class RibbonSetup : IExtensionApplication
 {
@@ -58,15 +56,17 @@ public sealed class RibbonSetup : IExtensionApplication
         var ribbon = ComponentManager.Ribbon;
         if (ribbon.Tabs.Any(t => t.Id == TabId)) return;
 
+        // Left to right in the order of the work; at most three small buttons per panel (three rows fit the ribbon).
         var tab = new RibbonTab { Id = TabId, Title = "C3DTools" };
         tab.Panels.Add(Panel("Tuyến", "ytc",
-            ("Yếu tố cong", "CTYTC", "Thiết kế và cắm cong nằm theo TCVN 4054 cho polyline hoặc alignment: đường cong, siêu cao, mở rộng, khung yếu tố, cọc và bảng."),
-            ("Tạo tuyến", "CTTUYEN", "Tạo alignment mới từ polyline hoặc các điểm chỉ trên bản vẽ, kèm tỉ lệ bình đồ, vận tốc, trắc dọc tự nhiên và mặt cắt."),
-            ("Phát sinh cọc", "CTPHATCOC", "Phát sinh hoặc chèn cọc (Sample Line) dọc tuyến, khoảng cách riêng trên đoạn thẳng và đoạn cong."),
-            ("Đánh tên cọc", "CTDANHCOC", "Đánh lại tên toàn bộ cọc của một nhóm cọc: cọc C, H, Km và cọc chủ yếu theo quy tắc chọn."),
-            ("Mẫu TCVN", "CTYTCMAU", "Nhập kiểu nhãn, label set và bộ viết tắt TCVN cho alignment vào bản vẽ."),
+            ("Tạo tuyến", "CTTUYEN", "Tạo alignment mới từ polyline hoặc các điểm chỉ trên bản vẽ, kèm tỉ lệ bình đồ và vận tốc thiết kế."),
+            ("Yếu tố cong", "CTYTC", "Thiết kế và cắm cong nằm theo TCVN 4054: đường cong, siêu cao, mở rộng, khung yếu tố, cọc và bảng."),
             ("Bảng cong", "CTYTCBANG", "Xuất bảng tổng hợp yếu tố cong của một tuyến ra AutoCAD Table, CSV hoặc Excel."),
-            ("Toạ độ cọc", "CTTOADO", "Lập bảng toạ độ cọc của alignment ra AutoCAD Table, CSV, Excel hoặc điểm COGO.")));
+            ("Tuyến hiện hành", "CTTUYENHH", "Chọn tuyến mà các lệnh sau sẽ làm việc; không phải chọn lại ở từng lệnh.")));
+        tab.Panels.Add(Panel("Cọc", "coc",
+            ("Phát sinh cọc", "CTPHATCOC", "Phát sinh hoặc chèn cọc dọc tuyến: cọc Km, H, C và cọc đặc biệt tại đường cong, kèm tên cọc trên bình đồ."),
+            ("Đánh tên cọc", "CTDANHCOC", "Đánh lại tên cọc của một nhóm cọc theo quy tắc chọn và ghi tên lên bình đồ."),
+            ("Toạ độ cọc", "CTTOADO", "Lập bảng toạ độ cọc của tuyến ra AutoCAD Table, CSV, Excel hoặc điểm COGO.")));
         tab.Panels.Add(Panel("Trắc dọc", "tracdoc",
             ("Bảng trắc dọc", "CTTRACDOC", "Vẽ bảng số liệu trắc dọc kiểu Việt Nam dưới profile view và xuất CSV, Excel."),
             ("Cong đứng", "CTCONGDUNG", "Tính và ghi yếu tố cong đứng (A, R, T, E) của trắc dọc thiết kế, kiểm tra theo preset.")));
@@ -75,12 +75,13 @@ public sealed class RibbonSetup : IExtensionApplication
             ("Xếp trang", "CTXEPTRANG", "Xếp các trắc ngang vào tờ in theo thứ tự lý trình và vẽ khung tờ.")));
         tab.Panels.Add(Panel("Địa hình", "diahinh",
             ("Mặt địa hình", "CTMATDIA", "Xoá tam giác dài hoặc ngoài ranh giới của mặt phủ TIN, hoặc ghi cao độ đường đồng mức."),
-            ("VN-2000", "CTVN2000", "Chuyển toạ độ đối tượng hoặc điểm COGO sang kinh tuyến trục, múi chiếu VN-2000 khác.")));
-        tab.Panels.Add(Panel("Thoát nước", "thoatnuoc",
+            ("VN-2000", "CTVN2000", "Chuyển toạ độ đối tượng hoặc điểm COGO sang kinh tuyến trục, múi chiếu VN-2000 khác."),
             ("Bảng cống", "CTBANGCONG", "Lập bảng thống kê cống của các mạng cống cắt qua tuyến ra AutoCAD Table, CSV hoặc Excel.")));
-        tab.Panels.Add(Panel("Bản vẽ", "banve",
+        tab.Panels.Add(Panel("Tiện ích", "tienich",
+            ("Gửi báo lỗi", "CTBAOLOI", "Gom nhật ký, phiên bản và cấu hình thành một tệp zip trên Desktop để gửi khi gặp lỗi. Không kèm bản vẽ."),
             ("Chuyển font", "CTFONT", "Chuyển chữ tiếng Việt giữa TCVN3, VNI và Unicode cho đối tượng chọn hoặc toàn bản vẽ."),
-            ("Chuẩn layer", "CTLAYER", "Chuyển đối tượng sang layer chuẩn theo preset và xoá layer rỗng nếu cần.")));
+            ("Chuẩn layer", "CTLAYER", "Chuyển đối tượng sang layer chuẩn theo preset và xoá layer rỗng nếu cần."),
+            ("Mẫu TCVN", "CTYTCMAU", "Nhập kiểu nhãn, label set và bộ viết tắt TCVN cho alignment vào bản vẽ.")));
         ribbon.Tabs.Add(tab);
     }
```

**Step 3: Build the host**

Run: `dotnet build src/C3DTools.Civil2021 -c Release -v q --nologo`
Expected: `0 Warning(s)` and `0 Error(s)`.

**Step 4: Run the Core suite**

Run: `dotnet test C3DTools.Core.slnf -v q --nologo`
Expected: `Passed!  - Failed: 0`.

**Step 5: Commit**

```bash
git add src/C3DTools.Civil2021/Ui/RibbonSetup.cs src/C3DTools.Civil2021/Resources
git commit -m "feat(host): ribbon ordered by stage, three small buttons per panel"
```

### Task 9: Docs, packaging, release 0.6.0

The tester guide is in Vietnamese and ends with the list of what could not be checked on macOS.

**Files:**
- Modify: `.github/workflows/build.yml`
- Modify: `Directory.Build.props`
- Modify: `bundle/PackageContents.xml`
- Modify: `docs/testing.md`

**Step 1: Edit `build.yml`**

**Modify** `.github/workflows/build.yml`

```diff
--- a/.github/workflows/build.yml
+++ b/.github/workflows/build.yml
@@ -22,7 +22,7 @@ jobs:
         shell: pwsh
         run: |
           if ($env:GITHUB_REF_TYPE -eq 'tag') { $v = $env:GITHUB_REF_NAME.TrimStart('v') }
-          else { $v = "0.5.12.$env:GITHUB_RUN_NUMBER" }
+          else { $v = "0.6.0.$env:GITHUB_RUN_NUMBER" }
           "version=$v" >> $env:GITHUB_OUTPUT
 
       - name: Test Core
```


**Step 2: Edit `Directory.Build.props`**

**Modify** `Directory.Build.props`

```diff
--- a/Directory.Build.props
+++ b/Directory.Build.props
@@ -2,7 +2,7 @@
   <PropertyGroup>
     <LangVersion>10.0</LangVersion>
     <Nullable>disable</Nullable>
-    <Version>0.5.12</Version>
+    <Version>0.6.0</Version>
     <Company>C3DTools</Company>
     <Product>C3DTools</Product>
   </PropertyGroup>
```


**Step 3: Edit `PackageContents.xml`**

**Modify** `bundle/PackageContents.xml`

```diff
--- a/bundle/PackageContents.xml
+++ b/bundle/PackageContents.xml
@@ -3,7 +3,7 @@
                     AutodeskProduct="AutoCAD"
                     ProductType="Application"
                     Name="C3DTools"
-                    AppVersion="0.5.12"
+                    AppVersion="0.6.0"
                     Description="Công cụ tăng năng suất cho Civil 3D 2021"
                     Author="Bao Le"
                     ProductCode="{51A60CF3-C8AC-48ED-8988-5886BF0F851A}"
@@ -23,6 +23,8 @@
         <Command Global="CTTUYEN" Local="CTTUYEN" />
         <Command Global="CTPHATCOC" Local="CTPHATCOC" />
         <Command Global="CTDANHCOC" Local="CTDANHCOC" />
+        <Command Global="CTTUYENHH" Local="CTTUYENHH" />
+        <Command Global="CTBAOLOI" Local="CTBAOLOI" />
         <Command Global="CTFONT" Local="CTFONT" />
         <Command Global="CTLAYER" Local="CTLAYER" />
         <Command Global="CTTOADO" Local="CTTOADO" />
```


**Step 4: Edit `testing.md`**

**Modify** `docs/testing.md`

```diff
--- a/docs/testing.md
+++ b/docs/testing.md
@@ -192,3 +192,44 @@ Bảng có thêm cột **V** (tốc độ tại đỉnh, để trống = V của
 - [ ] `CTYTC` **Dồn dịch đỉnh trắc dọc phía sau**: PVI của trắc dọc thiết kế dời đúng khi R thay đổi
 - [ ] `CTYTC` **Góc chuyển hướng** trên polyline: đỉnh polyline dời theo
 - [ ] `CTPHATCOC` / `CTDANHCOC`: tạo, đổi tên, xoá Sample Line trong một bước undo; tên trùng giữa các Km có hậu tố "(Km1)"
+
+## Bản 0.6 — dùng dễ hơn
+
+Bản này không đổi phép tính nào; nó đổi cách dùng.
+
+### Tuyến hiện hành
+
+Bản vẽ nhớ một **tuyến hiện hành**. `CTPHATCOC`, `CTDANHCOC`, `CTTOADO`, `CTBANGCONG` mở ra là dùng ngay tuyến đó, không hỏi chọn alignment. Dòng lệnh báo `Tuyến hiện hành: <tên>`.
+
+- Tuyến vừa tạo bằng `CTTUYEN` tự thành tuyến hiện hành.
+- Bấm **Chọn trên bản vẽ…** trong bất kỳ lệnh nào và chọn tuyến khác: tuyến đó thành tuyến hiện hành.
+- Chọn alignment trước rồi mới gõ lệnh: lệnh dùng alignment đó cho lần chạy này.
+- Bản vẽ chỉ có một alignment: lệnh dùng luôn alignment đó.
+- `CTTUYENHH` (nút **Tuyến hiện hành**): xem hoặc đổi tuyến hiện hành.
+
+### Nâng cao và Về mặc định
+
+`CTTUYEN`, `CTPHATCOC`, `CTDANHCOC` chỉ hiện các ô hay dùng. Các ô còn lại nằm trong **Nâng cao**; bấm vào để mở, lần sau hộp thoại nhớ trạng thái mở/đóng.
+
+**Về mặc định** đặt lại mọi ô của hộp thoại về giá trị ban đầu. Tuyến đang chọn, kích thước cửa sổ và trạng thái Nâng cao giữ nguyên.
+
+### Ribbon
+
+Sáu panel theo thứ tự làm việc: **Tuyến → Cọc → Trắc dọc → Trắc ngang → Địa hình → Tiện ích**.
+
+### Gửi báo lỗi — `CTBAOLOI`
+
+Tạo tệp `C3DTools-baoloi-<ngày-giờ>.zip` trên Desktop, gồm `trace.log`, `error.log`, `options.json` và `thong-tin.txt` (phiên bản C3DTools, Civil 3D, Windows, tên bản vẽ, số alignment, số nhóm cọc). **Tệp không chứa bản vẽ và không tự gửi đi đâu.** Gửi tệp này kèm mô tả bước đang làm khi gặp lỗi.
+
+### Chưa kiểm tra trên Windows (0.6)
+
+- [ ] Tuyến hiện hành còn sau khi lưu, đóng và mở lại bản vẽ
+- [ ] Xoá alignment đang là tuyến hiện hành: lệnh hỏi chọn tuyến, không báo lỗi
+- [ ] Bản vẽ có 2 alignment, chưa có tuyến hiện hành: lệnh mở hộp thoại với "chưa chọn"
+- [ ] **Về mặc định**: các ô về giá trị ban đầu; tuyến, kích thước cửa sổ, trạng thái Nâng cao giữ nguyên
+- [ ] **Nâng cao**: mở/đóng được; chữ và mũi tên đọc được trên giao diện tối của Civil 3D
+- [ ] `CTTUYEN`: chọn tệp mặt cắt lỗi thì Nâng cao tự mở để hiện thông báo
+- [ ] Ribbon: 6 panel, không panel nào cao quá 3 hàng, icon Cọc và Tiện ích hiện đúng, bấm nút chạy lệnh
+- [ ] `CTBAOLOI`: có tệp zip trên Desktop, Explorer mở và chọn sẵn tệp, trong zip không có DWG
+- [ ] `U` sau `CTPHATCOC` hoàn tác cọc; tuyến hiện hành không gây thêm bước undo lạ
+
```

**Step 5: Check the versions agree**

Run: `grep -n "0.6.0" Directory.Build.props .github/workflows/build.yml bundle/PackageContents.xml`
Expected: one line from each of the three files.

**Step 6: Build the host**

Run: `dotnet build src/C3DTools.Civil2021 -c Release -v q --nologo`
Expected: `0 Warning(s)` and `0 Error(s)`.

**Step 7: Run the Core suite**

Run: `dotnet test C3DTools.Core.slnf -v q --nologo`
Expected: `Passed!  - Failed: 0`.

**Step 8: Commit**

```bash
git add .github/workflows/build.yml Directory.Build.props bundle/PackageContents.xml docs/testing.md
git commit -m "docs: 0.6 tester guide; bump to 0.6.0"
```

**Step 9: Release**

```bash
git push origin main
git tag -a v0.6.0 -m "C3DTools 0.6.0: active route, simpler dialogs, staged ribbon, bug report"
git push origin v0.6.0
gh run watch "$(gh run list --branch v0.6.0 --limit 1 --json databaseId --jq '.[0].databaseId')" --exit-status
gh release view v0.6.0 --json assets --jq '.assets[].name'
```

Expected: the run succeeds and the release lists `C3DTools-0.6.0.zip`.

**Step 10: After the tester confirms**

Record with the `autocad-learnings` skill what was seen on Civil 3D 2021: the Named Object Dictionary Xrecord surviving save and reopen, and how `Expander` renders under the dark theme.

---

## Risks

| Risk | Mitigation |
| --- | --- |
| A window shows "Về mặc định" but its loop ignores `Reset`: the dialog closes and nothing happens | The button exists only with `withReset: true`; the three windows that pass it have the loop case in Task 6 |
| The active route silently points at the wrong alignment | The command line always says which route is used and why; the dialog header shows its name and length |
| "Nâng cao" hides an option someone needs every time | Its open state is remembered per command; đợt 2 moves office-wide choices out of the dialogs |
| `Expander` is unreadable under AutoCAD's dark theme | On the tester checklist; fallback is a plain toggle button that sets the panel's `Visibility` |
| Writing the active route adds an undo step of its own | It happens inside the running command, which AutoCAD undoes as one; on the tester checklist |
| `main` moved since the diffs were taken | Context lines show where each change goes; rebase the diffs by hand where they no longer match |
