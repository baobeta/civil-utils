# C3DTools

Productivity tools for **Autodesk Civil 3D 2021**, built for Vietnamese road and drainage projects: survey point import checks, station (lý trình) labels, sample lines, cut/fill volumes and pipe checks.

> **Status: 0.3 (daily-use tools).** The Core logic is done and tested on macOS and CI. The add-in has `CTHELLO`, the TCVN curve element tools from 0.2 and the ten 0.3/0.4 commands below, each with its own dialog, preview, apply and single undo. None of the 0.3/0.4 commands has run inside Civil 3D yet: [docs/test-plan-0.3-0.4.md](docs/test-plan-0.3-0.4.md) is the Windows test plan, and it lists the Civil 3D API calls still to confirm. The command-line suite C-01…C-08 (`CTALIGN`, `CTDIEM`, `CTPROFILE`, `CTEXPORT`, `CTNHAC`, `CTCOC`, `CTKHOILUONG`, `CTCONG`, `CTCONFIG`) from the [command-suite plan](docs/plans/2026-09-24-command-suite.md) is also included; see the second table below.

## Commands

Ribbon tab **C3DTools**, grouped by panel, or type the command directly. The first command of each panel is its large button. `CTHELLO` checks that the add-in loads.

| Panel | Command | Button | Does |
| --- | --- | --- | --- |
| Tuyến | `CTYTC` | Yếu tố cong | Curve elements dialog: pick a polyline or an existing alignment, edit R/L1/L2/Wb/Wl per PI, preview and apply TCVN curves, boxes, stakes, CSV and Excel |
| | `CTYTCMAU` | Mẫu TCVN | Imports TCVN curve styles, label set and abbreviations |
| | `CTYTCBANG` | Bảng cong | Curve summary as an AutoCAD Table, CSV or Excel for an alignment or polyline |
| | `CTTOADO` | Toạ độ cọc | Stake coordinate table of an alignment (interval, curve and extra stakes; Z from a surface) as a Table, CSV, Excel or COGO points |
| Trắc dọc | `CTTRACDOC` | Bảng trắc dọc | Vietnamese profile data table drawn under a profile view (stakes, distances, stations, ground/design elevations, cut/fill, grades), CSV and Excel |
| | `CTCONGDUNG` | Cong đứng | Vertical curve elements (i1, i2, A, R, K, T, E, high/low point) of a design profile, checked against the preset; box above each PVI, Table, CSV, Excel |
| Trắc ngang | `CTTRACNGANG` | Bảng trắc ngang | Data table with cut/fill areas under each section view (or the whole section view group), CSV and Excel |
| | `CTXEPTRANG` | Xếp trang | Moves section views and their tables onto print sheets in station order and draws sheet frames and titles |
| Địa hình | `CTMATDIA` | Mặt địa hình | Deletes long or outside-boundary TIN triangles, or labels contour elevations where picked lines cross them |
| | `CTVN2000` | VN-2000 | Moves objects or all COGO points from one VN-2000 central meridian / zone to another |
| Thoát nước | `CTBANGCONG` | Bảng cống | Culvert schedule of the pipe networks crossing an alignment as a Table, CSV or Excel |
| Bản vẽ | `CTFONT` | Chuyển font | Converts Vietnamese text between TCVN3, VNI and Unicode for a selection or the whole drawing |
| | `CTLAYER` | Chuẩn layer | Moves objects to the preset's standard layers, creates them and optionally purges empty layers |

## Install (testers)

Testers should follow [docs/testing.md](docs/testing.md), which is in Vietnamese. In short: download `C3DTools-<version>.zip` from [Releases](https://github.com/baobeta/civil-utils/releases), extract it, close Civil 3D, double-click `install.cmd`, then type `CTHELLO` in Civil 3D 2021.

Requires Windows x64 and Civil 3D 2021 with update 2021.3 or later. The bundle doesn't load in plain AutoCAD.

### Command suite (C-01…C-08, command line)

| Command | Purpose |
| --- | --- |
| `CTHELLO` | Confirm that the local build loads and list available commands. |
| `CTCONFIG` | Save project display precision and mandatory pipe slope/cover rules. |
| `CTALIGN` | Create a Civil 3D Alignment from an open LWPOLYLINE. |
| `CTDIEM` | Validate and import a CSV/TXT survey point file as COGO points. |
| `CTPROFILE` | Create a surface profile and profile view for an Alignment. |
| `CTEXPORT` | Export Alignment, Surface, or Pipe Network inventory to CSV and an optional AutoCAD Table. |
| `CTNHAC` | Create station labels along an Alignment. |
| `CTCOC` | Create a Sample Line Group at planned stations and explicit offsets. |
| `CTKHOILUONG` | Sample two surfaces, calculate cut/fill, and create a table/optional CSV. |
| `CTCONG` | Check a selected Pipe against the project rules saved by `CTCONFIG`. |

The command contract and architectural decisions are recorded in [docs/plans/2026-09-24-command-suite.md](docs/plans/2026-09-24-command-suite.md). Follow [docs/manual-test-command-suite.md](docs/manual-test-command-suite.md) for local QA.

## Repository layout

```
src/
  C3DTools.Core/         netstandard2.0; business rules, no Autodesk references
  C3DTools.Civil2021/    net48 x64 add-in; commands and Civil 3D adapters
tests/
  C3DTools.Core.Tests/   xUnit tests for Core; run on any OS
bundle/                  PackageContents.xml, install/uninstall scripts
scripts/                 package-bundle.ps1
docs/plans/              implementation plans
docs/spikes/             Civil 3D 2021 API spike report
```

Host code only reads and writes drawing objects and calls Core. Everything that can be computed without Civil 3D lives in Core, so it's unit-tested on macOS and CI.

| Core area | Used by |
| --- | --- |
| `Stations`: format and parse `Km1+234.56`; plan sample line stations; stake lists and coordinate tables | C-05, C-06, C-07, `CTTOADO` |
| `Points`: validate point files (duplicate numbers, swapped N/E, comma decimals) | C-02 |
| `Volumes`: average-end-area cut/fill | C-07 |
| `Drainage`: pipe slope and cover checks against preset rules; culvert schedule | C-08, `CTBANGCONG` |
| `Curves`: TCVN 4054 curve elements (A, R, T1, T2, P, K), rule checks, route design and station formatting | C-09, C-10 |
| `Text`: TCVN3 / VNI / Unicode codec and encoding detection | `CTFONT` |
| `Geodesy`: transverse Mercator, VN-2000 meridian and zone conversion | `CTVN2000` |
| `Profiles`: vertical curve elements and checks, profile data table and layout | `CTCONGDUNG`, `CTTRACDOC` |
| `Sections`: section cut/fill areas, section table layout, sheet packing | `CTTRACNGANG`, `CTXEPTRANG` |
| `Surfaces`: triangle filter (edge length, boundary), contour label placement | `CTMATDIA` |
| `Drawing`: layer mapping rules | `CTLAYER` |
| `Ui`: remembered dialog options (size, last choices) | all dialogs |
| `Presets`: versioned JSON project presets | all |
| `Tables`: table model, UTF-8 CSV with BOM, dependency-free XLSX | exports |

## Development

You need the .NET SDK 8 or later. Visual Studio 2022 on Windows is optional and only needed for debugging inside Civil 3D.

```bash
# Core tests (any OS)
dotnet test C3DTools.Core.slnf

# Add-in (any OS; Autodesk references come from NuGet, compile-only)
dotnet build src/C3DTools.Civil2021/C3DTools.Civil2021.csproj -c Release

# Build against a local Civil 3D install instead of the community package
dotnet build src/C3DTools.Civil2021/C3DTools.Civil2021.csproj -c Release -p:UseLocalCivil3D=true
# add -p:Civil3DDir="D:\Autodesk\AutoCAD 2021" if Civil 3D is not in the default folder

# Package bundle + zip into artifacts/ (needs PowerShell 7 or Windows PowerShell 5.1)
pwsh scripts/package-bundle.ps1 -Version 0.3.0

# Local command-suite package (no release/tag required)
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-bundle.ps1 -Version 0.2.0.1
```

On macOS, if the Homebrew `powershell` cask isn't available, run `dotnet tool install --global PowerShell`.

**The zip must never contain Autodesk DLLs.** Every Autodesk package reference uses `ExcludeAssets="runtime"`, and `package-bundle.ps1` fails if an `Ac*`, `Aec*`, `Adw*` or `AdUi*` DLL ends up in the bundle.

The bundle also carries `bundle/Resources/` (versioned JSON presets such as `tcvn4054.preset.json`, and optionally a `C3DTools-TCVN.dwg` template for `CTYTCMAU`'s style import) alongside the add-in DLLs. CI builds the zip on every push.

## CI and releases

The GitHub Actions workflow [build.yml](.github/workflows/build.yml) runs on `windows-latest`:

- **Every push to `main` and every PR:** tests Core, builds the add-in and uploads the bundle as an artifact.
- **Tag `v*`** (for example `git tag v0.3.0 && git push origin v0.3.0`): also publishes a GitHub Release with `C3DTools-<version>.zip`.

## Conventions

- Parse and format numbers with `CultureInfo.InvariantCulture`. Vietnamese Windows uses a comma as the decimal separator.
- User-facing messages are in Vietnamese; code and identifiers are in English. Command names start with `CT`.
- Every command that creates or changes objects must offer a preview, cancel and a single undo.
- Every shipped or compile-only dependency, and any reused code, is recorded in [THIRD_PARTY.md](THIRD_PARTY.md) before merge.

More detail for contributors, including notes for AI coding agents, is in [CLAUDE.md](CLAUDE.md).
