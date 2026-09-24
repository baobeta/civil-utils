# C3DTools command-suite implementation plan

Date: 2026-09-24
Target: Civil 3D 2021 R24.0, `net48`, Windows x64

## Scope decision

The original PRD is not present in the repository. This plan therefore makes the C-01…C-08 command names and behavior an explicit implementation contract based on the README, the completed foundation plan, existing Core APIs, and the Civil 3D 2021 spike report.

| ID | Command | Implemented behavior | Drawing output |
| --- | --- | --- | --- |
| C-01 | `CTALIGN` | Select an open LWPOLYLINE, choose a name, preview `Alignment.Create`, apply or cancel. Direction follows polyline vertex order. | Civil 3D Alignment |
| C-02 | `CTDIEM` | Select a CSV/TXT point file, choose delimiter and column order, validate with Core, preserve point numbers/descriptions, skip existing numbers, preview and apply. | COGO points |
| C-03 | `CTPROFILE` | Select an Alignment and surface, create a surface profile and profile view with 2021-compatible style IDs. | Profile + Profile View |
| C-04 | `CTEXPORT` | Export Alignment, Surface, or Pipe Network inventory to UTF-8 BOM CSV and optionally place an AutoCAD Table. | CSV and optional Table |
| C-05 | `CTNHAC` | Label an Alignment at a station range/interval using `StationFormatter`. | DBText station labels |
| C-06 | `CTCOC` | Create a Sample Line Group and explicit ±offset sample lines from `StationPlanner`. | Sample lines |
| C-07 | `CTKHOILUONG` | Sample two TIN surfaces along an Alignment, integrate cut/fill cross-section areas, then calculate average-end-area volumes. | AutoCAD Table + optional CSV |
| C-08 | `CTCONG` | Read a selected Pipe, derive wall thickness, adapt ground/invert data, apply project slope/cover thresholds, and report results. | AutoCAD Table + optional CSV |

`CTHELLO` remains the bundle/version smoke command. `CTCONFIG` is an additional setup utility for project decimals and mandatory pipe thresholds; it is not assigned a C-number because the missing PRD leaves C-01/C-04 ambiguous.

## Shared rules

- User messages are Vietnamese; identifiers and parsing use invariant culture.
- Every drawing-changing command creates objects inside one transaction.
- Preview is shown before commit; Cancel aborts the transaction.
- One `U` after a successful command removes all drawing changes from that command.
- CSV files are external outputs and are not affected by `U`.
- Commands fail with a readable Vietnamese message rather than leaving partial drawing objects.

## Architecture

```text
Commands/
  ConfigurationCommands.cs CTCONFIG
  AlignmentCommands.cs   CTALIGN, CTPROFILE
  PointCommands.cs       CTDIEM
  StationCommands.cs     CTNHAC, CTCOC
  VolumeCommands.cs      CTKHOILUONG
  DrainageCommands.cs    CTCONG
  ExportCommands.cs      CTEXPORT
Services/
  HostServices.cs        prompts, selections, object/style helpers
  CivilAdapters.cs       surface sampling and pipe adaptation
  PresetService.cs       versioned project configuration
  TableAndFileServices.cs AutoCAD table, CSV, text-file input
  CommandRunner.cs       cancellation and user-facing errors
```

Business-only calculations remain in Core. `CrossSectionArea` is added to Core with unit tests because C-07 needs correct trapezoidal integration when a design profile crosses the existing profile.

## Verification phases

1. **Pure logic:** run all Core tests, including cut/fill crossing and trapezoid cases.
2. **Compile:** build against both the local Civil 3D 2021 assemblies and the CI compile-only packages.
3. **Packaging:** build `C3DTools-0.2.0.1.zip`; ensure no Autodesk DLL is included and all commands appear in `PackageContents.xml`.
4. **Civil smoke/manual:** install the local zip and test each command in a disposable metric drawing.
5. **Undo:** after every apply test, run one `U` and verify the expected object count returns to its pre-command value.

## Known scope limits

- C-07 is a tool-owned cross-section calculation, not Civil 3D material-by-material QTO. The spike moved that comparison to P1.
- C-08 asks whether Pipe endpoint Z is centerline or invert because a project network is required to verify the convention; the command exposes this as a prompt.
- XLSX remains out of scope for this build. CSV preserves Vietnamese with UTF-8 BOM; AutoCAD Table is supported directly.
