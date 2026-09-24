# CLAUDE.md

C3DTools: a .NET add-in for **Civil 3D 2021** (PRD 1.0). Implementation plans live in `docs/plans/`.

## Target stack

| Item | Value |
| --- | --- |
| Host | Civil 3D 2021, update 2021.3 or later, R24.0, Windows x64 |
| Host project | `net48`, `<PlatformTarget>x64</PlatformTarget>` |
| Core project | `netstandard2.0`, no Autodesk references, unit-tested on any OS |
| AutoCAD refs | NuGet `AutoCAD.NET`, `AutoCAD.NET.Core`, `AutoCAD.NET.Model` **24.0.0**, all `ExcludeAssets="runtime"` |
| Civil 3D refs | Community NuGet `Civil3D2021.Base` 1.0.0, `ExcludeAssets="runtime"`. It's a compile-only reference and must never be shipped. `-p:UseLocalCivil3D=true` uses the local install instead: `<install>\C3D\AeccDbMgd.dll` and `<install>\ACA\AecBaseMgd.dll` |
| Build and release | GitHub Actions on `windows-latest` produces `C3DTools-<version>.zip`; a `v*` tag publishes a GitHub Release. The zip must never contain Autodesk DLLs |
| API docs | [Civil 3D 2021 API Developer's Guide](https://help.autodesk.com/view/CIV3D/2021/ENU/?contextId=developer-guide) |

## AutoCAD / Civil 3D skills

`.claude/skills/` has `autocad-api`, `civil3d-api`, `autocad-scaffold`, `autocad-code-sleuth` and `autocad-learnings`, taken from ADN-DevTech/acad-api-skill (MIT). They are written for 2027 / .NET 10. For this repo, override them as follows:

- **Framework:** use `net48`, not `net10.0-windows`. Skip `FrameworkReference Microsoft.WindowsDesktop.App`, `GenerateTargetFrameworkAttribute` and `.deps.json`, which net48 doesn't produce.
- **Versions:** use AutoCAD packages `24.0.0`, not `26.0.0`. Don't use the `Civil3D.NET` NuGet: its first version, 13.6, is Civil 3D 2024.
- **Bundle:** `Platform="Civil3D"`, `SeriesMin="R24.0" SeriesMax="R24.0"`, with `RuntimeRequirements` inside the `ComponentEntry` and never `*` for SeriesMax.
- **Desktop package:** the host uses the full `AutoCAD.NET` package, because the ribbon needs `AdWindows`. The skills' "Core + Model only" rule is for Design Automation and accoreconsole, which are out of scope (PRD §5.3).
- **Help URLs:** for API reference, swap `/2027/` for `/2021/`. Signatures and features can differ, so check them against the 2021 guide or SnoopDbCivil3D.
- **Learnings:** record newly verified 2021-specific API behavior with the `autocad-learnings` skill.

## Conventions

- Parse and format numbers with `CultureInfo.InvariantCulture`. Vietnamese Windows uses a comma as the decimal separator.
- Business rules go in Core. Host code only reads and writes drawing objects and calls Core.
- User-facing messages are in Vietnamese; code and identifiers are in English. Command names start with `CT`.
- Every command that creates or changes objects must offer a preview, cancel, and a single undo (PRD §6).
- Reusing third-party code requires a written license and an entry in `THIRD_PARTY.md` (PRD §8.2).
