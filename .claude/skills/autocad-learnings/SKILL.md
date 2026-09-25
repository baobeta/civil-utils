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

## [2026-09-24] Civil 3D 2021 — Styles, abbreviations, Table and ribbon members (compile-time only)
- Styles: `StyleBase.ExportTo(Database, StyleConflictResolverType)` and `static ExportTo(ObjectIdCollection, Database, StyleConflictResolverType)` exist; `StyleConflictResolverType` (`Autodesk.Civil`) = Ignore/Rename/Override/CancelRemaining. Curve label styles: `civDoc.Styles.LabelStyles.AlignmentLabelStyles.CurveLabelStyles` (also `GeometryPointLabelStyles`, `SpiralLabelStyles`). Label sets: `civDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles.Add(name)` → `AlignmentLabelSetStyle` with `Add(ObjectId labelStyleId)`, `Add(LabelStyleType, string)`, `RemoveAt(int)`, `Count`. Curve table styles: `civDoc.Styles.TableStyles.AlignmentCurveTableStyles`.
- `LabelStyle.AddComponent(name, LabelStyleComponentType.Text)` → `LabelStyleTextComponent` with `.Text.Contents` (PropertyString), `.Border.Visible` (PropertyBoolean), `.Border.BorderType` (PropertyEnum<`Autodesk.Civil.TextBorderType`>: Rectangular/RoundedRectangular/Circular). Plan readability: `LabelStyle.Properties.PlanReadability.PlanReadable` / `.PlanReadableBias` / `.FlipAnchorsWithText` (bias unit assumed radians, not verified).
- Abbreviations: `civDoc.Settings.DrawingSettings.AbbreviationsSettings.AlignmentGeoPointText.SetAlignmentAbbreviation(AbbreviationAlignmentType, string)` (`Autodesk.Civil.Settings`); enum has TangentCurveIntersect (PC), CurveTangentIntersect (PT), TangentSpiralIntersect (TS), SpiralTangentIntersect (ST), SpiralCurveIntersect (SC), CurveSpiralIntersect (CS), TangentTangentIntersect (PI), MidCurvePoint, …
- AutoCAD Table (AcDbMgd 24.0): `Table.SetColumnWidth(int, double)` is `[Obsolete]` (warning CS0618) → use `table.Columns[c].Width`. `SetRowHeight(double)`, `SetSize`, `Cells[r, c]` (`Cell.TextString`, `CellRange.TextHeight` is `double?`), `CellRange.Create(table, r0, c0, r1, c1)`, `UnmergeCells`, `GenerateLayout()` exist.
- Ribbon (AdWindows 24.0): `ComponentManager.Ribbon`, `ComponentManager.ItemInitialized` (EventHandler<RibbonItemEventArgs>), `RibbonTab{Id,Title,Panels}`, `RibbonPanel{Source}`, `RibbonPanelSource{Title,Items}`, `RibbonRowPanel`/`RibbonRowBreak`, `RibbonButton{Text,ShowText,ShowImage,Size,Orientation,Image,LargeImage,CommandHandler,CommandParameter}`. AdWindows comes from the `AutoCAD.NET` package, so `ExcludeAssets="runtime"` keeps it out of the output.
- Source: reflection over the NuGet reference assemblies (MetadataLoadContext) and a clean `dotnet build` on macOS. Runtime behaviour NOT verified (field syntax of `Contents`, whether ExportTo works from a side database, whether abbreviations need a write-open).
- Promoted to: *(pending)*

## [2026-09-25] Civil 3D 2021 — Profile entity grades: unit NOT verified (pending)
- `ProfileTangent.Grade`, `ProfileCircular/ProfileParabolaSymmetric/ProfileParabolaAsymmetric.GradeIn/GradeOut` and `ProfilePVI.GradeIn/GradeOut` are `double`; whether 2021 returns them as fractions (0.03) or percent (3.0) is not verified on a live Civil 3D.
- C3DTools does not trust them: `ProfileReader` computes tangent grades from Start/End station and elevation, and checks curve grades against (PVI − TĐ)/(PVIStation − StartStation) and (TC − PVI)/(EndStation − PVIStation) with `ProfileSegment.NormalizeGrade` (ratio 50–200 → divide by 100, one warning "Civil trả về độ dốc theo %; đã quy đổi").
- Other members used by CTCONGDUNG/CTTRACDOC, verified by reflection only: `Profile.Entities`, `Profile.ElevationAt(double)`, `Profile.ProfileType`, `Alignment.GetProfileIds/GetSampleLineGroupIds`, `SampleLineGroup.GetSampleLineIds()`, `SampleLine.Station`, `ProfileView.FindXYAtStationAndElevation(double, double, ref double, ref double)` → bool, `ProfileView.StationStart/StationEnd/ElevationMin/ElevationMax`, `Graph.Location`, `ProfileViewStyle.GraphStyle.VerticalExaggeration`.
- Why it matters: a percent value read as a fraction makes every A, R, E and grade label 100× wrong.
- Source: reflection over Civil3D2021.Base 1.0.0 (MetadataLoadContext); runtime pending (test plan 0.3/0.4).
- Promoted to: *(pending)*

## [2026-09-25] Civil 3D 2021 — Pipe networks for CTBANGCONG (reflection only, runtime pending)
- The pipe network class is `Autodesk.Civil.DatabaseServices.Network` (no `PipeNetwork` type): `GetPipeIds()`, `GetStructureIds()`, `Name`. List them with `CivilDocument.GetPipeNetworkIds()`.
- `Pipe.Length2D` is `[Obsolete]` in 2021 (CS0618: "Use Length2DCenterToCenter instead"); `Length2DToInsideEdge`, `Length3D*` also exist. `Pipe.InnerHeight`, `OuterHeight`, `CrossSectionalShape` (`SweptShapeType`: Undefined, CustomShape, Circular, Rectangular, Elliptical, EggShaped, HorizontalElliptical, Arched), `FlowDirection` (`FlowDirectionType`: Bidirectional, StartToEnd, EndToStart), `StartStructureId/EndStructureId` exist.
- `Part` (base of Pipe/Structure) has `WallThickness`, `PartDescription`, `PartSizeName`, `PartSubType`, `Material` — spike #6 saw no `WallThickness` on `Pipe` because it is inherited; C3DTools still derives wall = (OuterDiameterOrWidth − InnerDiameterOrWidth)/2 as decided in the spike. There is no `PartFamilyName`.
- `Structure.RimElevation`, `SumpElevation`, `Location` exist. `Alignment.PointLocation(station, offset, tolerance, ref e, ref n, ref bearing)` overload exists (bearing unit not verified).
- Unverified: whether `Pipe.StartPoint.Z` is the invert or the centreline — preset `Culvert.EndpointIsCentreline` (default false) decides; whether `StationOffset` throws or returns a clamped station for points beyond the alignment ends (C3DTools does both checks).
- Promoted to: *(pending)*

## [2026-09-25] Civil 3D 2021 — Members used by the 0.3/0.4 commands (reflection only, runtime pending)
One entry per command; profiles (CTCONGDUNG, CTTRACDOC) and pipe networks (CTBANGCONG) are in the two entries above and are not repeated. Every item below compiles against AutoCAD.NET 24.0.0 / Civil3D2021.Base 1.0.0; the runtime checks are the "Chưa kiểm tra trên Windows" list of `docs/test-plan-0.3-0.4.md`.
- Ribbon (AdWindows 24.0): `RibbonItem.ToolTip` (type object) accepts a `RibbonToolTip` (`RibbonToolTipBase.Title`, `.Content` (object); `RibbonToolTip.Command`, also `ExpandedContent`, `Shortcut`). Pending: how the tooltip renders.
- CTFONT: `DBText.HasFields`, `MText.Contents`, `AttributeReference.IsMTextAttribute` / `MTextAttribute` (get returns a copy: set it back), `AttributeDefinition.IsMTextAttributeDefinition` / `MTextAttributeDefinition` / `Prompt`, `Dimension.DimensionText`, `MLeader.ContentType` / `MText` (copy, set back), table `Cell.TextStyleId` is `ObjectId?`, `Cell.Contents[i].ContentTypes` (`CellContentTypes.Field`), `TextStyleTableRecord.Font` (`FontDescriptor(typeface, bold, italic, charset, pitchAndFamily)`) and `FileName`. Pending: MLeader and MText-attribute write-back, cell text style set, SHX style switched to a TrueType font by clearing `FileName`.
- CTLAYER: `BlockTableRecord.IsDynamicBlock` / `GetAnonymousBlockIds()`, `Database.Purge(ObjectIdCollection)` (leaves the unreferenced ids), `SymbolUtilityServices.ValidateSymbolName` / `IsLinetypeContinuousName` / `IsLayerDefpointsName`, `Transaction.GetObject(id, OpenMode.ForWrite, openErased: false, forceOpenOnLockedLayer: true)`. Pending: writes on locked layers, purge result.
- CTTOADO: `Alignment.PointLocation(station, offset, ref e, ref n)`, `Surface.FindElevationAtXY(x, y)` (throws outside the surface; handled), `CivilDocument.CogoPoints.Add(Point3d location, string desc, bool useNextPointNumSetting)` → ObjectId, `CogoPoint.PointName` setter (throws on a duplicate name; handled), `CogoPointCollection.Remove(ObjectId)`. Pending: all of them at runtime.
- CTVN2000: `CivilDocument.GetAllPointIds()`, `CogoPoint.Easting` / `Northing` have public setters (a locked point is expected to throw; handled per point), `Entity.TransformBy` for BlockReference / DBText / MText, `Vertex2d.Position` and `PolylineVertex3d.Position` setters. Pending: COGO writes, attribute follow-through on blocks.
- CTMATDIA: `TinSurface.GetTriangles(bool includeInvisible)` → `TinSurfaceTriangleCollection` (IDisposable), `TinSurfaceTriangle.Vertex1..3` / `Edge1..3`, `TinSurfaceEdge.Vertex1/2`, `TinSurfaceVertex.Location`, `TinSurface.DeleteLines(IEnumerable<TinSurfaceEdge>)` → SurfaceOperationDeleteMultipleLines, `TinSurface.ExtractContours(double interval)` → ObjectIdCollection, `Surface.BoundariesDefinition.AddBoundaries(ObjectIdCollection, double midOrdinateDistance, SurfaceBoundaryType, bool useNonDestructiveBreakline)` → SurfaceOperationAddBoundary, `Surface.BuildOptions.UseMaximumTriangleLength` / `MaximumTriangleLength`, `Surface.Rebuild()`, `SurfaceStyle.ContourStyle.MajorContourInterval` / `MinorContourInterval`. Pending: DeleteLines inside an undoable transaction, which entity type ExtractContours creates and whether its `Elevation` is the contour level.
- CTTRACNGANG: `SectionView.ParentEntityId` (assumed to be the sample line; fallback searches `SampleLine.GetSectionViewIds()`), `SampleLine.GetSectionIds()` / `GroupId` / `Station`, `Section.SourceName` / `SourceType` / `SectionPoints` / `LeftOffset` / `RightOffset`, `SectionPoint.Location` (read as X = offset, Y = elevation, checked against the offset range), `SectionView.FindXYAtOffsetAndElevation(offset, elevation, ref x, ref y)` → bool, `SectionView.OffsetLeft` / `OffsetRight` / `ElevationMin` / `Location`, `SampleLineGroup.SectionViewGroups` → `SectionViewGroup.GetSectionViewIds()`. Pending: what `Location` marks on a 2021 section view, the SectionPoint axes.
- CTXEPTRANG: `SectionView.Location` has a setter (moves the view), `Entity.GeometricExtents` for the view's size. Pending: whether moving `Location` carries the grid, labels and section lines.
- Source: reflection over the NuGet reference assemblies (MetadataLoadContext) and a clean Release build on macOS (0 warnings). Nothing here has run in Civil 3D 2021 yet.
- Promoted to: *(pending)*
