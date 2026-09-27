# AND Design Route Workflow Implementation Plan (0.5)

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Tasks 1–9 were implemented on branch `feat/and-design-route-workflow` on 2026-09-27 (macOS, Core tests green, host Release build at 0 warnings); their steps are kept so the work can be re-derived or reviewed. Tasks 10–12 are open.

**Goal:** give C3DTools the four dialogs of AND Design's route workflow — create route, curve elements + superelevation/widening, generate/insert stakes, rename stakes — each with an easy WPF dialog, preview/apply/single undo, on Civil 3D 2021.

**Architecture:** unchanged from 0.3/0.4. Core (netstandard2.0) holds every rule and every dialog's view model (`INotifyPropertyChanged`, unit-tested on macOS). Host (net48, code-only WPF on `ToolWindow`) reads the drawing, binds the windows, writes in one transaction. Stakes are Civil 3D **Sample Lines** in a Sample Line Group (the object CTTRACNGANG/CTTOADO already read), named by the stake. Curve inputs that AND Design edits per PI (speed, superelevation, runoffs) live on `CurveInput` and are stored in the CTYTC XData tag as text, so a rerun reloads them.

**Tech Stack:** as 0.4 (net48 x64 host, `AutoCAD.NET` 24.0.0 + `Civil3D2021.Base` compile-only, xUnit Core tests). Civil 3D 2021 members used for the first time, verified by reflection over the NuGet DLLs (MetadataLoadContext probe): `Alignment.DesignSpeeds.Add`, `Alignment.UseDesignSpeed`, `Alignment.SuperelevationCurves.AddUserDefinedCurve`, `SuperelevationCurve.CriticalStations.Add/GetCriticalStationAt`, `SuperelevationCriticalStation.SetSlope`, `Profile.PVIs` / `ProfilePVI.Station` (set), `AssemblyCollection.ImportAssembly(name, Database, sourceName, Point3d)`, `SampleLine.Station/Name/GroupId`, `Alignment.StationOffset`.

---

## Mapping AND Design → C3DTools

| AND Design dialog | C3DTools | Core | Host |
| --- | --- | --- | --- |
| Tạo tuyến cho bình đồ mới | `CTTUYEN` | `RouteCreationSession` | `RouteCreateWindow`, `RouteCreateCommand`, `RouteTag` |
| Hiệu chỉnh yếu tố cong và thông số siêu cao | `CTYTC` detail panel | `CurveRow` detail props, `CurveLimits`, `SuperelevationPlanner`, `EdgeLineBuilder`, `PiEditor`, `StationShift`, `RouteGeometry`, `CurveInputText` | `CurveDesignWindow.BuildDetail`, `EdgeWriter`, `SuperelevationWriter`, `ProfileShifter`, `RouteWriter` |
| Phát sinh cọc | `CTPHATCOC` | `StakePlanner`, `StakeGenerationSession`, `RouteStake`, `StakeClassifier` | `StakeGenerateWindow`, `StakeCommands.RunGenerate`, `SampleLineStakes`, `AlignmentCurves` |
| Đánh lại toàn bộ tên cọc | `CTDANHCOC` | `StakeNamer`, `StakeNamingOptions`, `StakeRenameSession` | `StakeRenameWindow`, `StakeCommands.RunRename` |

**Decisions taken with the user (2026-09-27):**

- "Cọc H liên tục" = detail numbers run on past H stakes (C1, C2, H1, C3). Off: C1, C2, H1, C1.
- "Không đánh số quay lại khi TT>=100" = once the detail number in a km reached 100, the next Km stake does not restart at C1. **To confirm with testers** (listed in `docs/testing.md`).
- In scope besides the four dialogs: "Hiệu chỉnh góc chuyển hướng" (rotate the route after a PI), "Tạo polyline các đoạn nối" (widened edge polylines), "Dồn dịch đỉnh trắc dọc phía sau" (shift layout-profile PVIs), assembly selection ("Tệp mặt cắt" → `ImportAssembly`).
- Out of scope: "Tuyến kênh" naming, corridors, AND Design's "Tệp dữ liệu trắc dọc-trắc ngang" import.

**UI rules:** the six rules of `2026-09-25-release-0.3-0.4-implementation.md` apply unchanged.

---

## Task order

| Task | Item | Where | State |
| --- | --- | --- | --- |
| T1 | Stake model + `StakeNamer` (all CTDANHCOC options) | Core | done |
| T2 | `StakePlanner` (generate / replace / insert) + `StakeClassifier` | Core | done |
| T3 | `StakeGenerationSession`, `StakeRenameSession` view models | Core | done |
| T4 | `CurveLimits` (Rmax/Lmax, A ⇄ L), per-PI speed, superelevation inputs and `SuperelevationPlanner` | Core | done |
| T5 | `RouteGeometry`, `EdgeLineBuilder`, `PiEditor`, `StationShift`, `CurveInputText`; `CurveDesignSession` / `CurveRow` detail panel | Core | done |
| T6 | `RouteCreationSession` | Core | done |
| T7 | Host: tag extras, `RouteTag`, `EdgeWriter`, `SuperelevationWriter`, `ProfileShifter`, `RouteWriter` wiring, `CurveDesignWindow` detail panel | Host | done |
| T8 | Host: `CTTUYEN` window + command, `Prompts.PickPoints/PickStation` | Host | done |
| T9 | Host: `SampleLineStakes`, `AlignmentCurves`, `CTPHATCOC` / `CTDANHCOC` windows + commands; ribbon, `PackageContents.xml`, preset, version 0.5.0, `docs/testing.md` | Host + docs | done |
| T10 | Independent code review and fixes | — | done (2026-09-27, no blockers; left-turn tests added) |
| T11 | TCVN tables for superelevation (Bảng 13) and runoff length (Bảng 14) in the preset | preset | entered from memory 2026-09-27 (`TcvnTablesTests`); engineer review of the values still open |
| T12 | Windows verification on Civil 3D 2021 (checklist in `docs/testing.md`) | testers | open — zip published as release v0.5.0 |

Each task: Core tests first; host compiles on macOS at 0 warnings (`dotnet build src/C3DTools.Civil2021 -c Release`); `dotnet test C3DTools.Core.slnf` green; commit per task `feat(core|host): …`.

---

### Task 1: Stake model and naming rules

**Files:**
- Create: `src/C3DTools.Core/Stations/RouteStake.cs` — `StakeRole { Detail, Hundred, Km, CurveKey }`, `RouteStake(station, role, name, curveKind, curveNumber)`, `CurvePrefix`, `WithName`.
- Create: `src/C3DTools.Core/Stations/StakeNamer.cs` — `StakeNamingOptions` (one property per checkbox/field of AND Design's dialog: `DetailPrefix`, `FirstDetailNumber`, `RestartPerKm`, `NoRestartFrom100`, `DetailContinuousThroughH`, `CreateHundreds`, `NameByStation`, `RenameCurveKeys`, `FirstPiNumber`, `KeepPrefixes`, `StationDecimals`), `StakeNamer.Name(stakes, options, fromIndex, toIndex)`, `UniqueLabels(names, stations)` (sample-line names must be unique: "H1 (Km0)", "H1 (Km1)", then "-2"), `KmName`, `HundredName`, `StationName`.
- Test: `tests/C3DTools.Core.Tests/Stations/StakeNamerTests.cs` (12 tests).

**Step 1: Write the failing tests** — one per option: defaults (`Km0, C1…C4, H1, C5 … H9, C40, Km1, C1`), `DetailContinuousThroughH=false` (`C1, C2, H1, C1`), `RestartPerKm=false`, `NoRestartFrom100` (100 detail stakes then Km → `C101` vs `C1`), `CreateHundreds=false`, prefix + first number, `NameByStation` (`Km0+020`, `Km0+125.50`), curve keys with `FirstPiNumber`, kept prefixes (case-insensitive, trimmed), range `fromIndex..toIndex`, bad range throws, `UniqueLabels`.

**Step 2:** `dotnet test C3DTools.Core.slnf --filter StakeNamer` → FAIL (types missing).

**Step 3:** implement as in the files above. Rules: a `CurveKey` stake never consumes a detail number; the first stake of the range keeps `FirstDetailNumber` even when it is a Km stake; a Km stake resets `next` to 1 only when `RestartPerKm` and not (`NoRestartFrom100` and `next-1 >= 100`); an H stake resets only when `!DetailContinuousThroughH`.

**Step 4:** `dotnet test … --filter StakeNamer` → 12 passed.

**Step 5:** `git commit -m "feat(core): stake model and AND Design naming rules"`.

### Task 2: Stake planning

**Files:**
- Create: `src/C3DTools.Core/Stations/StakePlanner.cs` — `StationZone(from, to)`, `Generate(from, to, straightSpacing, curveSpacing, zones, curveKeys)`, `Replace(existing, generated, from, to)`, `Insert(existing, stations, subStakeStyle, decimals, out skipped)`, `RoleOf(station)`, tolerance 1 mm, cap 5000 stakes.
- Create: `src/C3DTools.Core/Stations/StakeClassifier.cs` — `NamedStation`, `Classify(stakes, curveKeys)` (key match within 1 cm, else role by station).
- Test: `tests/C3DTools.Core.Tests/Stations/StakePlannerTests.cs` (11 tests).

Key behaviours tested: round multiples of the straight spacing outside zones and of the curve spacing inside; every H and Km; both ends; key stakes win over H/Km at the same station (rank CurveKey > Km > H > named detail > unnamed); range limits and drops keys outside; `Replace` keeps stakes outside the range; `Insert` names by station or `C1a`, `C1b` (previous name + letter; a name ending in a–y bumps the letter); duplicates skipped and counted.

### Task 3: Stake dialog view models

**Files:**
- Create: `src/C3DTools.Core/Stations/StakeGenerationSession.cs` — Phát sinh / Chèn mode, `FromText/ToText` (Km text or metres, `ParseStation`), spacings, `HalfWidthText`, insert list (`;`/space separated, `AddInsertStation`), group list with `(Nhóm mới)` + `NewGroupName`, `SubStakeStyle`, `Plan(existing, zones, keys)` (generate → `Replace` → default `StakeNamer` names; insert → `Insert`), `SetPreview(planned, existing)` (rows: mới / đổi tên (old) / giữ; `NewCount`, `RemovedCount`, `PlannedLabels` unique), `CanApply`, `SummaryText`.
- Create: `src/C3DTools.Core/Stations/StakeRenameSession.cs` — `SetStakes(description, stakes)`, `StakeChoices` ("C1 (Km0+020.00)"), `FromIndex/ToIndex`, one property per option, `Options`, live `PreviewRows` (station, old, new, `Changed`), `NewNames` (unique), `ChangedCount`, `CanApply` (only when something changes).
- Test: `tests/C3DTools.Core.Tests/Stations/StakeSessionsTests.cs` (12 tests).

### Task 4: Curve limits, per-PI speed, superelevation

**Files:**
- Create: `src/C3DTools.Core/Curves/CurveLimits.cs` — `Available(pis, design, curve, out before, out after)` (leg minus neighbour's T), `MaxRadius(delta, l1, l2, before, after)` and `MaxSpiral(r, delta, before, after)` (bisection, floored to 1 cm, null when nothing fits), `SpiralParameter`/`SpiralLength` (A = √(R·L)).
- Modify: `src/C3DTools.Core/Curves/RouteDesigner.cs` — `CurveInput` gains `DesignSpeed?`, `Superelevated`, `SuperRate`, `RunoffOnSpiral`, `RunoffIn/Out`, `OffsetIn/Out`; the TCVN check uses `input.DesignSpeed ?? designSpeed`.
- Modify: `src/C3DTools.Core/Curves/CurveRules.cs` — `Superelevation` table (isc % by speed and radius). `CurveRuleChecker.Find` made public; issue codes `TransitionOverlap`, `TransitionOutsideRoute` (warnings).
- Modify: `src/C3DTools.Core/Presets/PresetSections.cs`, `ProjectPreset.cs` — `RoadSectionOptions { PavementHalfWidth = 3.5, CrossSlope = 2, EdgeStep = 1 }`.
- Create: `src/C3DTools.Core/Curves/Superelevation.cs` — `EntryRange/ExitRange` (the spiral when `RunoffOnSpiral` and L > 0, else `Runoff` starting `Offset` before TĐ / ending `Offset` after TC), `Plan(curve, crossSlope)` → 8 `SuperelevationPoint`s (BeginNormalCrown, LevelCrown at in/(in+isc), ReverseCrown at 2·in/(in+isc), BeginFullSuper, then mirrored), slopes in % with the outside lane on the left for a right turn, `WideningFactor`, `Check` (adds warnings to the curves), `Table`.
- Tests: `CurveLimitsTests.cs` (6), `SuperelevationTests.cs` (8).

### Task 5: Route geometry, edges, PI edit, profile shift, session panel

**Files:**
- Create: `src/C3DTools.Core/Curves/RouteGeometry.cs` — point/direction/offset point at any station of a designed route (tangent, clothoid via `CurveGeometryBuilder.Clothoid`, arc); tested against `CurveGeometryBuilder` for L = 0, L1 = L2 and L1 ≠ L2.
- Create: `src/C3DTools.Core/Curves/EdgeLines.cs` — `EdgeLineBuilder.Build(route | offsetPoint func, design, halfWidth, step)` (inside edge = Wb, outside = Wl, ramped by `WideningFactor`), `PiEditor.Deflection/SetDeflection` (rotates everything after the PI, keeps turn direction and later angles).
- Create: `src/C3DTools.Core/Profiles/StationShift.cs` — old→new station map from matched curves (by `PiIndex`), interpolated inside an old curve, `EndShift`, `IsIdentity`.
- Create: `src/C3DTools.Core/Curves/CurveInputText.cs` — `"v1;speed;super;rate;onSpiral;runIn;runOut;offIn;offOut"` for the XData tag.
- Modify: `src/C3DTools.Core/Curves/CurveDesignSession.cs` — `DrawEdges`, `WriteSuperelevation`, `ShiftProfiles`, `SplitWidening` (default off = YTC.lsp), `SuggestNormalRadius`, `CrossSlope`, `PavementHalfWidth`, `EdgeStep`, `CanEditDeflection`, `PisEdited`, `SuperelevationPoints`, `HasSuperelevation`, `SuggestRow`, `SuggestSuperelevation` (returns a message when the table has no value), `SetDeflection`; `Recalculate` runs `SuperelevationPlanner.Check`. `CurveRow` gains `HeaderText`, `SpeedText` (empty = route speed), `DeflectionText`, `A1Text/A2Text`, `RmaxText/LmaxText`, `Superelevated`, `SuperRateText` (0–20), `RunoffOnSpiral`, `RunoffIn/OutText`, `OffsetIn/OutText`, `SuperelevationText`; `Set` gained a validator.
- Tests: `RouteGeometryTests.cs` (7), `CurveDetailTests.cs` (10), `CurveInputTextTests.cs` (3). Existing `CurveDesignSessionTests` unchanged and green.

### Task 6: Route creation view model

**Files:**
- Create: `src/C3DTools.Core/Curves/RouteCreationSession.cs` — name (unique against the drawing's alignments, auto "TuyenN"), description, scale (→ `TextHeight` = 2.5 mm × scale/1000), start station (Km text), speed list from preset, style / label set / surface / assembly lists, layer (invalid chars), section file + `LoadAllAssemblies` → `AssembliesToImport`, source = polyline or picked points (deduplicated), `OpenCurveDesign`, `CanApply`, `SummaryText`.
- Test: `RouteCreationSessionTests.cs` (6).

### Task 7: Host — CTYTC additions

**Files:**
- Modify: `Ui/ToolWindow.cs` — `DialogAction` gains `PickPoints`, `PickFrom`, `PickTo`.
- Modify: `Curves/YtcTag.cs` — `YtcKind.Edge = 6`, `Extras` text (CurveInputText) written/read through `ToolTag.Text`.
- Modify: `Curves/RouteSource.cs` — `ApplyTags` restores extras; polyline source sets `CanEditDeflection`; alignment source reads `RouteTag` scale for text height; `DesignBareAlignment` (set only by CTYTC's picker): an alignment with no curves opens in "Thiết kế lại cong" with `CreateAlignment = true`.
- Modify: `Curves/AsBuiltDesign.cs` — as-built curves keep the user's W/superelevation inputs (`AsBuilt(input, group)`).
- Create: `Curves/RouteTag.cs` — regapp `C3DTOOLS_TUYEN` on the alignment: scale, speed, assembly name.
- Create: `Curves/TransitionWriters.cs` — `EdgeWriter` (LWPOLYLINEs on `YTC_MEP`, `OnAlignment` flips the offset sign: Civil 3D offsets are positive to the right), `SuperelevationWriter` (nested transaction; `AddUserDefinedCurve` between the sub-entities spanning the runoffs, `CriticalStations.Add(station, type, region)`, `SetSlope` on the four lane segments with slopes as fractions), `ProfileShifter` (layout profiles only; forward moves applied from the last PVI back so PVIs never cross).
- Modify: `Curves/CurveGeometryWriter.cs` — `CreateAlignment/UpdateAlignment` return the alignment id.
- Modify: `Curves/RouteWriter.cs` — polyline vertices moved when `PisEdited`; old curves measured before an update for `StationShift`; edges; superelevation into the alignment (message when there is no alignment); `_SIEUCAO.csv/.xlsx` next to the YEUTOCONG files; `Edge` kind erased on rerun.
- Modify: `Ui/CurveDesignWindow.cs` — window 1400×680; columns V and SC; tool row B/2 and in (%); output rows "Polyline các đoạn nối", "Siêu cao → Alignment", "Dồn dịch đỉnh trắc dọc phía sau" (alignment only); right-hand detail panel (`BuildDetail`) bound to the grid's `SelectedItem`: header, < Trước / Tiếp >, Tốc độ tại đỉnh, Góc chuyển hướng (enabled by `CanEditDeflection`), Rmin tối thiểu / thông thường, R, L1/L2, A1/A2 (disabled in read-only mode), Rmax/Lmax, Tra yếu tố cong; Không bố trí / Siêu cao, i max, Bố trí theo chuyển tiếp, Mở rộng phân đều khi tra, Mở rộng bụng/lưng, Nối đầu / Nối cuối (Chiều dài nối, Lệch ngoài), Tra siêu cao with its message line. `CommitEdits` also pushes the focused detail text box.

**Verify:** `dotnet build src/C3DTools.Civil2021 -c Release` → 0 warnings.

### Task 8: Host — CTTUYEN

**Files:**
- Modify: `Ui/Prompts.cs` — `PickPoints(ed, first, next)` (rubber band, transient red legs, `Lui` keyword, Enter after ≥ 2), `PickStation(ed, alignment, message)`.
- Create: `Ui/RouteCreateWindow.cs` — form laid out like AND Design's dialog; "Theo polyline…" (`Pick`) and "Chỉ điểm…" (`PickPoints`) in the header; `…` opens `OpenFileDialog` and reads the DWG's assemblies through a callback.
- Create: `Commands/RouteCreateCommand.cs` — `CTTUYEN`: reads alignment names/styles/label sets (prefers `TCVN_Tuyen` / CTYTCMAU's label set)/surfaces/assemblies; `ReadAssemblies(path)` via side `Database.ReadDwgFile`; `Write` in one transaction: layer, temporary polyline for picked points (`EraseExistingEntities`), `Alignment.Create(PolylineOptions)`, description, `ReferencePointStation`, `DesignSpeeds.Add` + `UseDesignSpeed`, `ImportAssembly` for each name below the route start, `RouteTag.Write`, `Profile.CreateFromSurface` "<tuyến>-TN"; preview asks `Giữ kết quả?`; on keep with `OpenCurveDesign`, selects the alignment and sends `_CTYTC`.

### Task 9: Host — CTPHATCOC, CTDANHCOC, packaging, docs

**Files:**
- Create: `Curves/SampleLineStakes.cs` — `AlignmentCurves.Read` (keys + NĐ…NC zones, as CTTOADO reads them; CTTOADO now uses it), `Groups`, `Read` (station order), `Write` (erase removed, rename kept in two passes through temporary names, create new ±halfWidth), `Rename`, `CreateGroup` (unique name), `Summary`.
- Create: `Ui/StakeWindows.cs` — `StakeGenerateWindow`, `StakeRenameWindow` (yellow rows = renamed), `StakeInputs` helpers.
- Create: `Commands/StakeCommands.cs` — `CTPHATCOC` (group choice, `…` picks a station, Chèn "Chỉ điểm…", preview → plan, apply → `SampleLineStakes.Write` in one transaction), `CTDANHCOC` (pick a sample line or an alignment; the owning alignment is found by scanning `GetSampleLineGroupIds` since 2021 has no parent id on the group; preview renames then asks `Giữ kết quả?`).
- Modify: `Ui/RibbonSetup.cs` (Tuyến panel: Tạo tuyến, Phát sinh cọc, Đánh tên cọc), `bundle/PackageContents.xml` (three commands, AppVersion 0.5.0), `bundle/Resources/tcvn4054.preset.json` (`Superelevation: []`, `RoadSection`), `Directory.Build.props` + `.github/workflows/build.yml` (0.5.0), `docs/testing.md` (section "Các lệnh mới trong 0.5", open questions, Windows checklist).

### Task 10: Independent review (open)

Dispatch `code-reviewer` on `git diff main...feat/and-design-route-workflow`; fix blockers and should-fixes with a test per fix; rerun `dotnet test C3DTools.Core.slnf` and the Release host build; squash the WIP commit into `feat(core): …` / `feat(host): …` commits.

### Task 11: TCVN tables (open — needs the user)

Bảng 13 (isc by V and R) is in `CurveRules.Superelevation` and Bảng 14 (L by V and R) in `CurveRules.MinSpiral` of `bundle/Resources/tcvn4054.preset.json`, entered from memory and marked so in `CurveRules.Source`. `tests/C3DTools.Core.Tests/Presets/TcvnTablesTests.cs` checks completeness and consistency (contiguous ranges from 0 to R không siêu cao of Bảng 11, isc and L falling with R, isc ≤ 8 % / 7 %, lookups at Rmin). **Open:** a second engineer compares every value with the printed standard, as the Rmin table was.

### Task 12: Windows verification (open — testers)

Run the checklist "Chưa kiểm tra trên Windows (0.5)" in `docs/testing.md` on Civil 3D 2021.3. Highest risk, in order: `SuperelevationWriter` (never run; `AddUserDefinedCurve`/`SetSlope` semantics assumed), `ProfileShifter` (PVI station set), `ImportAssembly` from a side database, `SampleLine.Name` rename with temporary names, `PickPoints` transient graphics. Each has a Vietnamese fallback message and leaves the drawing unchanged on failure.
