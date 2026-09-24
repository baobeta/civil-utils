# C3DTools

Productivity tools for **Autodesk Civil 3D 2021**, built for Vietnamese road and drainage projects: survey point import checks, station (lý trình) labels, sample lines, cut/fill volumes and pipe checks.

> **Status: 0.2 (curve element tools).** The Core logic is done and tested. The add-in has `CTHELLO` plus the TCVN curve element tools below. Commands C-01…C-08 are waiting on the Civil 3D API spike ([docs/spikes/2026-10-spike-report.md](docs/spikes/2026-10-spike-report.md)).

## Commands

Ribbon tab **C3DTools** (panel **Tuyến**), or type the command directly:

| Command | Does |
| --- | --- |
| `CTHELLO` | Checks that the add-in loads |
| `CTYTC` | Curve elements dialog (Yếu tố cong): pick a polyline or an existing alignment, edit R/L1/L2/Wb/Wl per PI, preview and apply TCVN curves, boxes, stakes and CSV |
| `CTYTCMAU` | Imports TCVN curve styles, label set and abbreviations (Mẫu TCVN) |
| `CTYTCBANG` | Curve summary AutoCAD Table plus CSV for an alignment or polyline (Bảng cong) |

## Install (testers)

Testers should follow [docs/testing.md](docs/testing.md), which is in Vietnamese. In short: download `C3DTools-<version>.zip` from [Releases](https://github.com/baobeta/civil-utils/releases), extract it, close Civil 3D, double-click `install.cmd`, then type `CTHELLO` in Civil 3D 2021.

Requires Windows x64 and Civil 3D 2021 with update 2021.3 or later. The bundle doesn't load in plain AutoCAD.

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
| `Stations`: format and parse `Km1+234.56`; plan sample line stations | C-05, C-06, C-07 |
| `Points`: validate point files (duplicate numbers, swapped N/E, comma decimals) | C-02 |
| `Volumes`: average-end-area cut/fill | C-07 |
| `Drainage`: pipe slope and cover checks against preset rules | C-08 |
| `Curves`: TCVN 4054 curve elements (A, R, T1, T2, P, K), rule checks, route design and station formatting | C-09, C-10 |
| `Presets`: versioned JSON project presets | all |
| `Tables`: table model, UTF-8 CSV with BOM | exports |

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
pwsh scripts/package-bundle.ps1 -Version 0.2.0
```

On macOS, if the Homebrew `powershell` cask isn't available, run `dotnet tool install --global PowerShell`.

**The zip must never contain Autodesk DLLs.** Every Autodesk package reference uses `ExcludeAssets="runtime"`, and `package-bundle.ps1` fails if an `Ac*`, `Aec*`, `Adw*` or `AdUi*` DLL ends up in the bundle.

The bundle also carries `bundle/Resources/` (versioned JSON presets such as `tcvn4054.preset.json`, and optionally a `C3DTools-TCVN.dwg` template for `CTYTCMAU`'s style import) alongside the add-in DLLs. CI builds the zip on every push.

## CI and releases

The GitHub Actions workflow [build.yml](.github/workflows/build.yml) runs on `windows-latest`:

- **Every push to `main` and every PR:** tests Core, builds the add-in and uploads the bundle as an artifact.
- **Tag `v*`** (for example `git tag v0.2.0 && git push origin v0.2.0`): also publishes a GitHub Release with `C3DTools-<version>.zip`.

## Conventions

- Parse and format numbers with `CultureInfo.InvariantCulture`. Vietnamese Windows uses a comma as the decimal separator.
- User-facing messages are in Vietnamese; code and identifiers are in English. Command names start with `CT`.
- Every command that creates or changes objects must offer a preview, cancel and a single undo.
- Every shipped or compile-only dependency, and any reused code, is recorded in [THIRD_PARTY.md](THIRD_PARTY.md) before merge.

More detail for contributors, including notes for AI coding agents, is in [CLAUDE.md](CLAUDE.md).
