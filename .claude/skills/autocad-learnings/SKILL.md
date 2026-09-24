---
name: autocad-learnings
description: Append-only log of newly verified AutoCAD / Civil 3D API gotchas. Use when you discover API behavior (error, signature, version difference) not already documented in the autocad-api, civil3d-api or autocad-scaffold skills, and record it here.
---

> Upstream: ADN-DevTech/acad-api-skill `skills/learnings.md` @ `1134159` (MIT). Upstream facts are verified for AutoCAD/Civil 3D **2027 / .NET 10**; this repo targets **Civil 3D 2021 (R24.0, net48)**. Where they conflict, the overrides in the root `CLAUDE.md` win. Cross-references to `autocad-api.md`, `civil3d-api.md`, `scaffold.md` mean the `autocad-api`, `civil3d-api`, `autocad-scaffold` skills.

# Learnings Log — AutoCAD / Civil 3D / Plant 3D

Append-only knowledge accumulation file. The AI records verified discoveries here during development sessions. **Never delete entries** — only append new ones or mark existing ones as promoted.

## How this works

1. During a development session, the AI encounters a new gotcha, API behavior, or corrected pattern that is **not already documented** in the skill files.
2. The AI appends it here with today's date, category, and a brief description.
3. A human reviews the entry, verifies it against SDK docs or runtime behavior.
4. Once verified, the entry is **promoted** — moved into the appropriate skill file (`autocad-api.md`, `civil3d-api.md`, `plant3d-api.md`, or `scaffold.md`) and marked below with `Promoted to:`.

## Entry format

```
## [YYYY-MM-DD] Category — Short title
- What was discovered (exact API behavior, error message, correct usage)
- Why it matters (what breaks if you get it wrong)
- Source: how it was verified (SDK sample path, runtime test, official docs URL)
- Promoted to: *(pending)* or `skill-file.md` > Section Name
```

---

## Entries

*(No entries yet — the AI will append discoveries below this line as they occur.)*

## [2026-09-24] Civil 3D 2021 — Alignment signatures in Civil3D2021.Base 1.0.0 (compile-time only)
- `AlignmentEntityCollection.AddFreeSCS(int previousEntityId, int nextEntityId, double spiral1Param, double spiral2Param, SpiralParamType spType, double radius, bool isGreaterThan180, SpiralType spiralDefinition)`; no clockwise flag (the turn comes from the tangents). `AddFreeCurve(int prev, int next, double paramValue, CurveParamType paramType, bool isGreaterThan180, CurveType curveType)`; `CurveType` is only `Compound`/`Reverse`.
- `SpiralType` is `Autodesk.Civil.SpiralType` (namespace `Autodesk.Civil`, not `DatabaseServices`); `SpiralParamType`, `SpiralDirectionType`, `CurveParamType`, `AlignmentEntityType` are in `Autodesk.Civil.DatabaseServices`.
- `Alignment.PointLocation(station, offset, ref e, ref n)` and an overload with `(station, offset, tolerance, ref e, ref n, ref bearing)`; `StationOffset(e, n, ref station, ref offset)`; `ReferencePointStation` has a public setter; `Alignment.Create(CivilDocument, PolylineOptions, name, siteId, layerId, styleId, labelSetId)` exists. `PolylineOptions` is a struct with `PlineId`, `AddCurvesBetweenTangents`, `EraseExistingEntities`.
- `AlignmentSubEntityArc.Clockwise`, `AlignmentSubEntitySpiral.RadiusIn/RadiusOut/Direction/SpiralDefinition`, `AlignmentEntity.SubEntityCount` + indexer, `AlignmentEntityCollection.GetEntityByOrder(int)` exist.
- `ShowModalWindow(System.Windows.Window)` lives on `Autodesk.AutoCAD.ApplicationServices.Core.Application` (AcCoreMgd) in 2021, not on `ApplicationServices.Application` (AcMgd has only the `Uri` overloads).
- Why it matters: the 2027 skills show other overloads; guessing breaks the macOS build.
- Source: reflection over the NuGet reference assemblies with System.Reflection.MetadataLoadContext, and `AcCoreMgd.xml`; runtime behaviour (e.g. AddFreeSCS with L1 ≠ L2, one spiral 0) is NOT verified yet (Task 0 spikes S8/S9/S10).
- Promoted to: *(pending)*
