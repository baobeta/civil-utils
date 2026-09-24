# C3DTools Foundation Implementation Plan (PRD 1.0 — weeks 0–4)

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Stand up the repo, the host-independent Core library (all pure logic behind MVP commands C-01…C-08, fully unit-tested), a Civil 3D 2021 add-in skeleton that loads as a bundle, and the spike report that decides the final MVP scope.

**Architecture:** Three layers per PRD §7.3. `C3DTools.Core` (netstandard2.0) holds pure calculation — station formatting, point-file validation, average-end-area volumes, pipe rules, station planning, presets, table export — and is tested on any OS. `C3DTools.Civil2021` (net48, x64) holds commands, shared services and the Civil 3D/AutoCAD adapters, and is built and run only on Windows with Civil 3D 2021 installed. Host code never contains business rules; it reads/writes drawing objects and calls Core.

**Tech Stack:** .NET SDK 8+ (builds both), netstandard2.0 + net48, xUnit, Newtonsoft.Json, official `AutoCAD.NET` 24.0.0 NuGet (compile-only), community `Civil3D2021.Base` 1.0.0 NuGet for `AeccDbMgd`/`AecBaseMgd` (compile-only, never shipped), Autodesk bundle format (`PackageContents.xml`), GitHub Actions (Windows runner) producing a downloadable bundle zip.

---

## Where this plan sits

PRD §10 schedules 15 weeks. This plan covers **week 0 (setup) through the Core half of weeks 3–4**, plus the **week 1–2 spike**. Follow-up plans are written *after* the spike, because the spike decides which Civil 3D APIs actually work in 2021 (PRD §11):

| Plan | Content | Written when |
| --- | --- | --- |
| 1 (this) | Repo, Core logic + tests, add-in skeleton, bundle, spike | Now |
| 2 | Host services: command catalog, preview pipeline, style resolver, table builder (XLSX/AutoCAD Table), preset service, log, ribbon | After spike report |
| 3 | C-01…C-04 commands | After Plan 2 |
| 4 | C-05…C-08 commands, alpha → beta packaging | After Plan 3 |

## Machines

- **macOS (current):** writes and builds everything, including the net48 add-in (all Autodesk references come from NuGet). Runs Core tests.
- **GitHub Actions (`windows-latest`):** builds, tests and packages `C3DTools-<version>.zip` on every push; publishes a GitHub Release on a `v*` tag (Task 13).
- **Windows with Civil 3D 2021 (update 2021.3 or later):** only for *running* the add-in — the manual checks in Task 11 and the spike in Task 12. Testers download the zip, run `install.cmd`, open Civil 3D. Visual Studio 2022 is optional, for debugging.

## Conventions (apply to every task)

- Namespace root `C3DTools`; command names `CT…`; user-facing messages in Vietnamese; code and identifiers in English.
- **Always parse and format numbers with `CultureInfo.InvariantCulture`.** A Windows machine set to Vietnamese uses a comma as the decimal separator; relying on the current culture silently corrupts coordinates.
- Core has no reference to any Autodesk assembly. If a Core type needs a Civil 3D concept, model it as plain data.
- Commit after every task with a conventional commit message.

---

### Task 0: Install the .NET SDK (macOS)

**Step 1: Install**

Run: `brew install --cask dotnet-sdk`

**Step 2: Verify**

Run: `dotnet --list-sdks`
Expected: one line starting with `8.` or higher.

(No commit.)

---

### Task 1: Repo scaffolding

**Files:**
- Modify: `.gitignore`
- Create: `Directory.Build.props`
- Create: `src/C3DTools.Core/C3DTools.Core.csproj`
- Create: `tests/C3DTools.Core.Tests/C3DTools.Core.Tests.csproj`
- Create: `C3DTools.sln` (via CLI)
- Create: `C3DTools.Core.slnf`
- Create: `THIRD_PARTY.md`

**Step 1: Extend `.gitignore`** — append:

```gitignore

# .NET
bin/
obj/
.vs/
*.user
TestResults/
```

**Step 2: `Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <LangVersion>10.0</LangVersion>
    <Nullable>disable</Nullable>
    <Version>0.1.0</Version>
    <Company>C3DTools</Company>
    <Product>C3DTools</Product>
  </PropertyGroup>
</Project>
```

`LangVersion 10.0` gives file-scoped namespaces on netstandard2.0/net48 (syntax only). Do not use records or `init` — they need runtime types net48 lacks.

**Step 3: `src/C3DTools.Core/C3DTools.Core.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <RootNamespace>C3DTools.Core</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
```

**Step 4: `tests/C3DTools.Core.Tests/C3DTools.Core.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <!-- run on whatever newer runtime is installed -->
    <RollForward>Major</RollForward>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\C3DTools.Core\C3DTools.Core.csproj" />
  </ItemGroup>
</Project>
```

**Step 5: Solution + macOS filter**

```bash
dotnet new sln -n C3DTools
dotnet sln C3DTools.sln add src/C3DTools.Core/C3DTools.Core.csproj tests/C3DTools.Core.Tests/C3DTools.Core.Tests.csproj
```

`C3DTools.Core.slnf`:

```json
{
  "solution": {
    "path": "C3DTools.sln",
    "projects": [
      "src\\C3DTools.Core\\C3DTools.Core.csproj",
      "tests\\C3DTools.Core.Tests\\C3DTools.Core.Tests.csproj"
    ]
  }
}
```

**Step 6: `THIRD_PARTY.md`** (PRD §8.2 rule 1 — every shipped dependency or reused code is recorded here)

```markdown
# Third-party components

| Component | Version / commit | License | Shipped in bundle | Attribution required |
| --- | --- | --- | --- | --- |
| Newtonsoft.Json | 13.0.3 | MIT | Yes (merged via ILRepack from Plan 4) | Keep license text in bundle |

Test-only dependencies (xUnit, Microsoft.NET.Test.Sdk) are not shipped.

Reused source code from licensed repositories (PRD §8.1) must be added here with repo URL, commit hash and license terms **before** the code is merged.
```

**Step 7: Verify build**

Run: `dotnet build C3DTools.Core.slnf`
Expected: `Build succeeded.` with 0 errors.

**Step 8: Commit**

```bash
git add .gitignore Directory.Build.props C3DTools.sln C3DTools.Core.slnf src tests THIRD_PARTY.md
git commit -m "chore: scaffold solution with Core library and test project"
```

---

### Task 2: Station formatting (lý trình) — used by C-05, C-06, C-07

**Files:**
- Create: `src/C3DTools.Core/Stations/StationFormatter.cs`
- Create: `tests/C3DTools.Core.Tests/TestCulture.cs`
- Test: `tests/C3DTools.Core.Tests/Stations/StationFormatterTests.cs`

**Step 1: Test helper for culture** — `tests/C3DTools.Core.Tests/TestCulture.cs`

```csharp
using System;
using System.Globalization;

namespace C3DTools.Core.Tests;

/// <summary>Runs code under a given culture, e.g. vi-VN where the decimal separator is a comma.</summary>
public static class TestCulture
{
    public static void Run(string name, Action action)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(name);
        try { action(); }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
```

**Step 2: Write the failing tests** — `tests/C3DTools.Core.Tests/Stations/StationFormatterTests.cs`

```csharp
using System;
using C3DTools.Core.Stations;
using Xunit;

namespace C3DTools.Core.Tests.Stations;

public class StationFormatterTests
{
    [Theory]
    [InlineData(1234.56, 2, "Km1+234.56")]
    [InlineData(0, 2, "Km0+000.00")]
    [InlineData(5.5, 2, "Km0+005.50")]
    [InlineData(7, 0, "Km0+007")]
    [InlineData(999.999, 2, "Km1+000.00")]
    [InlineData(12345.6789, 3, "Km12+345.679")]
    [InlineData(-12.5, 2, "-Km0+012.50")]
    public void Format_produces_km_plus_metres(double station, int decimals, string expected)
    {
        Assert.Equal(expected, StationFormatter.Format(station, decimals));
    }

    [Fact]
    public void Format_ignores_vietnamese_culture()
    {
        TestCulture.Run("vi-VN", () => Assert.Equal("Km1+234.56", StationFormatter.Format(1234.56, 2)));
    }

    [Fact]
    public void Format_rejects_bad_decimals()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StationFormatter.Format(1, -1));
    }

    [Theory]
    [InlineData("Km1+234.56", 1234.56)]
    [InlineData("km0+005", 5)]
    [InlineData("1+234.56", 1234.56)]
    [InlineData(" Km12+000.00 ", 12000)]
    [InlineData("-Km0+012.50", -12.5)]
    public void TryParse_reads_station_text(string text, double expected)
    {
        Assert.True(StationFormatter.TryParse(text, out var station));
        Assert.Equal(expected, station, 6);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("Km1+1234")]
    [InlineData("Km1+23,5")]
    [InlineData("Km1")]
    public void TryParse_rejects_invalid_text(string text)
    {
        Assert.False(StationFormatter.TryParse(text, out _));
    }
}
```

**Step 3: Run to verify failure**

Run: `dotnet test C3DTools.Core.slnf`
Expected: build FAILS — `The type or namespace name 'Stations' does not exist`.

**Step 4: Implement** — `src/C3DTools.Core/Stations/StationFormatter.cs`

```csharp
using System;
using System.Globalization;

namespace C3DTools.Core.Stations;

/// <summary>Formats and parses Vietnamese station text such as "Km1+234.56".</summary>
public static class StationFormatter
{
    public static string Format(double station, int decimals)
    {
        if (decimals < 0 || decimals > 6) throw new ArgumentOutOfRangeException(nameof(decimals));

        // Round once up front so 999.999 becomes Km1+000.00, not Km0+1000.00.
        var rounded = Math.Round(Math.Abs(station), decimals, MidpointRounding.AwayFromZero);
        var km = (long)Math.Floor(rounded / 1000.0);
        var metres = Math.Round(rounded - km * 1000.0, decimals, MidpointRounding.AwayFromZero);
        if (metres >= 1000.0)
        {
            km += 1;
            metres -= 1000.0;
        }

        var width = decimals == 0 ? 3 : 4 + decimals;
        var metresText = metres.ToString("F" + decimals, CultureInfo.InvariantCulture).PadLeft(width, '0');
        var sign = station < 0 && rounded > 0 ? "-" : "";
        return sign + "Km" + km.ToString(CultureInfo.InvariantCulture) + "+" + metresText;
    }

    public static bool TryParse(string text, out double station)
    {
        station = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var s = text.Trim();
        var negative = s.StartsWith("-", StringComparison.Ordinal);
        if (negative) s = s.Substring(1);
        if (s.StartsWith("Km", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);

        var parts = s.Split('+');
        if (parts.Length != 2) return false;
        if (!long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var km)) return false;
        if (!double.TryParse(parts[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var metres)) return false;
        if (metres >= 1000.0) return false;

        station = (km * 1000.0 + metres) * (negative ? -1 : 1);
        return true;
    }
}
```

**Step 5: Run tests**

Run: `dotnet test C3DTools.Core.slnf`
Expected: PASS, 19 tests.

**Step 6: Commit**

```bash
git add src/C3DTools.Core/Stations tests/C3DTools.Core.Tests
git commit -m "feat(core): format and parse station text"
```

---

### Task 3: Point file validation — C-02 `CTDIEM`

PRD §2: errors to catch before import are duplicate numbers, swapped X/Y and comma decimals. Swap detection uses VN-2000 coordinate ranges (Northing ≈ 0.9–2.7 million m, Easting ≈ 0.1–0.9 million m); the ranges live in options so a preset can override them.

**Files:**
- Create: `src/C3DTools.Core/Points/PointFileOptions.cs`
- Create: `src/C3DTools.Core/Points/PointFileResult.cs`
- Create: `src/C3DTools.Core/Points/PointFileParser.cs`
- Test: `tests/C3DTools.Core.Tests/Points/PointFileParserTests.cs`

**Step 1: Write the failing tests**

```csharp
using System;
using System.Linq;
using C3DTools.Core.Points;
using Xunit;

namespace C3DTools.Core.Tests.Points;

public class PointFileParserTests
{
    private static PointFileResult Parse(PointFileOptions options, params string[] lines) =>
        PointFileParser.Parse(lines, options);

    private static PointFileResult Parse(params string[] lines) => Parse(new PointFileOptions(), lines);

    [Fact]
    public void Reads_valid_pnezd_line()
    {
        var result = Parse("1,1200000.123,550000.456,5.25,MOC");

        Assert.Empty(result.Issues);
        var p = Assert.Single(result.Points);
        Assert.Equal(1u, p.Number);
        Assert.Equal(1200000.123, p.Northing, 6);
        Assert.Equal(550000.456, p.Easting, 6);
        Assert.Equal(5.25, p.Elevation.Value, 6);
        Assert.Equal("MOC", p.Description);
        Assert.Equal(1, p.Line);
    }

    [Fact]
    public void Flags_duplicate_point_number_and_keeps_first()
    {
        var result = Parse("1,1200000,550000,5,A", "1,1200001,550001,5,B");

        var p = Assert.Single(result.Points);
        Assert.Equal("A", p.Description);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(PointIssueCode.DuplicateNumber, issue.Code);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal(2, issue.Line);
        Assert.Contains("dòng 1", issue.Message);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void Accepts_comma_decimals_with_warning_when_delimiter_is_not_comma()
    {
        var result = Parse(new PointFileOptions { Delimiter = ';' }, "5;1200000,5;550000,25;3,1;CAY");

        var p = Assert.Single(result.Points);
        Assert.Equal(1200000.5, p.Northing, 6);
        Assert.Equal(550000.25, p.Easting, 6);
        Assert.Equal(3.1, p.Elevation.Value, 6);
        Assert.Equal(3, result.Issues.Count(i => i.Code == PointIssueCode.CommaDecimal));
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Rejects_non_numeric_coordinate()
    {
        var result = Parse("7,abc,550000,5,X");

        Assert.Empty(result.Points);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(PointIssueCode.NotANumber, issue.Code);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
    }

    [Fact]
    public void Warns_when_northing_and_easting_look_swapped()
    {
        var result = Parse("8,550000,1200000,5,X");

        Assert.Single(result.Points);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(PointIssueCode.SwappedXY, issue.Code);
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void Rejects_line_with_too_few_columns()
    {
        var result = Parse("9,1200000");

        Assert.Empty(result.Points);
        Assert.Equal(PointIssueCode.WrongColumnCount, Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Keeps_delimiter_inside_trailing_description()
    {
        var result = Parse("10,1200000,550000,5,COT DIEN, BE TONG");

        Assert.Equal("COT DIEN, BE TONG", Assert.Single(result.Points).Description);
    }

    [Fact]
    public void Allows_missing_trailing_description()
    {
        var result = Parse("11,1200000,550000,5");

        Assert.Empty(result.Issues);
        Assert.Equal("", Assert.Single(result.Points).Description);
    }

    [Fact]
    public void Skips_blank_and_comment_lines_but_keeps_file_line_numbers()
    {
        var result = Parse("# so hieu,N,E,Z,mo ta", "", "12,1200000,550000,5,A", "x");

        Assert.Single(result.Points);
        Assert.Equal(4, Assert.Single(result.Issues).Line);
    }

    [Fact]
    public void Supports_other_column_orders()
    {
        var result = Parse(new PointFileOptions { Columns = "PENZ" }, "3,550000,1200000,4");

        var p = Assert.Single(result.Points);
        Assert.Equal(1200000, p.Northing, 6);
        Assert.Equal(550000, p.Easting, 6);
        Assert.Empty(result.Issues);
    }

    [Theory]
    [InlineData("PNX")]
    [InlineData("PNNE")]
    [InlineData("NEZ")]
    public void Rejects_invalid_column_spec(string columns)
    {
        Assert.Throws<ArgumentException>(() => Parse(new PointFileOptions { Columns = columns }, "1,2,3"));
    }

    [Fact]
    public void Parses_with_invariant_culture_on_vietnamese_windows()
    {
        TestCulture.Run("vi-VN", () =>
        {
            var result = Parse("1,1200000.123,550000.456,5.25,MOC");
            Assert.Empty(result.Issues);
            Assert.Equal(1200000.123, Assert.Single(result.Points).Northing, 6);
        });
    }
}
```

**Step 2: Run to verify failure**

Run: `dotnet test C3DTools.Core.slnf`
Expected: build FAILS — `The type or namespace name 'Points' does not exist`.

**Step 3: Implement**

`src/C3DTools.Core/Points/PointFileOptions.cs`

```csharp
namespace C3DTools.Core.Points;

public sealed class PointFileOptions
{
    /// <summary>Column order using P (number), N, E, Z, D (description). P, N and E are required.</summary>
    public string Columns { get; set; } = "PNEZD";

    public char Delimiter { get; set; } = ',';

    // VN-2000 plausibility ranges, used only to detect swapped N/E.
    public double MinNorthing { get; set; } = 900_000;
    public double MaxNorthing { get; set; } = 2_700_000;
    public double MinEasting { get; set; } = 100_000;
    public double MaxEasting { get; set; } = 900_000;
}
```

`src/C3DTools.Core/Points/PointFileResult.cs`

```csharp
using System.Collections.Generic;
using System.Linq;

namespace C3DTools.Core.Points;

public enum IssueSeverity { Warning, Error }

public enum PointIssueCode { WrongColumnCount, NotANumber, DuplicateNumber, CommaDecimal, SwappedXY }

public sealed class PointIssue
{
    public PointIssue(int line, IssueSeverity severity, PointIssueCode code, string message)
    {
        Line = line;
        Severity = severity;
        Code = code;
        Message = message;
    }

    public int Line { get; }
    public IssueSeverity Severity { get; }
    public PointIssueCode Code { get; }
    public string Message { get; }
}

public sealed class SurveyPoint
{
    public uint Number { get; set; }
    public double Northing { get; set; }
    public double Easting { get; set; }
    public double? Elevation { get; set; }
    public string Description { get; set; } = "";
    public int Line { get; set; }
}

public sealed class PointFileResult
{
    public PointFileResult(IReadOnlyList<SurveyPoint> points, IReadOnlyList<PointIssue> issues)
    {
        Points = points;
        Issues = issues;
    }

    public IReadOnlyList<SurveyPoint> Points { get; }
    public IReadOnlyList<PointIssue> Issues { get; }
    public bool HasErrors => Issues.Any(i => i.Severity == IssueSeverity.Error);
}
```

`src/C3DTools.Core/Points/PointFileParser.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace C3DTools.Core.Points;

/// <summary>Validates survey point files before they are imported as COGO points (C-02).</summary>
public static class PointFileParser
{
    public static PointFileResult Parse(IEnumerable<string> lines, PointFileOptions options)
    {
        if (lines == null) throw new ArgumentNullException(nameof(lines));
        if (options == null) throw new ArgumentNullException(nameof(options));
        var columns = ValidateColumns(options.Columns);

        var points = new List<SurveyPoint>();
        var issues = new List<PointIssue>();
        var firstLineByNumber = new Dictionary<uint, int>();
        var lineNo = 0;

        foreach (var raw in lines)
        {
            lineNo++;
            var line = raw?.Trim() ?? "";
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

            var fields = SplitFields(line, options.Delimiter, columns);
            if (fields == null)
            {
                issues.Add(new PointIssue(lineNo, IssueSeverity.Error, PointIssueCode.WrongColumnCount,
                    $"Dòng {lineNo}: cần {columns.Length} cột theo định dạng {options.Columns}."));
                continue;
            }

            uint? number = null;
            double? n = null, e = null, z = null;
            var description = "";
            var failed = false;

            for (var i = 0; i < columns.Length; i++)
            {
                var field = fields[i];
                switch (columns[i])
                {
                    case 'P':
                        if (uint.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)) number = parsed;
                        else issues.Add(NotANumber(lineNo, "số điểm", field));
                        failed |= number == null;
                        break;
                    case 'N':
                        n = ReadNumber(field, "N", lineNo, options.Delimiter, issues);
                        failed |= n == null;
                        break;
                    case 'E':
                        e = ReadNumber(field, "E", lineNo, options.Delimiter, issues);
                        failed |= e == null;
                        break;
                    case 'Z':
                        z = ReadNumber(field, "Z", lineNo, options.Delimiter, issues);
                        failed |= z == null;
                        break;
                    case 'D':
                        description = field;
                        break;
                }
            }

            if (failed) continue;

            if (firstLineByNumber.TryGetValue(number.Value, out var firstLine))
            {
                issues.Add(new PointIssue(lineNo, IssueSeverity.Error, PointIssueCode.DuplicateNumber,
                    $"Dòng {lineNo}: số điểm {number.Value} trùng với dòng {firstLine}."));
                continue;
            }
            firstLineByNumber[number.Value] = lineNo;

            if (InRange(e.Value, options.MinNorthing, options.MaxNorthing) &&
                InRange(n.Value, options.MinEasting, options.MaxEasting))
            {
                issues.Add(new PointIssue(lineNo, IssueSeverity.Warning, PointIssueCode.SwappedXY,
                    $"Dòng {lineNo}: N={Inv(n.Value)}, E={Inv(e.Value)} có vẻ bị đảo X/Y. Kiểm tra thứ tự cột."));
            }

            points.Add(new SurveyPoint
            {
                Number = number.Value,
                Northing = n.Value,
                Easting = e.Value,
                Elevation = z,
                Description = description,
                Line = lineNo,
            });
        }

        return new PointFileResult(points, issues);
    }

    private static char[] ValidateColumns(string spec)
    {
        var cols = (spec ?? "").ToUpperInvariant().ToCharArray();
        var valid = cols.All(c => "PNEZD".IndexOf(c) >= 0)
                    && cols.Distinct().Count() == cols.Length
                    && cols.Contains('P') && cols.Contains('N') && cols.Contains('E');
        if (!valid)
        {
            throw new ArgumentException(
                $"Định dạng cột không hợp lệ: '{spec}'. Dùng các ký tự P, N, E, Z, D; bắt buộc có P, N, E.",
                nameof(spec));
        }
        return cols;
    }

    private static string[] SplitFields(string line, char delimiter, char[] columns)
    {
        var endsWithDescription = columns[columns.Length - 1] == 'D';
        // A trailing description may itself contain the delimiter, so cap the split count.
        var parts = endsWithDescription
            ? line.Split(new[] { delimiter }, columns.Length)
            : line.Split(delimiter);

        if (endsWithDescription && parts.Length == columns.Length - 1)
            parts = parts.Concat(new[] { "" }).ToArray();

        return parts.Length == columns.Length ? parts.Select(p => p.Trim()).ToArray() : null;
    }

    private static double? ReadNumber(string field, string column, int lineNo, char delimiter, List<PointIssue> issues)
    {
        if (double.TryParse(field, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return value;

        if (delimiter != ',' && field.Contains(",") &&
            double.TryParse(field.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            issues.Add(new PointIssue(lineNo, IssueSeverity.Warning, PointIssueCode.CommaDecimal,
                $"Dòng {lineNo}: cột {column} dùng dấu phẩy thập phân ('{field}'), đã đọc là {Inv(value)}."));
            return value;
        }

        issues.Add(NotANumber(lineNo, "cột " + column, field));
        return null;
    }

    private static PointIssue NotANumber(int lineNo, string what, string field) =>
        new PointIssue(lineNo, IssueSeverity.Error, PointIssueCode.NotANumber,
            $"Dòng {lineNo}: {what} không phải số ('{field}').");

    private static bool InRange(double value, double min, double max) => value >= min && value <= max;

    private static string Inv(double value) => value.ToString(CultureInfo.InvariantCulture);
}
```

**Step 4: Run tests**

Run: `dotnet test C3DTools.Core.slnf`
Expected: PASS, 33 tests.

**Step 5: Commit**

```bash
git add src/C3DTools.Core/Points tests/C3DTools.Core.Tests/Points
git commit -m "feat(core): validate survey point files before import"
```

---

### Task 4: Average-end-area volumes — C-07 `CTKHOILUONG`

PRD §11 names this the top risk: the 2021 API may not expose per-material volumes, so the plugin computes them from section areas and the spike compares against Civil 3D's report.

**Files:**
- Create: `src/C3DTools.Core/Volumes/AverageEndArea.cs`
- Test: `tests/C3DTools.Core.Tests/Volumes/AverageEndAreaTests.cs`

**Step 1: Write the failing tests**

```csharp
using System;
using C3DTools.Core.Volumes;
using Xunit;

namespace C3DTools.Core.Tests.Volumes;

public class AverageEndAreaTests
{
    [Fact]
    public void First_section_has_zero_volume()
    {
        var rows = AverageEndArea.Compute(new[] { new SectionAreas(0, 10, 2) });

        var row = Assert.Single(rows);
        Assert.Equal(0, row.Distance);
        Assert.Equal(0, row.CutVolume);
        Assert.Equal(0, row.FillVolume);
    }

    [Fact]
    public void Volume_is_mean_area_times_distance()
    {
        var rows = AverageEndArea.Compute(new[]
        {
            new SectionAreas(0, 10, 0),
            new SectionAreas(20, 20, 4),
        });

        Assert.Equal(20, rows[1].Distance, 9);
        Assert.Equal(300, rows[1].CutVolume, 9);  // (10 + 20) / 2 * 20
        Assert.Equal(40, rows[1].FillVolume, 9);  // (0 + 4) / 2 * 20
    }

    [Fact]
    public void Accumulates_volumes()
    {
        var rows = AverageEndArea.Compute(new[]
        {
            new SectionAreas(0, 10, 0),
            new SectionAreas(20, 20, 4),
            new SectionAreas(50, 0, 6),
        });

        Assert.Equal(300 + 300, rows[2].CumulativeCut, 9);  // (20 + 0) / 2 * 30 = 300
        Assert.Equal(40 + 150, rows[2].CumulativeFill, 9);  // (4 + 6) / 2 * 30 = 150
    }

    [Fact]
    public void Empty_input_gives_empty_output()
    {
        Assert.Empty(AverageEndArea.Compute(Array.Empty<SectionAreas>()));
    }

    [Fact]
    public void Rejects_stations_that_do_not_increase()
    {
        var ex = Assert.Throws<ArgumentException>(() => AverageEndArea.Compute(new[]
        {
            new SectionAreas(20, 1, 1),
            new SectionAreas(20, 1, 1),
        }));
        Assert.Contains("tăng dần", ex.Message);
    }

    [Fact]
    public void Rejects_negative_area()
    {
        Assert.Throws<ArgumentException>(() => AverageEndArea.Compute(new[] { new SectionAreas(0, -1, 0) }));
    }
}
```

**Step 2: Run to verify failure**

Run: `dotnet test C3DTools.Core.slnf`
Expected: build FAILS — `The type or namespace name 'Volumes' does not exist`.

**Step 3: Implement** — `src/C3DTools.Core/Volumes/AverageEndArea.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;

namespace C3DTools.Core.Volumes;

public sealed class SectionAreas
{
    public SectionAreas(double station, double cutArea, double fillArea)
    {
        Station = station;
        CutArea = cutArea;
        FillArea = fillArea;
    }

    public double Station { get; }
    public double CutArea { get; }
    public double FillArea { get; }
}

public sealed class VolumeRow
{
    public double Station { get; set; }
    public double CutArea { get; set; }
    public double FillArea { get; set; }
    public double Distance { get; set; }
    public double CutVolume { get; set; }
    public double FillVolume { get; set; }
    public double CumulativeCut { get; set; }
    public double CumulativeFill { get; set; }
}

/// <summary>Cut/fill volumes between consecutive sections by the average end area method.</summary>
public static class AverageEndArea
{
    public static IReadOnlyList<VolumeRow> Compute(IReadOnlyList<SectionAreas> sections)
    {
        if (sections == null) throw new ArgumentNullException(nameof(sections));

        var rows = new List<VolumeRow>(sections.Count);
        VolumeRow previous = null;

        foreach (var s in sections)
        {
            if (s.CutArea < 0 || s.FillArea < 0)
                throw new ArgumentException($"Diện tích âm tại lý trình {Inv(s.Station)}.", nameof(sections));
            if (previous != null && s.Station <= previous.Station)
                throw new ArgumentException(
                    $"Lý trình phải tăng dần: {Inv(s.Station)} đứng sau {Inv(previous.Station)}.", nameof(sections));

            var row = new VolumeRow { Station = s.Station, CutArea = s.CutArea, FillArea = s.FillArea };
            if (previous != null)
            {
                row.Distance = s.Station - previous.Station;
                row.CutVolume = (previous.CutArea + s.CutArea) / 2.0 * row.Distance;
                row.FillVolume = (previous.FillArea + s.FillArea) / 2.0 * row.Distance;
                row.CumulativeCut = previous.CumulativeCut + row.CutVolume;
                row.CumulativeFill = previous.CumulativeFill + row.FillVolume;
            }

            rows.Add(row);
            previous = row;
        }

        return rows;
    }

    private static string Inv(double value) => value.ToString(CultureInfo.InvariantCulture);
}
```

**Step 4: Run tests**

Run: `dotnet test C3DTools.Core.slnf`
Expected: PASS, 39 tests.

**Step 5: Commit**

```bash
git add src/C3DTools.Core/Volumes tests/C3DTools.Core.Tests/Volumes
git commit -m "feat(core): average end area cut/fill volumes"
```

---

### Task 5: Pipe checks — C-08 `CTCONG`

Thresholds are **not** hard-coded: PRD §12 lists "which regulation sets minimum slope/cover" as an open discovery question, so `PipeRules` has no defaults and must come from the project preset.

Cover is measured from ground to the pipe's outer top: `ground − (invert + inner diameter + wall thickness)`. Civil 3D exposes `InnerDiameterOrWidth` and `WallThickness` on pipes, so the adapter can fill this directly.

**Files:**
- Create: `src/C3DTools.Core/Drainage/PipeChecker.cs`
- Test: `tests/C3DTools.Core.Tests/Drainage/PipeCheckerTests.cs`

**Step 1: Write the failing tests**

```csharp
using System.Linq;
using C3DTools.Core.Drainage;
using Xunit;

namespace C3DTools.Core.Tests.Drainage;

public class PipeCheckerTests
{
    private static readonly PipeRules Rules = new PipeRules(minSlope: 0.003, minCover: 0.7);

    private static PipeData Pipe() => new PipeData
    {
        Name = "C1",
        Length = 40,
        StartInvert = 10.0,
        EndInvert = 9.8,
        InnerDiameter = 0.6,
        WallThickness = 0.08,
        StartGround = 11.5,
        EndGround = 11.4,
    };

    [Fact]
    public void Good_pipe_has_no_issues()
    {
        var r = PipeChecker.Check(Pipe(), Rules);

        Assert.Empty(r.Issues);
        Assert.Equal(0.005, r.Slope, 9);
        Assert.Equal(0.82, r.StartCover, 9);  // 11.5 - (10.0 + 0.6 + 0.08)
        Assert.Equal(0.92, r.EndCover, 9);    // 11.4 - (9.8 + 0.6 + 0.08)
    }

    [Fact]
    public void Flags_slope_below_minimum()
    {
        var p = Pipe();
        p.EndInvert = 9.94;  // slope 0.0015

        var issue = Assert.Single(PipeChecker.Check(p, Rules).Issues);
        Assert.Equal(PipeIssueCode.SlopeTooLow, issue.Code);
        Assert.Contains("0.15%", issue.Message);
    }

    [Fact]
    public void Flags_adverse_slope()
    {
        var p = Pipe();
        p.EndInvert = 10.1;

        var r = PipeChecker.Check(p, Rules);
        Assert.True(r.Slope < 0);
        Assert.Contains(r.Issues, i => i.Code == PipeIssueCode.SlopeTooLow);
    }

    [Fact]
    public void Flags_low_cover_at_end()
    {
        var p = Pipe();
        p.EndGround = 10.9;  // cover 0.42

        var r = PipeChecker.Check(p, Rules);
        Assert.Equal(PipeIssueCode.CoverTooLowEnd, Assert.Single(r.Issues).Code);
    }

    [Fact]
    public void Zero_length_is_reported_without_other_checks()
    {
        var p = Pipe();
        p.Length = 0;

        var r = PipeChecker.Check(p, Rules);
        Assert.Equal(PipeIssueCode.InvalidLength, Assert.Single(r.Issues).Code);
        Assert.True(double.IsNaN(r.Slope));
    }

    [Fact]
    public void Messages_use_dot_decimal_on_vietnamese_windows()
    {
        TestCulture.Run("vi-VN", () =>
        {
            var p = Pipe();
            p.EndInvert = 9.94;
            Assert.Contains("0.15%", PipeChecker.Check(p, Rules).Issues.Single().Message);
        });
    }
}
```

**Step 2: Run to verify failure**

Run: `dotnet test C3DTools.Core.slnf`
Expected: build FAILS — `The type or namespace name 'Drainage' does not exist`.

**Step 3: Implement** — `src/C3DTools.Core/Drainage/PipeChecker.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;

namespace C3DTools.Core.Drainage;

public sealed class PipeData
{
    public string Name { get; set; } = "";
    public double Length { get; set; }
    public double StartInvert { get; set; }
    public double EndInvert { get; set; }
    public double InnerDiameter { get; set; }
    public double WallThickness { get; set; }
    public double StartGround { get; set; }
    public double EndGround { get; set; }
}

/// <summary>Project thresholds. Values come from the preset; there are deliberately no defaults.</summary>
public sealed class PipeRules
{
    public PipeRules(double minSlope, double minCover)
    {
        MinSlope = minSlope;
        MinCover = minCover;
    }

    /// <summary>Minimum slope as a fraction (0.003 = 0.3%).</summary>
    public double MinSlope { get; }

    /// <summary>Minimum cover in metres, ground to pipe outer top.</summary>
    public double MinCover { get; }
}

public enum PipeIssueCode { InvalidLength, SlopeTooLow, CoverTooLowStart, CoverTooLowEnd }

public sealed class PipeIssue
{
    public PipeIssue(PipeIssueCode code, string message)
    {
        Code = code;
        Message = message;
    }

    public PipeIssueCode Code { get; }
    public string Message { get; }
}

public sealed class PipeCheckResult
{
    public PipeCheckResult(string name, double slope, double startCover, double endCover, IReadOnlyList<PipeIssue> issues)
    {
        Name = name;
        Slope = slope;
        StartCover = startCover;
        EndCover = endCover;
        Issues = issues;
    }

    public string Name { get; }
    public double Slope { get; }
    public double StartCover { get; }
    public double EndCover { get; }
    public IReadOnlyList<PipeIssue> Issues { get; }
}

public static class PipeChecker
{
    public static PipeCheckResult Check(PipeData pipe, PipeRules rules)
    {
        if (pipe == null) throw new ArgumentNullException(nameof(pipe));
        if (rules == null) throw new ArgumentNullException(nameof(rules));

        var issues = new List<PipeIssue>();
        if (!(pipe.Length > 0))
        {
            issues.Add(new PipeIssue(PipeIssueCode.InvalidLength, $"Cống {pipe.Name}: chiều dài không hợp lệ."));
            return new PipeCheckResult(pipe.Name, double.NaN, double.NaN, double.NaN, issues);
        }

        var slope = (pipe.StartInvert - pipe.EndInvert) / pipe.Length;
        var outerHeight = pipe.InnerDiameter + pipe.WallThickness;
        var startCover = pipe.StartGround - (pipe.StartInvert + outerHeight);
        var endCover = pipe.EndGround - (pipe.EndInvert + outerHeight);

        if (slope < rules.MinSlope)
            issues.Add(new PipeIssue(PipeIssueCode.SlopeTooLow,
                $"Cống {pipe.Name}: độ dốc {Percent(slope)} nhỏ hơn tối thiểu {Percent(rules.MinSlope)}."));
        if (startCover < rules.MinCover)
            issues.Add(new PipeIssue(PipeIssueCode.CoverTooLowStart,
                $"Cống {pipe.Name}: chiều sâu chôn đầu cống {Metres(startCover)} nhỏ hơn {Metres(rules.MinCover)}."));
        if (endCover < rules.MinCover)
            issues.Add(new PipeIssue(PipeIssueCode.CoverTooLowEnd,
                $"Cống {pipe.Name}: chiều sâu chôn cuối cống {Metres(endCover)} nhỏ hơn {Metres(rules.MinCover)}."));

        return new PipeCheckResult(pipe.Name, slope, startCover, endCover, issues);
    }

    private static string Percent(double fraction) =>
        (fraction * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%";

    private static string Metres(double value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture) + " m";
}
```

**Step 4: Run tests**

Run: `dotnet test C3DTools.Core.slnf`
Expected: PASS, 45 tests.

**Step 5: Commit**

```bash
git add src/C3DTools.Core/Drainage tests/C3DTools.Core.Tests/Drainage
git commit -m "feat(core): pipe slope and cover checks"
```

---

### Task 6: Station planning — C-06 `CTCOC`

Builds the list of stations for sample lines: regular stations every `interval`, plus the start and end, merged with special stations (TĐ, TC, P…). A special station replaces any regular station within `tolerance`.

**Naming is a placeholder** (`Km{n}` at each kilometre, `H{n}` at each hundred metres, `C{i}` for everything else). PRD §12 asks how real projects name their stations; once discovery answers that, replace `RegularName` and its tests.

**Files:**
- Create: `src/C3DTools.Core/Stations/StationPlanner.cs`
- Test: `tests/C3DTools.Core.Tests/Stations/StationPlannerTests.cs`

**Step 1: Write the failing tests**

```csharp
using System;
using System.Linq;
using C3DTools.Core.Stations;
using Xunit;

namespace C3DTools.Core.Tests.Stations;

public class StationPlannerTests
{
    [Fact]
    public void Builds_regular_stations_including_start_and_end()
    {
        var plan = StationPlanner.Build(0, 250, 50, null);

        Assert.Equal(new[] { 0.0, 50, 100, 150, 200, 250 }, plan.Select(p => p.Station));
        Assert.Equal(new[] { "Km0", "C1", "H1", "C2", "H2", "C3" }, plan.Select(p => p.Name));
        Assert.All(plan, p => Assert.False(p.IsSpecial));
    }

    [Fact]
    public void Adds_end_when_not_on_interval()
    {
        var plan = StationPlanner.Build(0, 120, 50, null);

        Assert.Equal(new[] { 0.0, 50, 100, 120 }, plan.Select(p => p.Station));
    }

    [Fact]
    public void Merges_special_stations_and_replaces_nearby_regular_ones()
    {
        var specials = new[]
        {
            new SpecialStation(120.5, "TĐ1"),
            new SpecialStation(150.004, "P1"),
        };

        var plan = StationPlanner.Build(0, 250, 50, specials);

        Assert.Equal(new[] { "Km0", "C1", "H1", "TĐ1", "P1", "H2", "C2" }, plan.Select(p => p.Name));
        Assert.True(plan.Single(p => p.Name == "P1").IsSpecial);
    }

    [Fact]
    public void Names_kilometre_and_hundreds_after_first_km()
    {
        var plan = StationPlanner.Build(1000, 1100, 100, null);

        Assert.Equal(new[] { "Km1", "H1" }, plan.Select(p => p.Name));
    }

    [Fact]
    public void Rejects_special_station_outside_range()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            StationPlanner.Build(0, 100, 50, new[] { new SpecialStation(150, "TC1") }));
        Assert.Contains("TC1", ex.Message);
    }

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(0, 100, -5)]
    public void Rejects_non_positive_interval(double start, double end, double interval)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StationPlanner.Build(start, end, interval, null));
    }

    [Fact]
    public void Rejects_end_not_after_start()
    {
        Assert.Throws<ArgumentException>(() => StationPlanner.Build(100, 100, 20, null));
    }
}
```

**Step 2: Run to verify failure**

Run: `dotnet test C3DTools.Core.slnf`
Expected: build FAILS — `The name 'StationPlanner' does not exist`.

**Step 3: Implement** — `src/C3DTools.Core/Stations/StationPlanner.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace C3DTools.Core.Stations;

public sealed class SpecialStation
{
    public SpecialStation(double station, string name)
    {
        Station = station;
        Name = name;
    }

    public double Station { get; }
    public string Name { get; }
}

public sealed class PlannedStation
{
    public PlannedStation(double station, string name, bool isSpecial)
    {
        Station = station;
        Name = name;
        IsSpecial = isSpecial;
    }

    public double Station { get; }
    public string Name { get; }
    public bool IsSpecial { get; }
}

/// <summary>Plans sample line stations for C-06.</summary>
public static class StationPlanner
{
    public static IReadOnlyList<PlannedStation> Build(
        double start, double end, double interval, IEnumerable<SpecialStation> specials, double tolerance = 0.01)
    {
        if (!(end > start)) throw new ArgumentException("Lý trình cuối phải lớn hơn lý trình đầu.", nameof(end));
        if (!(interval > 0)) throw new ArgumentOutOfRangeException(nameof(interval), "Khoảng cách cọc phải lớn hơn 0.");

        var specialList = (specials ?? Enumerable.Empty<SpecialStation>()).ToList();
        foreach (var s in specialList)
        {
            if (s.Station < start - tolerance || s.Station > end + tolerance)
                throw new ArgumentOutOfRangeException(nameof(specials), $"Cọc {s.Name} nằm ngoài phạm vi tuyến.");
        }

        // Multiply instead of accumulating so 0.1-step errors do not build up over long alignments.
        var regular = new List<double> { start };
        for (var k = (long)Math.Ceiling((start + tolerance) / interval); k * interval < end - tolerance; k++)
            regular.Add(k * interval);
        regular.Add(end);

        var result = new List<PlannedStation>();
        var detailIndex = 0;
        foreach (var station in regular)
        {
            if (specialList.Any(s => Math.Abs(s.Station - station) <= tolerance)) continue;
            result.Add(new PlannedStation(station, RegularName(station, tolerance, ref detailIndex), false));
        }

        result.AddRange(specialList.Select(s => new PlannedStation(s.Station, s.Name, true)));
        return result.OrderBy(p => p.Station).ToList();
    }

    // Placeholder convention until discovery confirms how projects name stations (PRD §12).
    private static string RegularName(double station, double tolerance, ref int detailIndex)
    {
        var km = Math.Round(station / 1000.0);
        if (Math.Abs(station - km * 1000.0) <= tolerance)
            return "Km" + ((long)km).ToString(CultureInfo.InvariantCulture);

        var hundreds = Math.Round(station / 100.0);
        if (Math.Abs(station - hundreds * 100.0) <= tolerance)
            return "H" + ((long)hundreds % 10).ToString(CultureInfo.InvariantCulture);

        detailIndex++;
        return "C" + detailIndex.ToString(CultureInfo.InvariantCulture);
    }
}
```

**Step 4: Run tests**

Run: `dotnet test C3DTools.Core.slnf`
Expected: PASS, 52 tests.

**Step 5: Commit**

```bash
git add src/C3DTools.Core/Stations tests/C3DTools.Core.Tests/Stations
git commit -m "feat(core): plan sample line stations with special stations"
```

---

### Task 7: Project preset (JSON, versioned)

PRD §6/§7.4: presets carry project name/version, decimals, Civil 3D style names, point file options and pipe rules. The file has a `SchemaVersion` so an older plugin refuses a preset written by a newer one instead of silently dropping fields.

**Files:**
- Create: `src/C3DTools.Core/Presets/ProjectPreset.cs`
- Create: `src/C3DTools.Core/Presets/PresetSerializer.cs`
- Test: `tests/C3DTools.Core.Tests/Presets/PresetSerializerTests.cs`

**Step 1: Write the failing tests**

```csharp
using C3DTools.Core.Drainage;
using C3DTools.Core.Presets;
using Xunit;

namespace C3DTools.Core.Tests.Presets;

public class PresetSerializerTests
{
    [Fact]
    public void Round_trips_a_preset()
    {
        var preset = new ProjectPreset { Name = "Dự án QL1A", Version = "1.2", StationDecimals = 3 };
        preset.Styles["Alignment"] = "Tim tuyến";
        preset.PointFile.Delimiter = ';';
        preset.PipeRules = new PipeRules(0.003, 0.7);

        var back = PresetSerializer.Load(PresetSerializer.Save(preset));

        Assert.Equal("Dự án QL1A", back.Name);
        Assert.Equal("1.2", back.Version);
        Assert.Equal(3, back.StationDecimals);
        Assert.Equal("Tim tuyến", back.Styles["Alignment"]);
        Assert.Equal(';', back.PointFile.Delimiter);
        Assert.Equal(0.003, back.PipeRules.MinSlope);
        Assert.Equal(0.7, back.PipeRules.MinCover);
    }

    [Fact]
    public void Style_keys_are_case_insensitive_after_load()
    {
        var json = "{ \"SchemaVersion\": 1, \"Name\": \"A\", \"Styles\": { \"Alignment\": \"Tim tuyến\" } }";

        Assert.Equal("Tim tuyến", PresetSerializer.Load(json).Styles["alignment"]);
    }

    [Fact]
    public void Pipe_rules_stay_null_when_not_configured()
    {
        Assert.Null(PresetSerializer.Load("{ \"SchemaVersion\": 1, \"Name\": \"A\" }").PipeRules);
    }

    [Fact]
    public void Rejects_missing_schema_version()
    {
        var ex = Assert.Throws<PresetException>(() => PresetSerializer.Load("{ \"Name\": \"A\" }"));
        Assert.Contains("SchemaVersion", ex.Message);
    }

    [Fact]
    public void Rejects_newer_schema_version()
    {
        var ex = Assert.Throws<PresetException>(() => PresetSerializer.Load("{ \"SchemaVersion\": 99 }"));
        Assert.Contains("mới hơn", ex.Message);
    }

    [Fact]
    public void Wraps_invalid_json()
    {
        Assert.Throws<PresetException>(() => PresetSerializer.Load("{ not json"));
    }
}
```

**Step 2: Run to verify failure**

Run: `dotnet test C3DTools.Core.slnf`
Expected: build FAILS — `The type or namespace name 'Presets' does not exist`.

**Step 3: Implement**

`src/C3DTools.Core/Presets/ProjectPreset.cs`

```csharp
using System;
using System.Collections.Generic;
using C3DTools.Core.Drainage;
using C3DTools.Core.Points;

namespace C3DTools.Core.Presets;

public sealed class ProjectPreset
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0";

    public int StationDecimals { get; set; } = 2;
    public int ElevationDecimals { get; set; } = 3;
    public int VolumeDecimals { get; set; } = 2;

    /// <summary>Civil 3D style / label set names keyed by role, e.g. "Alignment" → "Tim tuyến".</summary>
    public Dictionary<string, string> Styles { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public PointFileOptions PointFile { get; set; } = new PointFileOptions();

    /// <summary>Null until the project defines its thresholds; C-08 refuses to run without them.</summary>
    public PipeRules PipeRules { get; set; }
}
```

`src/C3DTools.Core/Presets/PresetSerializer.cs`

```csharp
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
}
```

**Step 4: Run tests**

Run: `dotnet test C3DTools.Core.slnf`
Expected: PASS, 58 tests.

**Step 5: Commit**

```bash
git add src/C3DTools.Core/Presets tests/C3DTools.Core.Tests/Presets
git commit -m "feat(core): versioned project preset serialization"
```

---

### Task 8: Table data and UTF-8 CSV export

PRD §6: exports must keep Vietnamese text. Excel only detects UTF-8 in a CSV when the file starts with a byte-order mark, so the writer always emits one. XLSX (ClosedXML) and AutoCAD Table output come in Plan 2 and reuse `TableData`.

**Files:**
- Create: `src/C3DTools.Core/Tables/TableData.cs`
- Create: `src/C3DTools.Core/Tables/CsvTableWriter.cs`
- Create: `src/C3DTools.Core/Tables/NumberFormat.cs`
- Test: `tests/C3DTools.Core.Tests/Tables/CsvTableWriterTests.cs`

**Step 1: Write the failing tests**

```csharp
using System;
using System.IO;
using System.Text;
using C3DTools.Core.Tables;
using Xunit;

namespace C3DTools.Core.Tests.Tables;

public class CsvTableWriterTests
{
    private static byte[] Write(TableData table)
    {
        using var stream = new MemoryStream();
        CsvTableWriter.Write(table, stream);
        return stream.ToArray();
    }

    [Fact]
    public void Writes_utf8_with_bom_and_crlf()
    {
        var table = new TableData("Cọc", "Cao độ");
        table.AddRow("Km0", "1.234");

        var bytes = Write(table);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal("Cọc,Cao độ\r\nKm0,1.234\r\n", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }

    [Fact]
    public void Quotes_cells_with_delimiter_quote_or_newline()
    {
        var table = new TableData("A", "B", "C");
        table.AddRow("2,5", "say \"hi\"", "x\ny");

        var text = Encoding.UTF8.GetString(Write(table)[3..]);

        Assert.Equal("A,B,C\r\n\"2,5\",\"say \"\"hi\"\"\",\"x\ny\"\r\n", text);
    }

    [Fact]
    public void Rejects_row_with_wrong_cell_count()
    {
        var table = new TableData("A", "B");
        Assert.Throws<ArgumentException>(() => table.AddRow("only one"));
    }

    [Fact]
    public void Leaves_stream_open()
    {
        using var stream = new MemoryStream();
        CsvTableWriter.Write(new TableData("A"), stream);
        Assert.True(stream.CanWrite);
    }

    [Fact]
    public void NumberFormat_uses_dot_on_vietnamese_windows()
    {
        TestCulture.Run("vi-VN", () => Assert.Equal("1.50", NumberFormat.Fixed(1.5, 2)));
    }
}
```

**Step 2: Run to verify failure**

Run: `dotnet test C3DTools.Core.slnf`
Expected: build FAILS — `The type or namespace name 'Tables' does not exist`.

**Step 3: Implement**

`src/C3DTools.Core/Tables/TableData.cs`

```csharp
using System;
using System.Collections.Generic;

namespace C3DTools.Core.Tables;

/// <summary>Header + rows of already-formatted cells, shared by CSV, XLSX and AutoCAD Table output.</summary>
public sealed class TableData
{
    private readonly List<string[]> _rows = new List<string[]>();

    public TableData(params string[] headers)
    {
        if (headers == null || headers.Length == 0) throw new ArgumentException("Bảng cần ít nhất một cột.", nameof(headers));
        Headers = headers;
    }

    public IReadOnlyList<string> Headers { get; }
    public IReadOnlyList<string[]> Rows => _rows;

    public void AddRow(params string[] cells)
    {
        if (cells == null || cells.Length != Headers.Count)
            throw new ArgumentException($"Dòng cần {Headers.Count} ô.", nameof(cells));
        _rows.Add(cells);
    }
}
```

`src/C3DTools.Core/Tables/CsvTableWriter.cs`

```csharp
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace C3DTools.Core.Tables;

public static class CsvTableWriter
{
    public static void Write(TableData table, Stream stream, char delimiter = ',')
    {
        if (table == null) throw new ArgumentNullException(nameof(table));
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        // BOM so Excel opens Vietnamese text as UTF-8.
        using var writer = new StreamWriter(stream, new UTF8Encoding(true), 4096, leaveOpen: true) { NewLine = "\r\n" };
        writer.WriteLine(string.Join(delimiter.ToString(), table.Headers.Select(h => Escape(h, delimiter))));
        foreach (var row in table.Rows)
            writer.WriteLine(string.Join(delimiter.ToString(), row.Select(c => Escape(c, delimiter))));
    }

    private static string Escape(string value, char delimiter)
    {
        value ??= "";
        var needsQuotes = value.IndexOf(delimiter) >= 0 || value.IndexOfAny(new[] { '"', '\r', '\n' }) >= 0;
        return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
```

`src/C3DTools.Core/Tables/NumberFormat.cs`

```csharp
using System.Globalization;

namespace C3DTools.Core.Tables;

public static class NumberFormat
{
    /// <summary>Fixed decimals with a dot separator regardless of Windows regional settings.</summary>
    public static string Fixed(double value, int decimals) =>
        value.ToString("F" + decimals, CultureInfo.InvariantCulture);
}
```

**Step 4: Run tests**

Run: `dotnet test C3DTools.Core.slnf`
Expected: PASS, 63 tests.

**Step 5: Commit**

```bash
git add src/C3DTools.Core/Tables tests/C3DTools.Core.Tests/Tables
git commit -m "feat(core): table model and UTF-8 CSV export"
```

---

### Task 9: Continuous integration for Core

Runs Core tests on every push so Core stays green independent of Windows work.

**Files:**
- Create: `.github/workflows/core.yml`

**Step 1: Write the workflow**

```yaml
name: core
on:
  push:
  pull_request:
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x
      - run: dotnet test C3DTools.Core.slnf
```

**Step 2: Verify locally**

Run: `dotnet test C3DTools.Core.slnf`
Expected: PASS, 63 tests.

**Step 3: Commit and push** (first push of the repo)

```bash
git add .github/workflows/core.yml
git commit -m "ci: run Core tests on push"
git push -u origin main
```

Then run `gh run list --repo baobeta/civil-utils --limit 1` (with `gh` switched to the `baobeta` account) and confirm the run status is `completed success`.

---

### Task 10: Civil 3D 2021 add-in skeleton (build anywhere)

This project builds on macOS, Windows and CI: the SDK pulls .NET Framework 4.8 reference assemblies from NuGet automatically, and every Autodesk reference is a compile-only package. Running it still needs Civil 3D 2021 (Task 11).

Civil 3D references come from the community package `Civil3D2021.Base` 1.0.0 (Autodesk's own `Civil3D.NET` starts at Civil 3D 2024). It is an unofficial repackage of `AeccDbMgd.dll`/`AecBaseMgd.dll` with no stated license, so it is used **compile-only** — `ExcludeAssets="runtime"` keeps it out of the build output and the bundle, and Civil 3D supplies the real DLLs at runtime. Passing `-p:UseLocalCivil3D=true` switches to the DLLs of a local Civil 3D install instead.

**Files:**
- Create: `src/C3DTools.Civil2021/C3DTools.Civil2021.csproj`
- Create: `src/C3DTools.Civil2021/Commands/HelloCommand.cs`
- Modify: `C3DTools.sln`
- Modify: `THIRD_PARTY.md`

**Step 1: Project file** — `src/C3DTools.Civil2021/C3DTools.Civil2021.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <PlatformTarget>x64</PlatformTarget>
    <RootNamespace>C3DTools.Civil2021</RootNamespace>
    <UseLocalCivil3D Condition="'$(UseLocalCivil3D)' == ''">false</UseLocalCivil3D>
    <Civil3DDir Condition="'$(Civil3DDir)' == ''">C:\Program Files\Autodesk\AutoCAD 2021</Civil3DDir>
  </PropertyGroup>

  <ItemGroup>
    <!-- Official Autodesk "AutoCAD 2021 .Net API" packages; compile-only, AutoCAD supplies them at runtime -->
    <PackageReference Include="AutoCAD.NET" Version="24.0.0" ExcludeAssets="runtime" />
    <PackageReference Include="AutoCAD.NET.Core" Version="24.0.0" ExcludeAssets="runtime" />
    <PackageReference Include="AutoCAD.NET.Model" Version="24.0.0" ExcludeAssets="runtime" />
  </ItemGroup>

  <!-- Default (CI, macOS): community package, compile-only, never shipped -->
  <ItemGroup Condition="'$(UseLocalCivil3D)' != 'true'">
    <PackageReference Include="Civil3D2021.Base" Version="1.0.0" ExcludeAssets="runtime" />
  </ItemGroup>

  <!-- -p:UseLocalCivil3D=true: DLLs from a local Civil 3D 2021 install -->
  <ItemGroup Condition="'$(UseLocalCivil3D)' == 'true'">
    <Reference Include="AeccDbMgd">
      <HintPath>$(Civil3DDir)\C3D\AeccDbMgd.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="AecBaseMgd">
      <HintPath>$(Civil3DDir)\ACA\AecBaseMgd.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\C3DTools.Core\C3DTools.Core.csproj" />
  </ItemGroup>
</Project>
```

**Step 2: First command** — `src/C3DTools.Civil2021/Commands/HelloCommand.cs`

Proves three things at once: the bundle loads, the Civil 3D API is reachable, and Core is referenced.

```csharp
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using C3DTools.Core.Stations;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(C3DTools.Civil2021.Commands.HelloCommand))]

namespace C3DTools.Civil2021.Commands;

public class HelloCommand
{
    [CommandMethod("C3DTOOLS", "CTHELLO", CommandFlags.Modal)]
    public void Hello()
    {
        var editor = AcApp.DocumentManager.MdiActiveDocument.Editor;
        var alignments = CivilApplication.ActiveDocument.GetAlignmentIds().Count;
        editor.WriteMessage(
            $"\nC3DTools 0.1 — bản vẽ có {alignments} tuyến. Ví dụ lý trình: {StationFormatter.Format(1234.5, 2)}\n");
    }
}
```

**Step 3: Add to solution and build**

```bash
dotnet sln C3DTools.sln add src/C3DTools.Civil2021/C3DTools.Civil2021.csproj
dotnet build src/C3DTools.Civil2021/C3DTools.Civil2021.csproj -c Release
ls src/C3DTools.Civil2021/bin/Release/net48/
```

Expected: `Build succeeded.` The output folder contains `C3DTools.Civil2021.dll`, `C3DTools.Core.dll` and `Newtonsoft.Json.dll`, and **no** `AcMgd.dll`, `AcDbMgd.dll`, `AeccDbMgd.dll` or `AecBaseMgd.dll`. If any Autodesk DLL appears, the compile-only settings are wrong — fix before continuing, because shipping Autodesk DLLs breaks loading and violates their license.

**Step 4: Record the compile-only dependencies** — add to the table in `THIRD_PARTY.md`:

```markdown
| AutoCAD.NET / .Core / .Model (Autodesk) | 24.0.0 | Autodesk SDK terms | No (compile-only) | — |
| Civil3D2021.Base (community repackage of Autodesk AeccDbMgd/AecBaseMgd) | 1.0.0 | None stated | No (compile-only) | — |
```

**Step 5: Commit**

```bash
git add src/C3DTools.Civil2021 C3DTools.sln THIRD_PARTY.md
git commit -m "feat(civil2021): add-in skeleton with CTHELLO command"
```

---

### Task 11: Bundle, packaging and tester install

A tester gets one zip:

```
C3DTools-<version>.zip
  C3DTools.bundle/
    PackageContents.xml
    Contents/*.dll
  install.cmd        ← double-click to install
  uninstall.cmd
  install.ps1
  THIRD_PARTY.md
```

Files downloaded from the internet carry Windows' "Mark of the Web"; .NET Framework refuses to load blocked DLLs, so AutoCAD fails with a load error. `install.ps1` therefore runs `Unblock-File` on everything it copies.

**Files:**
- Create: `bundle/PackageContents.xml`
- Create: `bundle/install.ps1`
- Create: `bundle/install.cmd`
- Create: `bundle/uninstall.cmd`
- Create: `scripts/package-bundle.ps1`

**Step 1: Generate two GUIDs** — `uuidgen` (macOS) or `[guid]::NewGuid()` (PowerShell), twice. Use them as `ProductCode` and `UpgradeCode`. Never change `UpgradeCode` afterwards.

**Step 2: `bundle/PackageContents.xml`** (PRD §7.5: RuntimeRequirements inside the ComponentEntry, SeriesMax pinned, no `*`). `AppVersion` is overwritten by the packaging script.

```xml
<?xml version="1.0" encoding="utf-8"?>
<ApplicationPackage SchemaVersion="1.0"
                    AutodeskProduct="AutoCAD"
                    ProductType="Application"
                    Name="C3DTools"
                    AppVersion="0.1.0"
                    Description="Công cụ tăng năng suất cho Civil 3D 2021"
                    Author="Bao Le"
                    ProductCode="{PRODUCT-CODE-GUID}"
                    UpgradeCode="{UPGRADE-CODE-GUID}">
  <CompanyDetails Name="C3DTools" />
  <Components Description="Civil 3D 2021">
    <ComponentEntry AppName="C3DTools"
                    ModuleName="./Contents/C3DTools.Civil2021.dll"
                    AppDescription="C3DTools cho Civil 3D 2021"
                    LoadOnAutoCADStartup="True">
      <RuntimeRequirements OS="Win64" Platform="Civil3D" SeriesMin="R24.0" SeriesMax="R24.0" />
      <Commands GroupName="C3DTOOLS">
        <Command Global="CTHELLO" Local="CTHELLO" />
      </Commands>
    </ComponentEntry>
  </Components>
</ApplicationPackage>
```

**Step 3: `bundle/install.ps1`** — installs (or with `-Uninstall`, removes) the per-user bundle. Messages are ASCII-only so Windows PowerShell 5.1 shows them correctly without a BOM.

```powershell
param([switch]$Uninstall)
$ErrorActionPreference = "Stop"

$target = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\C3DTools.bundle"

if (Get-Process acad -ErrorAction SilentlyContinue) {
    throw "Close Civil 3D before installing or uninstalling C3DTools."
}

if (Test-Path $target) { Remove-Item $target -Recurse -Force }

if ($Uninstall) {
    Write-Host "C3DTools removed."
    return
}

Copy-Item (Join-Path $PSScriptRoot "C3DTools.bundle") $target -Recurse
# Downloaded files are blocked by Windows; AutoCAD cannot load blocked DLLs.
Get-ChildItem $target -Recurse -File | Unblock-File
Write-Host "C3DTools installed to $target"
Write-Host "Open Civil 3D 2021 and type CTHELLO."
```

`bundle/install.cmd`:

```bat
@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
pause
```

`bundle/uninstall.cmd`:

```bat
@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -Uninstall
pause
```

**Step 4: `scripts/package-bundle.ps1`** — builds and produces `artifacts/C3DTools-<version>/` plus the zip. Runs on Windows PowerShell 5.1, pwsh 7 on Windows (CI), and pwsh on macOS (`brew install --cask powershell`) for a local dry run.

```powershell
param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0"
)
$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $root "artifacts"
$stage = Join-Path $artifacts "C3DTools-$Version"
$bundle = Join-Path $stage "C3DTools.bundle"
$contents = Join-Path $bundle "Contents"
$buildOut = Join-Path $root "src/C3DTools.Civil2021/bin/$Configuration/net48"

dotnet build (Join-Path $root "src/C3DTools.Civil2021/C3DTools.Civil2021.csproj") -c $Configuration -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $contents | Out-Null

# -Encoding UTF8 on both sides: the manifest contains Vietnamese text.
$manifest = Get-Content (Join-Path $root "bundle/PackageContents.xml") -Raw -Encoding UTF8
$manifest = $manifest -replace 'AppVersion="[^"]*"', "AppVersion=`"$Version`""
Set-Content (Join-Path $bundle "PackageContents.xml") $manifest -Encoding UTF8

Copy-Item (Join-Path $buildOut "*.dll") $contents
foreach ($f in "install.ps1", "install.cmd", "uninstall.cmd") {
    Copy-Item (Join-Path $root "bundle/$f") $stage
}
Copy-Item (Join-Path $root "THIRD_PARTY.md") $stage

$autodesk = Get-ChildItem $contents -Filter *.dll | Where-Object { $_.Name -match '^(Ac|Aec|Adw|AdUi)' }
if ($autodesk) { throw "Autodesk DLLs must not be packaged: $($autodesk.Name -join ', ')" }

$zip = Join-Path $artifacts "C3DTools-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip
Write-Host "Package: $zip"
```

Also add `artifacts/` to `.gitignore`.

**Step 5: Package locally**

Run: `pwsh scripts/package-bundle.ps1` (macOS needs PowerShell: `brew install --cask powershell`).
Expected: `Package: .../artifacts/C3DTools-0.1.0.zip`; `unzip -l artifacts/C3DTools-0.1.0.zip` lists `C3DTools.bundle/PackageContents.xml`, `C3DTools.bundle/Contents/C3DTools.Civil2021.dll`, `install.cmd`, `uninstall.cmd`, `install.ps1`, `THIRD_PARTY.md`.

**Step 6: Commit**

```bash
git add bundle scripts .gitignore
git commit -m "build: bundle packaging and tester install scripts"
```

**Step 7 (Windows + Civil 3D 2021): Verify install manually**

1. Copy the zip to the Windows machine (or download it from CI once Task 13 is done), extract, double-click `install.cmd`. Expected: `C3DTools installed to ...C3DTools.bundle`.
2. Start **Civil 3D 2021**, open any DWG, type `CTHELLO`. Expected: `C3DTools 0.1 — bản vẽ có N tuyến. Ví dụ lý trình: Km1+234.50`.
3. Start **AutoCAD 2021** (plain, if installed), type `CTHELLO`. Expected: `Unknown command` — the bundle must not load there.
4. If a security prompt appears on load, record it in the spike report (SECURELOAD / code signing becomes a Plan 4 packaging item).
5. Close Civil 3D, double-click `uninstall.cmd`, restart Civil 3D. Expected: `CTHELLO` is unknown.

---

### Task 12 (Windows + Civil 3D 2021): Spike (PRD §10, weeks 1–2)

The spike answers the 7 PRD questions **before** C-01…C-08 are locked. Spike code is throwaway: put it in `src/C3DTools.Civil2021/Spikes/` as `CTSPIKE…` commands, and delete that folder once the report is written. Do not write unit tests for spikes; the output is the report.

API signatures below are the ones to look for; confirm each against the Civil 3D 2021 API reference and SnoopDbCivil3D before relying on it — finding out is the point of the spike.

**Files:**
- Create: `docs/spikes/2026-10-spike-report.md`
- Create (temporary): `src/C3DTools.Civil2021/Spikes/*.cs`

**Step 1: Create the report skeleton** — `docs/spikes/2026-10-spike-report.md`

```markdown
# Spike report — Civil 3D 2021 API (PRD §10)

Civil 3D build tested: ________ (from ABOUT; must be 2021.3 or later)

| # | Question | Method | Result | Decision |
| --- | --- | --- | --- | --- |
| 1 | Bundle loads on Civil 3D 2021, not on plain AutoCAD 2021 | Task 11 step 7 | | |
| 2 | Preview + single undo with COGO points and alignments | | | |
| 3 | Alignment from polyline with arcs; profile from surface; profile view | | | |
| 4 | Sample line at a given station | | | |
| 5 | Material section areas per station; own volumes vs Civil 3D volume report | | | |
| 6 | Pipe diameter, inverts, slope; structure rim/sump | | | |
| 7 | XLSX with Vietnamese text; AutoCAD Table | | | |

## Scope changes for C-01…C-08
```

**Step 2: Run each spike and fill a row.** What to try:

| # | Starting point in the API |
| --- | --- |
| 2 | `CivilApplication.ActiveDocument.CogoPoints.Add(Point3d, string, bool)` inside one `Transaction`; build the "plan" in a transaction you `Abort()`, then apply in a second one you `Commit()`. After the command, one `U` must remove everything. |
| 3 | `Alignment.Create(CivilDocument, PolylineOptions, string, ObjectId siteId, ObjectId layerId, ObjectId styleId, ObjectId labelSetId)` with `PolylineOptions { PlineId, AddCurvesBetweenTangents = false, EraseExistingEntities = false }`; `Profile.CreateFromSurface(...)`; `ProfileView.Create(...)`. Test a polyline containing arcs. |
| 4 | `SampleLineGroup.Create(name, alignmentId)`; `SampleLine.Create(name, groupId, Point2dCollection)` with the two end points computed by `alignment.PointLocation(station, ±offset, ref x, ref y)`. If no station-based overload exists, this points-based route is the fallback. Feed stations from `StationPlanner`. |
| 5 | Material list / `SampleLine.GetMaterialSectionId(...)` → section area per material. Feed areas into `AverageEndArea.Compute` and compare with Civil 3D's Volume Report on the same DWG. Record the max difference. If areas are not reachable, try the report XML fallback (PRD §11). |
| 6 | `Pipe`: `InnerDiameterOrWidth`, `WallThickness`, `Length2D`, `StartPoint`/`EndPoint`, `Slope`; `Structure`: `RimElevation`, `SumpElevation`. Confirm whether `StartPoint.Z` is centerline or invert, and map to `PipeData` for `PipeChecker`. |
| 7 | ClosedXML writing a sheet with Vietnamese headers (check it loads in net48 inside AutoCAD without DLL conflicts); `Autodesk.AutoCAD.DatabaseServices.Table` filled from a `TableData`. |

**Step 3: Record decisions.** For every "no" or "partial" result, write the scope change (e.g. "C-06 moves to P1" per PRD §11). The report is the input to Plan 2.

**Step 4: Remove spike code and commit the report**

```bash
git rm -r src/C3DTools.Civil2021/Spikes
git add docs/spikes/2026-10-spike-report.md
git commit -m "docs: Civil 3D 2021 API spike report"
```

---

### Task 13: CI build and downloadable releases

Can be done right after Task 11 (it does not depend on the spike). Replaces the Core-only workflow from Task 9 with one Windows job that tests Core, builds the add-in and packages the zip.

- Every push / PR: the zip is attached to the run as an artifact named `C3DTools-<version>` (download needs a GitHub login; kept 90 days).
- Tag `v0.1.0` etc.: the zip is published as a **GitHub Release** — the link to send testers.
- CI versions are numeric (`0.1.0.<run number>`) because `PackageContents.xml` `AppVersion` and assembly versions must be numeric.

**Files:**
- Delete: `.github/workflows/core.yml`
- Create: `.github/workflows/build.yml`
- Create: `docs/testing.md`

**Step 1: `.github/workflows/build.yml`**

```yaml
name: build
on:
  push:
    branches: [main]
    tags: ['v*']
  pull_request:

jobs:
  build:
    runs-on: windows-latest
    permissions:
      contents: write   # needed only to create releases on tags
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - name: Version
        id: ver
        shell: pwsh
        run: |
          if ($env:GITHUB_REF_TYPE -eq 'tag') { $v = $env:GITHUB_REF_NAME.TrimStart('v') }
          else { $v = "0.1.0.$env:GITHUB_RUN_NUMBER" }
          "version=$v" >> $env:GITHUB_OUTPUT

      - name: Test Core
        run: dotnet test C3DTools.Core.slnf

      - name: Package bundle
        shell: pwsh
        run: ./scripts/package-bundle.ps1 -Version ${{ steps.ver.outputs.version }}

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: C3DTools-${{ steps.ver.outputs.version }}
          path: artifacts/C3DTools-${{ steps.ver.outputs.version }}/

      - name: Publish release
        if: github.ref_type == 'tag'
        env:
          GH_TOKEN: ${{ github.token }}
        run: >
          gh release create ${{ github.ref_name }}
          artifacts/C3DTools-${{ steps.ver.outputs.version }}.zip
          --title "C3DTools ${{ github.ref_name }}"
          --generate-notes
```

The artifact uploads the *folder*, so GitHub's download is a single zip (uploading the zip itself would produce a zip inside a zip).

**Step 2: `docs/testing.md`** — the page to send testers

```markdown
# Cài C3DTools để thử nghiệm

Yêu cầu: Windows 64-bit, Civil 3D 2021 (update 2021.3 trở lên).

1. Tải file `C3DTools-<phiên bản>.zip` ở mục **Assets** của bản mới nhất: https://github.com/baobeta/civil-utils/releases
2. Giải nén (chuột phải → Extract All).
3. Đóng Civil 3D, bấm đúp `install.cmd`.
4. Mở Civil 3D 2021, gõ lệnh `CTHELLO`.

Gỡ cài đặt: đóng Civil 3D, bấm đúp `uninstall.cmd`.

Báo lỗi: gửi ảnh chụp dòng lệnh, file DWG (nếu được) và phiên bản trong tên file zip.
```

**Step 3: Replace the workflow and commit**

```bash
git rm .github/workflows/core.yml
git add .github/workflows/build.yml docs/testing.md
git commit -m "ci: build, package and release the Civil 3D bundle on Windows"
git push
```

**Step 4: Verify a push build**

Run: `gh run list --repo baobeta/civil-utils --workflow build --limit 1` (with `gh` switched to `baobeta`).
Expected: `completed  success`. Then `gh run download <run-id> --repo baobeta/civil-utils -D /tmp/c3d-ci && ls -R /tmp/c3d-ci` lists `C3DTools.bundle/Contents/C3DTools.Civil2021.dll` and `install.cmd`.

**Step 5: Verify a release**

```bash
git tag v0.1.0
git push origin v0.1.0
```

Expected: `gh release view v0.1.0 --repo baobeta/civil-utils` shows asset `C3DTools-0.1.0.zip`. On the Windows machine, repeat Task 11 Step 7 using that downloaded zip — this is the exact path a tester takes, including the Mark-of-the-Web unblock.

---

## Done criteria for this plan

- `dotnet test C3DTools.Core.slnf` passes (63 tests) locally and in CI.
- Every push to `main` produces a `C3DTools-<version>` artifact; tag `v0.1.0` produces a GitHub Release with `C3DTools-0.1.0.zip`.
- The released zip, installed with `install.cmd` on a Civil 3D 2021 machine, makes `CTHELLO` work; `CTHELLO` is unknown in plain AutoCAD 2021 and after `uninstall.cmd`.
- No Autodesk DLL is inside the zip.
- `docs/spikes/2026-10-spike-report.md` has all 7 rows filled with a decision.
- `THIRD_PARTY.md` lists every shipped and compile-only dependency.
