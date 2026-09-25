# TCVN Curve Elements (Yếu tố cong) Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace `YTC.lsp` and the manual label-style workflow with one dialog-driven command, `CTYTC`. The user picks a polyline or alignment, sees every PI in a grid, and types R, L1, L2, Wb and Wl. T, P, K, stations and TCVN warnings update live. One click on Áp dụng then produces a Civil 3D alignment with clothoid/arc curves, the TCVN curve boxes, the NĐ/TĐ/P/TC/NC stakes (native, dynamic) and the CSV/table.

**Architecture:** Core (`C3DTools.Core.Curves`) does all the math:
- `RouteDesigner` turns PI coordinates plus per-PI inputs into curve elements, stations and warnings. It is the LISP's logic, extended to L1 ≠ L2.
- `CurveGrouper` reads curves back from an existing alignment.
- Formatters build the box and table text.

A WPF dialog in the host binds to a thin view model that calls `RouteDesigner` on every edit. The host only reads the selection and writes the results: alignment entities, MText boxes, label set, AutoCAD Table. Every drawing change goes through the preview → confirm → one-transaction/one-undo pattern verified in spike #2.

## Parity principle: YTC.lsp is the reference

`YTC.lsp` / `YTCA` are known to give correct results. This plan ports them, it does not redesign them. Concretely:

- **Same output, same place.** Layers `YTC_CONG`, `YTC_COC`, `YTC_BANG`; stake geometry (tick from −h to 10h, station text at 0.55·tick ± 0.9h, readable rotation); box text and rotation; leader; CSV `<tên bản vẽ>_YEUTOCONG.csv` with the same 14 columns. An engineer who runs the LISP and `CTYTC` on the same input must get drawings they can overlay.
- **Golden tests from the LISP.** Before Task 1, run the LISP on three fixtures (simple arc, symmetric SCS, a 5-PI route with a collinear PI) and save its CSVs under `tests/C3DTools.Core.Tests/Curves/Golden/`. Core tests read them and require agreement to 0.01 m / 1". Where the plan's math is more precise than the LISP's (p series, Task 2), the tolerance still holds for L/R < 0.5, which covers real roads.
- **Same defaults.** V = 60, h = 2.5, start station 0, each PI defaults to the previous one. The LISP resets an over-long L to 0 with a message; the dialog shows it as an error instead (D10).
- **Plain geometry by default.** Like the LISP, polyline mode draws `ARC` + clothoid `LWPOLYLINE`s (20 segments, `ytc:clo`) on `YTC_CONG`. Creating a Civil 3D alignment is a checkbox, off by default, because it is the one part with API risk (S8/S9).
- **Only the interaction changes:** one dialog instead of prompts, live results, editable rows, and errors before drawing.

## What changes compared with YTC.lsp

| YTC.lsp | CTYTC |
| --- | --- |
| 6 command-line prompts per PI (R, L, Wb, Wl…); a typo means starting over | One grid: edit any cell in any order, see results immediately, go back and change a PI |
| Result only visible after drawing | Live T1/T2, P, K, stations; red cell for R < Rmin, overlap with the previous curve, or L too long for α |
| L1 = L2 only; p series truncated at L⁴ | L1 ≠ L2; clothoid series to θ⁵ (Task 2) |
| Draws plain ARC/LWPOLYLINE; changing R means redrawing by hand | Same drawing by default, but every object is XData-tagged, so rerunning `CTYTC` on the polyline reloads the grid and replaces the result. Optional checkbox: create a Civil 3D alignment instead |
| Stakes are TEXT, typed as `T\U+0110` | Stakes are still our own numbered TEXT (D9), but tagged with XData so a rerun refreshes them; the native label set is also imported for users who prefer it |
| `YTC` (polyline) and `YTCA` (alignment) are two commands with different prompts | One dialog with two modes: **Thiết kế** (polyline or alignment, curves editable) and **Chỉ cắm cọc + khung** (`YTCA` behaviour: the alignment is left untouched) |
| `YTCA` reads Wb/Wl by selecting Offset Alignments on the command line | Button **Đọc Wb/Wl từ Offset Alignment…** in the dialog; the values land in the grid, where they can still be edited |
| CSV in ANSI without a BOM (Excel breaks the Vietnamese) | UTF-8 CSV with a BOM (`CsvTableWriter`) plus an AutoCAD Table |
| Rmin table hard-coded in the LISP | Preset JSON per project, seeded from the LISP table (Task 4) |

Kept from the LISP because it works well: each new PI defaults to the previous PI's R/L/Wb/Wl; start station entry; the box layout (A P / R K / T1 T2 / L1 L2 / Wb Wl), rotated so its vertical axis is the bisector, with a leader from the curve midpoint through the PI to the box; the overlap warning; the marker on the PI being edited; `YTCA`'s way of measuring widening (nominal width = the smallest offset along the route; W = offset at the curve midpoint − nominal) and its definition bụng = inside of the curve, lưng = outside.

**Tech Stack:** Same as the foundation plan: netstandard2.0 Core + xUnit, net48 x64 host, `AutoCAD.NET` 24.0.0 and `Civil3D2021.Base` (compile-only), Newtonsoft.Json presets, bundle zip via `scripts/package-bundle.ps1`.

---

## Why not just the native workflow?

The native workflow (Label Style + Label Set + Abbreviations, saved into a .dwt) is good and is kept as mode 1 (`CTYTCMAU`). It has three gaps that justify code:

| Gap | Native Civil 3D | This plan |
| --- | --- | --- |
| T, P with transition curves | `Curve Tangent` and `Curve External Secant` are for the **circular arc only**. They are wrong for TCVN when L > 0. The PI label workaround exists but gives no box and no L. | `CurveElementsCalculator` computes T1/T2/P using the spiral shift p and the tangent offset m, including L1 ≠ L2 |
| W (độ mở rộng) | Not available; typed by hand per label | Looked up from the preset's TCVN 4054 widening table by R |
| Checks against TCVN 4054 | Only if someone builds a design-criteria XML file | `CurveRuleChecker` warns on R < Rmin(V) and L < Lmin(V, R) |
| Repeated setup per drawing | Manual, or a .dwt | `CTYTCMAU` imports everything into any open drawing in one step |

## Commands and UI

The main command is `CTYTC`. It opens one dialog, which is also on the ribbon (tab **C3DTools**, button **Yếu tố cong**):

```
┌ Yếu tố cong – TCVN 4054 ──────────────────────────────────────────────────────┐
│ Tuyến: Polyline (7 đỉnh)  [Chọn trên bản vẽ…]   Lý trình đầu [0.00]  V [60 ▾] km/h│
│ Rmin giới hạn 125 m · thông thường 250 m                                        │
│ ┌────┬───────────┬───────┬──────┬──────┬─────┬─────┬────────┬────────┬────────┬──────────────┐ │
│ │Đỉnh│ A         │ R     │ L1   │ L2   │ Wb  │ Wl  │ T1     │ T2     │ K      │ Cảnh báo     │ │
│ │ Đ1 │ 31°31'52" │ 250   │ 50   │ 50   │ 0.6 │ 0   │ 95.69  │ 95.69  │ 187.58 │              │ │
│ │ Đ2 │ 12°05'10" │ 100   │ 0    │ 0    │ 0   │ 0   │ 10.59  │ 10.59  │ 21.09  │ R < Rmin ⚠   │ │
│ │ Đ3 │ 64°10'00" │ 300   │ 60   │ 40   │ 0.4 │ 0   │ 218.06 │ 208.51 │ 385.98 │ Chồng cong ✖ │ │
│ └────┴───────────┴───────┴──────┴──────┴─────┴─────┴────────┴────────┴────────┴──────────────┘ │
│ Dòng chọn: TĐ Km0+131.09 · P Km0+224.88 · TC Km0+318.68   [Phóng tới đỉnh]            │
│ [Gợi ý R, L theo TCVN]  [Áp dòng này cho các dòng dưới]      Chiều cao chữ [2.5]      │
│ ☑ Vẽ đường cong (ARC/clothoid)  ☐ Tạo Alignment Civil 3D  ☑ Khung  ☑ Cọc  ☑ CSV      │
│                                        [Xem trước]   [Áp dụng]   [Hủy]                │
└──────────────────────────────────────────────────────────────────────────────┘
```

- **A** is read-only and comes from the PI geometry. **R, L1, L2, Wb and Wl** are editable. A new row copies the row above (LISP behaviour). **T1, T2, P, K** and the stations are recalculated on every keystroke.
- A cell with ✖ (overlap with the previous curve, or L too long for α) disables **Áp dụng**. A cell with ⚠ (R below the limit or normal radius, L below the minimum) only warns.
- **Chọn trên bản vẽ…**, **Phóng tới đỉnh**, **Đọc Wb/Wl từ Offset Alignment…** and **Xem trước** hide the dialog, do their job in the drawing, and reopen the dialog with its state kept.
- **Chế độ** (shown only when the source is an alignment): ○ *Thiết kế lại cong* (R, L editable; Áp dụng rewrites the curves) or ● *Chỉ cắm cọc + khung* (default, `YTCA` behaviour: R, L, A, T, P, K are read from the alignment and shown read-only; only Wb/Wl are editable; the alignment is never modified).
- **Xem trước** draws everything in an open transaction and asks `Giữ kết quả? [Co/Khong]` on the command line. Không rolls it back and returns to the dialog. **Áp dụng** does the same without asking. Either way the result is one undo.

| Command | Does |
| --- | --- |
| `CTYTC` | The dialog above. Accepts a **LWPOLYLINE** (a new alignment is created, like `YTC`) or an **existing alignment** (grid prefilled from its curves; by default only stakes and boxes are written, like `YTCA`; in *Thiết kế lại cong* mode Áp dụng also updates the curves) |
| `CTYTCMAU` | Imports styles, label set and abbreviations only (for users who want native labels without the dialog). `CTYTC` calls the same importer automatically when the styles are missing |
| `CTYTCBANG` | Curve summary AutoCAD Table plus CSV for an alignment. Also available as a checkbox in the dialog |

Proposed PRD IDs: C-09 (`CTYTC`, `CTYTCMAU`) and C-10 (`CTYTCBANG`). **Update the PRD before Task 9.** The former `CTBOCONG` (P1) is folded into `CTYTC`: filleting PIs *is* the Áp dụng step.

## Where this plan sits

- It requires the foundation plan (done) and its spike report.
- It **does not** require Plan 2 (host services). Each command here uses the preview/commit pattern directly (spike #2), and this plan creates the minimal ribbon itself (Task 13). When Plan 2 lands, move these onto its preview pipeline, preset service and ribbon.
- Tasks 1–7 are Core only and run on macOS; so does Task 8's `PiExtractor`. Task 9's dialog builds on macOS (its Step 1 checks this) but can only be *seen* on Windows. Tasks 0 and 9–14 need Windows with Civil 3D 2021 for the manual checks.

## Decisions

Settled by `YTC.lsp` (change them only if the LISP was wrong):

| # | Decision |
| --- | --- |
| D1 | K = total length = Rα + (L1+L2)/2 (the LISP's R(α−2β) + 2L). The table also shows K0 |
| D2 | Wb = widening on the inside of the curve (bụng), Wl = on the outside (lưng), in metres. For an existing alignment they are measured from Offset Alignments with widening (the `YTCA` method); for a polyline they are typed, defaulting to the previous row. The preset widening table (optional) only feeds the *Gợi ý* button |
| D3 | Box rotated so its vertical axis lies on the bisector, centred outside the PI at `4h + bh/2`, text flipped to stay readable, plus a leader from the curve midpoint through the PI to the near edge of the box (`ytc:bang`, second version) |
| D4 | Box always shows every line, zeros included, with no title line: `A= P=` / `R= K=` / `T1= T2=` / `L1= L2=` / `Wb= Wl=`. The Đ number appears only on the stakes and in the table |
| D6 | Seed Rmin limit and normal radius from the LISP's table (TCVN 4054‑2005, V = 20…120). A second engineer still checks it against the standard |

Still open (domain owner; D5 before Task 4, the rest before Task 7):

| # | Question | Default |
| --- | --- | --- |
| D5 | Lct min: from which table (TCVN 4054‑2005 Bảng 14?), by V only, or by V and R? | By V and R, entered in the preset. When empty, no L check runs |
| D7 | Number format: the LISP trims zeros (`R=250`, `A=31°31'52"`) while the manual style uses 0.01. Which one for the box? | Trim trailing zeros, like the LISP. The table and CSV always use 2 decimals |
| D9 | Stakes: our own numbered stakes (TĐ1, P1, TC1…, like the LISP, refreshed when `CTYTC` is rerun) or native geometry-point labels (update live, but can't show the Đ number)? | Our own numbered stakes; the native label set is still imported for users who prefer it |
| D10 | The LISP silently resets L to 0 when it is too long for α. Should the dialog do the same (with a note) or block Áp dụng until the user fixes L? | Block, and show `L quá dài so với góc chuyển hướng` on the row; one click on *Gợi ý* fixes it |
| D8 | Station text: `0+131.09` (LISP and the manual workflow) or `Km0+131.09` (`StationFormatter` today)? | `0+131.09` in stakes and box; add a `withKmPrefix` flag to `StationFormatter` in Task 5 |

## Conventions (apply to every task)

As in the foundation plan: `CultureInfo.InvariantCulture` for every number, Vietnamese user messages, English identifiers, `CT` command prefix, no Autodesk references in Core, and a commit after every task.

---

### Task 0 (Windows + Civil 3D 2021): API spike for curve elements

Append a new section **"Spike 2 — curve elements"** to `docs/spikes/2026-10-spike-report.md`, using the same table format (# / Question / Method / Result / Decision). Use temporary `CTSPIKEYTC*` commands on a throwaway branch and don't merge them.

| # | Question | Method |
| --- | --- | --- |
| S1 | How do we read the sub-entities of an alignment? Check that `alignment.Entities.GetEntityByOrder(i)`, `entity.SubEntityCount`, `entity[j]`, `AlignmentSubEntityArc.Radius/Clockwise/StartStation/Length`, `AlignmentSubEntitySpiral.RadiusIn/RadiusOut/Direction/SpiralDefinition` exist in 2021. What is `RadiusIn` on the tangent side (∞, 0 or NaN)? | Build a fixture alignment: line, SCS (R200, L50/50), line, arc (R100), line, SCS (R300, L60/40), line. Dump everything with `ed.WriteMessage` |
| S2 | Do our Core numbers match Civil 3D? | For each fixture curve, compare Civil 3D's arc `Length`, `Tangent`, `ExternalSecant` (circular part), and the PI label's "Total Tangent"/"Total External", against Core output for T and P (tolerance 0.01 m) |
| S3 | Abbreviations API: where are *Alignment Geometry Point Text* abbreviations in 2021? (Candidates: `CivilDocument.Settings.DrawingSettings.AbbreviationsSettings.AlignmentGeoPointText` / `…AlignmentGeoPointEntityData`.) Can we set PC→TĐ and similar, and does the change undo? | Reflection dump plus a set-and-undo test |
| S4 | Style import: does `StyleBase.ExportTo(Database, StyleConflictResolverType)` exist in 2021 and copy a curve label style plus its label set from a side-loaded template DB (`Database.ReadDwgFile`) into the active drawing? Does it undo in one step? | Hand-author `C3DTools-TCVN.dwg` by following the manual workflow (Label Style, Label Set, Table Style). Then import it into a blank drawing |
| S5 | Fallback if S4 fails: can a curve label style be *created* by code? Check `CurveLabelStyles.Add`, `LabelStyle.AddComponent(name, LabelStyleComponentType.Text)`, `Text.Contents.Value = "<[Curve Radius(Um\|P2\|RN\|AP\|GC\|UN\|SN\|OF\|AP)]>"`, `Border.Visible`, plan readability. Read the exact property-field codes from the hand-made style | Reflection plus reading back the S4 template's component contents |
| S6 | `MText.ShowBorders` (text frame): available in the 2021 API? Is the frame visible at plot? | Create one MText, toggle it, plot to PDF |
| S7 | XData on MText survives save, reopen and `U` | Tag, save, reopen, read back |
| S8 | Signatures of `AlignmentEntityCollection.AddFreeCurve…` and `AddFreeSCS…` in 2021 (between two entities, by radius and spiral lengths, clothoid) | Reflection plus one fillet on a polyline-made alignment |
| S9 | Can an existing alignment's curves be edited in place? Check whether `AlignmentArc.Radius` and `AlignmentSCS` spiral lengths are settable. If not, check that deleting the curve entity between two tangents and calling `AddFree…` again keeps the tangents and the alignment's labels | Edit the S1 fixture both ways, then `U` |
| S10 | Start station: does `alignment.ReferencePointStation = sta0` (or `SetReferencePoint…`) set the station at the start of a polyline-made alignment? | Set it to 1000 and read `StartingStation` |
| S11 | A WPF window shown with `Application.ShowModalWindow`: can it close, let the command run `GetEntity` or zoom, and then reopen with the same view model? Does it scale correctly at 125 %/150 % Windows DPI? | Minimal window with a DataGrid and one button |
| S12 | Does the macOS/CI build of `net48` with `<UseWPF>true</UseWPF>` plus `<EnableWindowsTargeting>true</EnableWindowsTargeting>` compile XAML? | `dotnet build` on macOS and on the Windows CI runner |
| S13 | Widening from Offset Alignments in 2021: does `Alignment.IsOffsetAlignment` / `OffsetAlignmentInfo` expose the nominal offset and the widening regions (start/end station, width) directly? If not, does `centreline.StationOffset(x, y, ref station, ref offset)` on points sampled along the offset alignment reproduce `YTCA`'s geometric method? | Fixture: one alignment with a left and a right offset alignment, one widening region each, compare with the LISP's numbers |
| S14 | T1, T2 and P measured from an alignment (PI = intersection of the tangent directions at NĐ and NC; T1 = PI→NĐ; P = PI→P point), as `YTCA` does, versus Core's formulas | Compare on the S1 fixture to 0.01 m |

**Decisions to record:** style route (S4 template import, or S5 code-built); abbreviation route (API, or documented manual step); box frame (`ShowBorders`, or a separate polyline); curve update route (S9 edit in place, or delete and re-add); UI build route (S12 XAML, or WPF built in C# code with no XAML if the macOS build fails).

```bash
git add docs/spikes/2026-10-spike-report.md
git commit -m "docs: spike 2 — Civil 3D 2021 curve element APIs"
```

---

### Task 1: Alignment segments and curve grouping

A curve group is one PI's worth of geometry: `[Spiral] Arc [Spiral]`. The host flattens alignment sub-entities into `AlignmentSegment`s. Core decides what counts as a curve and computes the total deflection. Deflection is the arc's L/R plus L/(2R) for each spiral.

**Files:**
- Create: `src/C3DTools.Core/Curves/AlignmentSegment.cs`
- Create: `src/C3DTools.Core/Curves/CurveGroup.cs`
- Create: `src/C3DTools.Core/Curves/CurveGrouper.cs`
- Test: `tests/C3DTools.Core.Tests/Curves/CurveGrouperTests.cs`

**Step 1: Write the failing tests**

```csharp
using System;
using System.Collections.Generic;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class CurveGrouperTests
{
    private static AlignmentSegment Line(double start, double length) =>
        new AlignmentSegment(SegmentKind.Line, start, length, 0, 0);
    private static AlignmentSegment Arc(double start, double length, double r, int turn = 1) =>
        new AlignmentSegment(SegmentKind.Arc, start, length, r, turn);
    private static AlignmentSegment Spiral(double start, double length, double r, int turn = 1) =>
        new AlignmentSegment(SegmentKind.Spiral, start, length, r, turn);

    [Fact]
    public void Groups_spiral_arc_spiral_into_one_curve()
    {
        var segments = new List<AlignmentSegment>
        {
            Line(0, 100),
            Spiral(100, 50, 200),
            Arc(150, 159.43951, 200),
            Spiral(309.43951, 50, 200),
            Line(359.43951, 80),
        };

        var result = CurveGrouper.Group(segments);

        Assert.Empty(result.Warnings);
        var g = Assert.Single(result.Groups);
        Assert.Equal(1, g.Index);
        Assert.Equal(200, g.Radius, 6);
        Assert.Equal(50, g.SpiralIn, 6);
        Assert.Equal(50, g.SpiralOut, 6);
        Assert.Equal(60, g.DeltaDegrees, 4);
        Assert.Equal(100, g.StartStation, 6);        // NĐ
        Assert.Equal(150, g.ArcStartStation, 6);     // TĐ
        Assert.Equal(309.43951, g.ArcEndStation, 5); // TC
        Assert.Equal(359.43951, g.EndStation, 5);    // NC
    }

    [Fact]
    public void Simple_arc_is_a_curve_without_spirals()
    {
        var result = CurveGrouper.Group(new[] { Line(0, 50), Arc(50, 157.0796, 100, -1), Line(207.0796, 50) });

        var g = Assert.Single(result.Groups);
        Assert.Equal(0, g.SpiralIn);
        Assert.Equal(0, g.SpiralOut);
        Assert.Equal(90, g.DeltaDegrees, 3);
        Assert.Equal(-1, g.Turn);
    }

    [Fact]
    public void Numbers_curves_in_station_order()
    {
        var result = CurveGrouper.Group(new[]
        {
            Line(0, 10), Arc(10, 20, 100), Line(30, 10), Arc(40, 20, 150), Line(60, 10),
        });

        Assert.Equal(new[] { 1, 2 }, result.Groups.ConvertAll(g => g.Index));
    }

    [Fact]
    public void Compound_curve_is_skipped_with_warning()
    {
        var result = CurveGrouper.Group(new[] { Line(0, 10), Arc(10, 20, 100), Arc(30, 20, 150), Line(50, 10) });

        Assert.Empty(result.Groups);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("Km0+010.00", warning);
    }

    [Fact]
    public void Spiral_that_does_not_match_arc_radius_is_skipped()
    {
        var result = CurveGrouper.Group(new[] { Spiral(0, 50, 250), Arc(50, 100, 200), Line(150, 10) });

        Assert.Empty(result.Groups);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Spiral_only_run_is_skipped()
    {
        var result = CurveGrouper.Group(new[] { Line(0, 10), Spiral(10, 40, 200), Spiral(50, 40, 200), Line(90, 10) });

        Assert.Empty(result.Groups);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => CurveGrouper.Group(null));
    }
}
```

**Step 2: Run to verify failure**

Run: `dotnet test C3DTools.Core.slnf --filter FullyQualifiedName~CurveGrouperTests`
Expected: build FAIL, `The type or namespace name 'Curves' does not exist`.

**Step 3: Implement**

`src/C3DTools.Core/Curves/AlignmentSegment.cs`:

```csharp
namespace C3DTools.Core.Curves;

public enum SegmentKind { Line, Arc, Spiral }

/// <summary>One alignment sub-entity as plain data. The host fills it from Civil 3D.</summary>
public sealed class AlignmentSegment
{
    public AlignmentSegment(SegmentKind kind, double startStation, double length, double radius, int turn)
    {
        Kind = kind;
        StartStation = startStation;
        Length = length;
        Radius = radius;
        Turn = turn;
    }

    public SegmentKind Kind { get; }
    public double StartStation { get; }
    public double Length { get; }

    /// <summary>Arc radius; for a spiral, the radius at its curved end. 0 for lines.</summary>
    public double Radius { get; }

    /// <summary>+1 turns right (clockwise), -1 turns left, 0 for lines.</summary>
    public int Turn { get; }
}
```

`src/C3DTools.Core/Curves/CurveGroup.cs`:

```csharp
using System;

namespace C3DTools.Core.Curves;

/// <summary>One PI: optional spiral in, circular arc, optional spiral out.</summary>
public sealed class CurveGroup
{
    public CurveGroup(int index, double radius, int turn, double deltaRadians,
        double spiralIn, double spiralOut, double startStation, double arcStartStation, double arcEndStation)
    {
        Index = index;
        Radius = radius;
        Turn = turn;
        DeltaRadians = deltaRadians;
        SpiralIn = spiralIn;
        SpiralOut = spiralOut;
        StartStation = startStation;
        ArcStartStation = arcStartStation;
        ArcEndStation = arcEndStation;
    }

    /// <summary>1-based PI number ("Đ1", "Đ2"...).</summary>
    public int Index { get; }
    public double Radius { get; }
    public int Turn { get; }
    public double DeltaRadians { get; }
    public double DeltaDegrees => DeltaRadians * 180.0 / Math.PI;
    public double SpiralIn { get; }
    public double SpiralOut { get; }

    /// <summary>NĐ when there is a spiral in, otherwise TĐ.</summary>
    public double StartStation { get; }
    public double ArcStartStation { get; }
    public double ArcEndStation { get; }
    public double EndStation => ArcEndStation + SpiralOut;
    public double ArcMidStation => (ArcStartStation + ArcEndStation) / 2;
}
```

`src/C3DTools.Core/Curves/CurveGrouper.cs`:

```csharp
using System;
using System.Collections.Generic;
using C3DTools.Core.Stations;

namespace C3DTools.Core.Curves;

public sealed class CurveGroupingResult
{
    public CurveGroupingResult(List<CurveGroup> groups, List<string> warnings)
    {
        Groups = groups;
        Warnings = warnings;
    }

    public List<CurveGroup> Groups { get; }
    public List<string> Warnings { get; }
}

public static class CurveGrouper
{
    // Civil 3D stores radii as doubles; a spiral's end radius can differ from the arc's in the last digits.
    private const double RadiusTolerance = 1e-4;

    public static CurveGroupingResult Group(IReadOnlyList<AlignmentSegment> segments)
    {
        if (segments == null) throw new ArgumentNullException(nameof(segments));

        var groups = new List<CurveGroup>();
        var warnings = new List<string>();
        var i = 0;
        while (i < segments.Count)
        {
            if (segments[i].Kind == SegmentKind.Line)
            {
                i++;
                continue;
            }

            var run = new List<AlignmentSegment>();
            while (i < segments.Count && segments[i].Kind != SegmentKind.Line) run.Add(segments[i++]);

            var group = TryBuild(run, groups.Count + 1, out var problem);
            if (group != null) groups.Add(group);
            else warnings.Add($"Đường cong tại {StationFormatter.Format(run[0].StartStation, 2)}: {problem} Bỏ qua.");
        }

        return new CurveGroupingResult(groups, warnings);
    }

    private static CurveGroup TryBuild(List<AlignmentSegment> run, int index, out string problem)
    {
        problem = null;
        var arcs = run.FindAll(s => s.Kind == SegmentKind.Arc);
        if (arcs.Count != 1)
        {
            problem = arcs.Count == 0
                ? "chỉ có đường cong chuyển tiếp, không có cong tròn."
                : "cong ghép nhiều bán kính chưa được hỗ trợ.";
            return null;
        }

        var arc = arcs[0];
        var arcIndex = run.IndexOf(arc);
        if (arcIndex > 1 || run.Count - arcIndex - 1 > 1)
        {
            problem = "có nhiều hơn một đoạn chuyển tiếp ở một phía.";
            return null;
        }

        var spiralIn = arcIndex == 1 ? run[0] : null;
        var spiralOut = arcIndex + 1 < run.Count ? run[arcIndex + 1] : null;
        foreach (var s in new[] { spiralIn, spiralOut })
        {
            if (s == null) continue;
            if (s.Turn != arc.Turn || Math.Abs(s.Radius - arc.Radius) > RadiusTolerance * arc.Radius)
            {
                problem = "đường cong chuyển tiếp không nối đúng bán kính cong tròn.";
                return null;
            }
        }

        var l1 = spiralIn?.Length ?? 0;
        var l2 = spiralOut?.Length ?? 0;
        var delta = arc.Length / arc.Radius + (l1 + l2) / (2 * arc.Radius);
        return new CurveGroup(index, arc.Radius, arc.Turn, delta, l1, l2,
            run[0].StartStation, arc.StartStation, arc.StartStation + arc.Length);
    }
}
```

**Step 4: Run tests.** Run the same command. Expected: 7 passed.

**Step 5: Commit**

```bash
git add src/C3DTools.Core/Curves tests/C3DTools.Core.Tests/Curves
git commit -m "feat(core): group alignment segments into TCVN curve groups"
```

---

### Task 2: Curve element math (A, R, T1, T2, P, K, K0)

The clothoid end point uses series to θ⁵, where θ = L/(2R):
X = L(1 − θ²/10 + θ⁴/216), Y = L(θ/3 − θ³/42 + θ⁵/1320).
The shift is p = Y − R(1 − cos θ) and the tangent offset is m = X − R sin θ.
With dᵢ = R + pᵢ, the tangents are T1 = m1 + (d2 − d1 cos α)/sin α and T2 = m2 + (d1 − d2 cos α)/sin α.
The external is P = |PI→centre| − R. The lengths are K = Rα + (L1+L2)/2 and K0 = Rα − (L1+L2)/2.
With L1 = L2 these reduce to the textbook forms T = (R+p)·tan(α/2) + m and P = (R+p)/cos(α/2) − R.

**Files:**
- Create: `src/C3DTools.Core/Curves/CurveElements.cs`
- Test: `tests/C3DTools.Core.Tests/Curves/CurveElementsTests.cs`

**Step 1: Failing tests.** The expected values are hand-computed. Case 2 matches the approximations L²/24R = 0.5208 and L/2 − L³/240R² = 24.9870.

```csharp
using System;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class CurveElementsTests
{
    private static double Rad(double deg) => deg * Math.PI / 180;

    [Fact]
    public void Circular_curve_matches_textbook_formulas()
    {
        var e = CurveElementsCalculator.Compute(100, Rad(90), 0, 0);

        Assert.Equal(100, e.T1, 4);
        Assert.Equal(100, e.T2, 4);
        Assert.Equal(41.4214, e.P, 4);
        Assert.Equal(157.0796, e.K, 4);
        Assert.Equal(e.K, e.K0, 9);
    }

    [Fact]
    public void Symmetric_spiral_curve()
    {
        var e = CurveElementsCalculator.Compute(200, Rad(60), 50, 50);

        Assert.Equal(0.5205, e.Shift1, 4);
        Assert.Equal(24.9870, e.TangentOffset1, 4);
        Assert.Equal(140.7576, e.T1, 4);
        Assert.Equal(140.7576, e.T2, 4);
        Assert.Equal(31.5412, e.P, 4);
        Assert.Equal(259.4395, e.K, 4);
        Assert.Equal(159.4395, e.K0, 4);
    }

    [Fact]
    public void Asymmetric_spiral_curve_has_different_tangents()
    {
        var e = CurveElementsCalculator.Compute(300, Rad(45), 60, 40);

        Assert.Equal(154.0685, e.T1, 4);
        Assert.Equal(144.7458, e.T2, 4);
        Assert.Equal(25.1086, e.P, 4);
        Assert.Equal(285.6194, e.K, 4);
    }

    [Theory]
    [InlineData(0, 30, 0, 0)]
    [InlineData(100, 0, 0, 0)]
    [InlineData(100, 180, 0, 0)]
    [InlineData(100, 30, -1, 0)]
    public void Rejects_invalid_input(double r, double deltaDeg, double l1, double l2)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CurveElementsCalculator.Compute(r, Rad(deltaDeg), l1, l2));
    }

    [Fact]
    public void Rejects_spirals_longer_than_the_deflection_allows()
    {
        // α = 10° needs (L1+L2)/(2R) ≤ 0.1745; 100/(2·100) = 0.5.
        var ex = Assert.Throws<ArgumentException>(() => CurveElementsCalculator.Compute(100, Rad(10), 50, 50));
        Assert.Contains("chuyển tiếp", ex.Message);
    }
}
```

**Step 2: Run to verify failure:** `dotnet test C3DTools.Core.slnf --filter FullyQualifiedName~CurveElementsTests`, expected build FAIL.

**Step 3: Implement** `src/C3DTools.Core/Curves/CurveElements.cs`:

```csharp
using System;

namespace C3DTools.Core.Curves;

public sealed class CurveElements
{
    public double T1 { get; set; }
    public double T2 { get; set; }
    public double P { get; set; }

    /// <summary>Total curve length including spirals.</summary>
    public double K { get; set; }

    /// <summary>Circular arc length only (what Civil 3D calls Curve Length).</summary>
    public double K0 { get; set; }

    public double Shift1 { get; set; }
    public double Shift2 { get; set; }
    public double TangentOffset1 { get; set; }
    public double TangentOffset2 { get; set; }
}

/// <summary>TCVN curve elements for a clothoid–arc–clothoid curve (L = 0 means no spiral).</summary>
public static class CurveElementsCalculator
{
    public static CurveElements Compute(double radius, double deltaRadians, double spiralIn, double spiralOut)
    {
        if (!(radius > 0)) throw new ArgumentOutOfRangeException(nameof(radius));
        if (!(deltaRadians > 0 && deltaRadians < Math.PI)) throw new ArgumentOutOfRangeException(nameof(deltaRadians));
        if (spiralIn < 0) throw new ArgumentOutOfRangeException(nameof(spiralIn));
        if (spiralOut < 0) throw new ArgumentOutOfRangeException(nameof(spiralOut));

        var circularLength = radius * deltaRadians - (spiralIn + spiralOut) / 2;
        if (circularLength < 0)
            throw new ArgumentException("Chiều dài đường cong chuyển tiếp quá lớn so với góc chuyển hướng.");

        Shift(spiralIn, radius, out var p1, out var m1);
        Shift(spiralOut, radius, out var p2, out var m2);
        var d1 = radius + p1;
        var d2 = radius + p2;
        var sin = Math.Sin(deltaRadians);
        var cos = Math.Cos(deltaRadians);

        // PI at the origin, tangent 1 along +x; the shifted centre sits d1 from tangent 1 and d2 from tangent 2.
        var centreX = (d1 * cos - d2) / sin;

        return new CurveElements
        {
            T1 = m1 + (d2 - d1 * cos) / sin,
            T2 = m2 + (d1 - d2 * cos) / sin,
            P = Math.Sqrt(centreX * centreX + d1 * d1) - radius,
            K = radius * deltaRadians + (spiralIn + spiralOut) / 2,
            K0 = circularLength,
            Shift1 = p1,
            Shift2 = p2,
            TangentOffset1 = m1,
            TangentOffset2 = m2,
        };
    }

    private static void Shift(double length, double radius, out double p, out double m)
    {
        if (length == 0)
        {
            p = 0;
            m = 0;
            return;
        }

        var t = length / (2 * radius);
        var x = length * (1 - t * t / 10 + Math.Pow(t, 4) / 216);
        var y = length * (t / 3 - Math.Pow(t, 3) / 42 + Math.Pow(t, 5) / 1320);
        p = y - radius * (1 - Math.Cos(t));
        m = x - radius * Math.Sin(t);
    }
}
```

**Step 4: Run tests,** expected: all pass. **Step 5: Commit**, with message `feat(core): TCVN curve elements with clothoid shift`.

> Spike S2 checks this against Civil 3D. If Civil 3D's total tangent differs by more than 0.01 m for L/R > 0.5, add the θ⁶/θ⁷ series terms and add a test for that case.

---

### Task 3: Angle text in degrees-minutes-seconds

Picture 4 shows `31d31'52"`; TCVN wants `31°31'52"`. Rounding must carry: 29°59'59.6" becomes `30°00'00"`.

**Files:**
- Create: `src/C3DTools.Core/Curves/AngleFormatter.cs`
- Test: `tests/C3DTools.Core.Tests/Curves/AngleFormatterTests.cs`

**Step 1: Failing tests**

```csharp
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class AngleFormatterTests
{
    [Theory]
    [InlineData(31.531111, 0, "31°31'52\"")]
    [InlineData(29.99999, 0, "30°00'00\"")]
    [InlineData(0.5, 0, "0°30'00\"")]
    [InlineData(5.0021, 1, "5°00'07.6\"")]
    [InlineData(-12.25, 0, "-12°15'00\"")]
    public void Formats_dms(double degrees, int secondDecimals, string expected)
    {
        Assert.Equal(expected, AngleFormatter.Dms(degrees, secondDecimals));
    }

    [Fact]
    public void Ignores_vietnamese_culture()
    {
        TestCulture.Run("vi-VN", () => Assert.Equal("5°00'07.6\"", AngleFormatter.Dms(5.0021, 1)));
    }
}
```

**Step 3: Implement**

```csharp
using System;
using System.Globalization;

namespace C3DTools.Core.Curves;

public static class AngleFormatter
{
    public static string Dms(double degrees, int secondDecimals = 0)
    {
        if (secondDecimals < 0 || secondDecimals > 3) throw new ArgumentOutOfRangeException(nameof(secondDecimals));

        // Round once in seconds so the carry into minutes and degrees is exact.
        var totalSeconds = Math.Round(Math.Abs(degrees) * 3600, secondDecimals, MidpointRounding.AwayFromZero);
        var d = (long)Math.Floor(totalSeconds / 3600);
        var m = (int)Math.Floor((totalSeconds - d * 3600) / 60);
        var s = Math.Round(totalSeconds - d * 3600 - m * 60, secondDecimals, MidpointRounding.AwayFromZero);

        var width = secondDecimals == 0 ? 2 : 3 + secondDecimals;
        var sign = degrees < 0 && totalSeconds > 0 ? "-" : "";
        return sign + d.ToString(CultureInfo.InvariantCulture) + "°"
            + m.ToString("00", CultureInfo.InvariantCulture) + "'"
            + s.ToString("F" + secondDecimals, CultureInfo.InvariantCulture).PadLeft(width, '0') + "\"";
    }
}
```

Run, pass, then commit with `feat(core): DMS angle formatting for curve labels`.

---

### Task 4: TCVN 4054 curve rules in the preset, and the rule checker

The rule *values* come from the standard, and the **domain owner** enters them (decision D5). The code ships the structure plus an empty preset. As with `PipeRules`, there are no hard-coded defaults.

**Files:**
- Create: `src/C3DTools.Core/Curves/CurveRules.cs`
- Create: `src/C3DTools.Core/Curves/CurveRuleChecker.cs`
- Modify: `src/C3DTools.Core/Presets/ProjectPreset.cs` (add `DesignSpeed`, `CurveRules`, `CurveBox`)
- Create: `bundle/Resources/tcvn4054.preset.json` (entered by the domain owner, Step 6)
- Test: `tests/C3DTools.Core.Tests/Curves/CurveRuleCheckerTests.cs`
- Modify: `tests/C3DTools.Core.Tests/Presets/PresetSerializerTests.cs` (round-trip test)

**Step 1: Failing tests.** They use synthetic numbers, **not** standard values.

```csharp
using System.Collections.Generic;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class CurveRuleCheckerTests
{
    private static readonly CurveRules Rules = new CurveRules
    {
        MinRadius = new List<SpeedRadiusRule> { new SpeedRadiusRule { DesignSpeed = 60, MinRadius = 125, NormalRadius = 180 } },
        MinSpiral = new List<RadiusRangeRule>
        {
            new RadiusRangeRule { DesignSpeed = 60, RadiusFrom = 125, RadiusTo = 250, Value = 50 },
            new RadiusRangeRule { DesignSpeed = 60, RadiusFrom = 250, RadiusTo = 1000, Value = 40 },
        },
        Widening = new List<RadiusRangeRule>
        {
            new RadiusRangeRule { RadiusFrom = 150, RadiusTo = 250, Value = 0.6 },
            new RadiusRangeRule { RadiusFrom = 100, RadiusTo = 150, Value = 0.9 },
        },
    };

    private static CurveGroup Curve(double r, double l) => new CurveGroup(1, r, 1, 1.0, l, l, 0, l, l + 100);

    [Fact]
    public void Passing_curve_has_no_issues_and_gets_widening()
    {
        var result = CurveRuleChecker.Check(Curve(200, 50), 60, Rules);

        Assert.Empty(result.Issues);
        Assert.Equal(0.6, result.Widening);
    }

    [Fact]
    public void Radius_below_minimum_is_reported()
    {
        var result = CurveRuleChecker.Check(Curve(110, 50), 60, Rules);

        Assert.Contains(result.Issues, i => i.Code == CurveIssueCode.RadiusTooSmall);
    }

    [Fact]
    public void Radius_between_limit_and_normal_is_a_warning_only()
    {
        var result = CurveRuleChecker.Check(Curve(150, 50), 60, Rules);

        var issue = Assert.Single(result.Issues);
        Assert.Equal(CurveIssueCode.RadiusBelowNormal, issue.Code);
    }

    [Fact]
    public void Spiral_shorter_than_minimum_is_reported()
    {
        var result = CurveRuleChecker.Check(Curve(200, 30), 60, Rules);

        Assert.Contains(result.Issues, i => i.Code == CurveIssueCode.SpiralTooShort);
    }

    [Fact]
    public void Range_upper_bound_is_inclusive()   // decision D2
    {
        Assert.Equal(0.9, CurveRuleChecker.Check(Curve(150, 50), 60, Rules).Widening);
    }

    [Fact]
    public void Unknown_design_speed_is_reported_not_guessed()
    {
        var result = CurveRuleChecker.Check(Curve(200, 50), 80, Rules);

        Assert.Contains(result.Issues, i => i.Code == CurveIssueCode.NoRuleForSpeed);
    }

    [Fact]
    public void Radius_outside_widening_table_gives_no_widening()
    {
        Assert.Null(CurveRuleChecker.Check(Curve(600, 50), 60, Rules).Widening);
    }
}
```

**Step 3: Implement** `CurveRules.cs`:

```csharp
using System.Collections.Generic;

namespace C3DTools.Core.Curves;

/// <summary>TCVN tables transcribed into the project preset. No built-in values.</summary>
public sealed class CurveRules
{
    public string Source { get; set; } = "";   // e.g. "TCVN 4054:2005, Bảng 11/12/14"
    public List<SpeedRadiusRule> MinRadius { get; set; } = new List<SpeedRadiusRule>();
    public List<RadiusRangeRule> MinSpiral { get; set; } = new List<RadiusRangeRule>();
    public List<RadiusRangeRule> Widening { get; set; } = new List<RadiusRangeRule>();
}

public sealed class SpeedRadiusRule
{
    public double DesignSpeed { get; set; }

    /// <summary>Rmin giới hạn: below this is an error.</summary>
    public double MinRadius { get; set; }

    /// <summary>Rmin thông thường: below this is a warning; also the suggested R.</summary>
    public double NormalRadius { get; set; }
}

/// <summary>Value applies when RadiusFrom &lt; R ≤ RadiusTo. DesignSpeed 0 = any speed.</summary>
public sealed class RadiusRangeRule
{
    public double DesignSpeed { get; set; }
    public double RadiusFrom { get; set; }
    public double RadiusTo { get; set; }
    public double Value { get; set; }
}

public sealed class CurveBoxOptions
{
    public double TextHeight { get; set; } = 2.5;
    public double OffsetFromAlignment { get; set; } = 10;
    public int LengthDecimals { get; set; } = 2;
    public int AngleSecondDecimals { get; set; } = 0;
    public bool ShowZeroValues { get; set; }   // decision D4
}
```

`CurveRuleChecker.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace C3DTools.Core.Curves;

public enum CurveIssueCode { NoRuleForSpeed, RadiusTooSmall, RadiusBelowNormal, SpiralTooShort }

public sealed class CurveIssue
{
    public CurveIssue(CurveIssueCode code, string message)
    {
        Code = code;
        Message = message;
    }

    public CurveIssueCode Code { get; }
    public string Message { get; }
}

public sealed class CurveCheckResult
{
    public List<CurveIssue> Issues { get; } = new List<CurveIssue>();

    /// <summary>Null when the widening table has no row for this radius.</summary>
    public double? Widening { get; set; }
}

public static class CurveRuleChecker
{
    public static CurveCheckResult Check(CurveGroup curve, double designSpeed, CurveRules rules)
    {
        if (curve == null) throw new ArgumentNullException(nameof(curve));
        if (rules == null) throw new ArgumentNullException(nameof(rules));

        var result = new CurveCheckResult();
        var name = "Đ" + curve.Index.ToString(CultureInfo.InvariantCulture);
        var speedText = designSpeed.ToString("0", CultureInfo.InvariantCulture);

        var minRadius = rules.MinRadius.FirstOrDefault(r => r.DesignSpeed == designSpeed);
        if (minRadius == null)
            result.Issues.Add(new CurveIssue(CurveIssueCode.NoRuleForSpeed,
                $"Preset chưa có Rmin cho vận tốc {speedText} km/h."));
        else if (curve.Radius < minRadius.MinRadius)
            result.Issues.Add(new CurveIssue(CurveIssueCode.RadiusTooSmall,
                $"{name}: R = {M(curve.Radius)} nhỏ hơn Rmin giới hạn = {M(minRadius.MinRadius)} (V = {speedText} km/h)."));
        else if (curve.Radius < minRadius.NormalRadius)
            result.Issues.Add(new CurveIssue(CurveIssueCode.RadiusBelowNormal,
                $"{name}: R = {M(curve.Radius)} nhỏ hơn Rmin thông thường = {M(minRadius.NormalRadius)}."));

        var minSpiral = Find(rules.MinSpiral, curve.Radius, designSpeed);
        if (minSpiral != null)
        {
            var shortest = Math.Min(curve.SpiralIn, curve.SpiralOut);
            if (shortest < minSpiral.Value)
                result.Issues.Add(new CurveIssue(CurveIssueCode.SpiralTooShort,
                    $"{name}: L = {M(shortest)} nhỏ hơn Lmin = {M(minSpiral.Value)}."));
        }

        result.Widening = Find(rules.Widening, curve.Radius, designSpeed)?.Value;
        return result;
    }

    private static RadiusRangeRule Find(IEnumerable<RadiusRangeRule> table, double radius, double speed) =>
        table.FirstOrDefault(r => (r.DesignSpeed == 0 || r.DesignSpeed == speed)
                                  && radius > r.RadiusFrom && radius <= r.RadiusTo);

    private static string M(double v) => v.ToString("0.00", CultureInfo.InvariantCulture) + " m";
}
```

**Step 4: Preset.** Add to `ProjectPreset`. The fields are additive and nullable, so `SchemaVersion` stays 1.

```csharp
    /// <summary>km/h. Default for CTYTC's design-speed prompt.</summary>
    public double? DesignSpeed { get; set; }

    /// <summary>Null until the project defines TCVN tables; CTYTC then skips checks and W.</summary>
    public CurveRules CurveRules { get; set; }

    public CurveBoxOptions CurveBox { get; set; } = new CurveBoxOptions();
```

Add a round-trip test to `PresetSerializerTests`: a preset with one rule in each table goes through Save and Load and keeps its values. Also check that the existing tests still pass.

**Step 5: Run all Core tests,** then commit with `feat(core): TCVN curve rules in preset and rule checker`.

**Step 6 (domain owner):** Create `bundle/Resources/tcvn4054.preset.json`. Seed `MinRadius` from the table in `YTC.lsp` (decision D6), as `DesignSpeed / MinRadius / NormalRadius`:

| V | 120 | 100 | 80 | 60 | 40 | 30 | 20 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Rmin giới hạn | 650 | 400 | 250 | 125 | 60 | 30 | 15 |
| Rmin thông thường | 1000 | 700 | 400 | 250 | 125 | 60 | 50 |

Add `MinSpiral` (D5), and optionally `Widening` (D2), from the standard. Put the table numbers in `CurveRules.Source`, and have a second engineer check it against TCVN 4054:2005. This is data entry, not code, so don't invent values.

Because Wb and Wl are two values (D2), `CurveCheckResult.Widening` becomes only a *suggestion* for the "Gợi ý" button. The per-curve Wb/Wl the user types lives in `CurveInput` (Task 5).

---

### Task 5: Route designer: PIs + inputs → curves, stations, issues

This is the LISP's main loop, rewritten as a pure function. The dialog calls it on every edit, so it must be fast, side-effect free and never throw on bad *user* input. Bad input becomes an issue on that row.

**Files:**
- Create: `src/C3DTools.Core/Curves/RouteDesigner.cs` (`PlanPoint`, `CurveInput`, `DesignedCurve`, `RouteDesign`, `RouteDesigner`)
- Modify: `src/C3DTools.Core/Curves/CurveRuleChecker.cs` (new issue codes, `IsError`)
- Test: `tests/C3DTools.Core.Tests/Curves/RouteDesignerTests.cs`

**Step 1: Failing tests.** The expected stations are hand-computed from Task 2's elements.

```csharp
using System;
using System.Collections.Generic;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

public class RouteDesignerTests
{
    private static PlanPoint P(double x, double y) => new PlanPoint(x, y);
    private static CurveInput R(double r, double l1 = 0, double l2 = 0) =>
        new CurveInput { Radius = r, SpiralIn = l1, SpiralOut = l2 };

    private static readonly PlanPoint[] Square = { P(0, 0), P(100, 0), P(100, 100) };

    [Fact]
    public void Circular_curve_on_left_turn()
    {
        var d = RouteDesigner.Design(Square, 0, new[] { R(50) }, 60, null);

        var c = Assert.Single(d.Curves);
        Assert.Equal(1, c.Number);
        Assert.Equal(-1, c.Turn);
        Assert.Equal(50, c.TangentBefore, 4);
        Assert.Equal(50, c.StationStart, 4);            // TĐ
        Assert.Equal(128.5398, c.StationArcEnd, 4);     // TC
        Assert.Equal(178.5398, d.EndStation, 4);
        Assert.True(d.CanApply);
    }

    [Fact]
    public void Asymmetric_spirals_give_nd_td_tc_nc()
    {
        var a = Math.PI / 4;
        var pis = new[] { P(0, 0), P(500, 0), P(500 + 500 * Math.Cos(a), 500 * Math.Sin(a)) };

        var d = RouteDesigner.Design(pis, 0, new[] { R(300, 60, 40) }, 60, null);

        var c = Assert.Single(d.Curves);
        Assert.Equal(345.9315, c.StationStart, 4);      // NĐ
        Assert.Equal(405.9315, c.StationArcStart, 4);   // TĐ
        Assert.Equal(591.5509, c.StationArcEnd, 4);     // TC
        Assert.Equal(631.5509, c.StationEnd, 4);        // NC
        Assert.Equal(986.8051, d.EndStation, 4);
    }

    [Fact]
    public void Start_station_shifts_everything()
    {
        var d = RouteDesigner.Design(Square, 1000, new[] { R(50) }, 60, null);

        Assert.Equal(1050, d.Curves[0].StationStart, 4);
    }

    [Fact]
    public void Collinear_pi_has_no_curve_and_no_number()
    {
        var pis = new[] { P(0, 0), P(50, 0), P(100, 0), P(100, 100) };

        var d = RouteDesigner.Design(pis, 0, new[] { R(50), R(50) }, 60, null);

        var c = Assert.Single(d.Curves);
        Assert.Equal(1, c.Number);
        Assert.Equal(2, c.PiIndex);
        Assert.Equal(50, c.StationStart, 4);
    }

    [Fact]
    public void Overlap_with_route_start_is_an_error()
    {
        var d = RouteDesigner.Design(Square, 0, new[] { R(150) }, 60, null);

        Assert.Contains(d.Curves[0].Issues, i => i.Code == CurveIssueCode.Overlap && i.IsError);
        Assert.False(d.CanApply);
    }

    [Fact]
    public void Spiral_too_long_for_deflection_is_an_error_not_an_exception()
    {
        var d = RouteDesigner.Design(Square, 0, new[] { R(100, 200, 200) }, 60, null);

        Assert.Contains(d.Curves[0].Issues, i => i.Code == CurveIssueCode.SpiralTooLong);
        Assert.Null(d.Curves[0].Elements);
        Assert.False(d.CanApply);
    }

    [Fact]
    public void Zero_radius_is_an_error()
    {
        var d = RouteDesigner.Design(Square, 0, new[] { R(0) }, 60, null);

        Assert.Contains(d.Curves[0].Issues, i => i.Code == CurveIssueCode.InvalidInput);
    }

    [Fact]
    public void Rule_warnings_do_not_block_apply()
    {
        var rules = new CurveRules
        {
            MinRadius = new List<SpeedRadiusRule> { new SpeedRadiusRule { DesignSpeed = 60, MinRadius = 125, NormalRadius = 250 } },
        };

        var d = RouteDesigner.Design(Square, 0, new[] { R(50) }, 60, rules);

        Assert.Contains(d.Curves[0].Issues, i => i.Code == CurveIssueCode.RadiusTooSmall && !i.IsError);
        Assert.True(d.CanApply);
    }

    [Fact]
    public void Input_count_must_match_interior_pis()
    {
        Assert.Throws<ArgumentException>(() => RouteDesigner.Design(Square, 0, new CurveInput[0], 60, null));
    }

    [Fact]
    public void Removes_consecutive_duplicate_points()
    {
        var clean = RouteDesigner.RemoveDuplicatePoints(new[] { P(0, 0), P(0, 0), P(10, 0), P(10, 1e-9) });

        Assert.Equal(2, clean.Count);
    }
}
```

**Step 2:** Run `dotnet test C3DTools.Core.slnf --filter FullyQualifiedName~RouteDesignerTests` and confirm it FAILs.

**Step 3: Implement.** First extend `CurveRuleChecker.cs`:

```csharp
public enum CurveIssueCode
{
    NoRuleForSpeed, RadiusTooSmall, RadiusBelowNormal, SpiralTooShort,
    InvalidInput, SpiralTooLong, Overlap,
}

// in CurveIssue:
    /// <summary>Errors block Áp dụng; warnings (TCVN checks) only inform, as in YTC.lsp.</summary>
    public bool IsError =>
        Code == CurveIssueCode.InvalidInput || Code == CurveIssueCode.SpiralTooLong || Code == CurveIssueCode.Overlap;
```

`src/C3DTools.Core/Curves/RouteDesigner.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Tables;

namespace C3DTools.Core.Curves;

public readonly struct PlanPoint
{
    public PlanPoint(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; }
    public double Y { get; }
}

/// <summary>What the user types for one PI. Wb = widening inside the curve (bụng), Wl = outside (lưng).</summary>
public sealed class CurveInput
{
    public double Radius { get; set; }
    public double SpiralIn { get; set; }
    public double SpiralOut { get; set; }
    public double Wb { get; set; }
    public double Wl { get; set; }

    public CurveInput Clone() => (CurveInput)MemberwiseClone();
}

public sealed class DesignedCurve
{
    /// <summary>Index into the PI list (1..n-2).</summary>
    public int PiIndex { get; set; }

    /// <summary>Đ number; collinear PIs are skipped.</summary>
    public int Number { get; set; }

    public int Turn { get; set; }
    public double DeltaRadians { get; set; }
    public CurveInput Input { get; set; }

    /// <summary>Null when the input is invalid (see Issues).</summary>
    public CurveElements Elements { get; set; }

    public double TangentBefore { get; set; }
    public double StationStart { get; set; }
    public double StationArcStart { get; set; }
    public double StationArcEnd { get; set; }
    public double StationEnd { get; set; }
    public double StationArcMid => (StationArcStart + StationArcEnd) / 2;
    public double? SuggestedWidening { get; set; }
    public List<CurveIssue> Issues { get; } = new List<CurveIssue>();

    public CurveGroup ToGroup() => new CurveGroup(Number, Input.Radius, Turn, DeltaRadians,
        Input.SpiralIn, Input.SpiralOut, StationStart, StationArcStart, StationArcEnd);
}

public sealed class RouteDesign
{
    public List<DesignedCurve> Curves { get; } = new List<DesignedCurve>();
    public double EndStation { get; set; }
    public bool CanApply => Curves.All(c => c.Issues.All(i => !i.IsError));
}

public static class RouteDesigner
{
    private const double Tolerance = 1e-4;

    public static List<PlanPoint> RemoveDuplicatePoints(IEnumerable<PlanPoint> points)
    {
        var result = new List<PlanPoint>();
        foreach (var p in points)
            if (result.Count == 0 || Distance(result[result.Count - 1], p) > 1e-6) result.Add(p);
        return result;
    }

    public static RouteDesign Design(IReadOnlyList<PlanPoint> pis, double startStation,
        IReadOnlyList<CurveInput> inputs, double designSpeed, CurveRules rules)
    {
        if (pis == null) throw new ArgumentNullException(nameof(pis));
        if (inputs == null) throw new ArgumentNullException(nameof(inputs));
        if (pis.Count < 2) throw new ArgumentException("Tuyến cần ít nhất 2 đỉnh.", nameof(pis));
        if (inputs.Count != pis.Count - 2)
            throw new ArgumentException($"Cần {pis.Count - 2} bộ thông số cong.", nameof(inputs));

        var design = new RouteDesign();
        var station = startStation;
        var previousT2 = 0.0;
        for (var i = 1; i < pis.Count - 1; i++)
        {
            var legIn = Distance(pis[i - 1], pis[i]);
            Deflection(pis[i - 1], pis[i], pis[i + 1], out var delta, out var turn);
            if (delta < 1e-6)
            {
                station += legIn - previousT2;   // straight through, as ytc: does
                previousT2 = 0;
                continue;
            }

            var input = inputs[i - 1] ?? new CurveInput();
            var curve = new DesignedCurve
            {
                PiIndex = i, Number = design.Curves.Count + 1, Turn = turn, DeltaRadians = delta, Input = input,
            };
            design.Curves.Add(curve);

            var name = "Đ" + curve.Number;
            if (!(input.Radius > 0) || input.SpiralIn < 0 || input.SpiralOut < 0 || delta > Math.PI - 1e-6)
                curve.Issues.Add(new CurveIssue(CurveIssueCode.InvalidInput, $"{name}: cần R > 0, L ≥ 0 và đỉnh không gập ngược."));
            else if ((input.SpiralIn + input.SpiralOut) / (2 * input.Radius) > delta)
                curve.Issues.Add(new CurveIssue(CurveIssueCode.SpiralTooLong, $"{name}: L quá dài so với góc chuyển hướng."));

            if (curve.Issues.Count > 0)
            {
                // Keep stationing going as if the PI had no curve, so later rows still show numbers.
                curve.StationStart = curve.StationArcStart = curve.StationArcEnd = curve.StationEnd = station + legIn - previousT2;
                station = curve.StationEnd;
                previousT2 = 0;
                continue;
            }

            var e = CurveElementsCalculator.Compute(input.Radius, delta, input.SpiralIn, input.SpiralOut);
            curve.Elements = e;
            curve.TangentBefore = legIn - previousT2 - e.T1;
            if (curve.TangentBefore < -Tolerance)
                curve.Issues.Add(new CurveIssue(CurveIssueCode.Overlap, curve.Number == 1
                    ? $"{name}: tiếp tuyến T1 vượt quá điểm đầu tuyến {NumberFormat.Fixed(-curve.TangentBefore, 2)} m."
                    : $"{name}: chồng lên đường cong trước {NumberFormat.Fixed(-curve.TangentBefore, 2)} m."));

            curve.StationStart = station + curve.TangentBefore;
            curve.StationArcStart = curve.StationStart + input.SpiralIn;
            curve.StationArcEnd = curve.StationArcStart + e.K0;
            curve.StationEnd = curve.StationArcEnd + input.SpiralOut;

            if (rules != null)
            {
                var check = CurveRuleChecker.Check(curve.ToGroup(), designSpeed, rules);
                curve.Issues.AddRange(check.Issues);
                curve.SuggestedWidening = check.Widening;
            }

            station = curve.StationEnd;
            previousT2 = e.T2;
        }

        var tail = Distance(pis[pis.Count - 2], pis[pis.Count - 1]) - previousT2;
        if (tail < -Tolerance && design.Curves.Count > 0)
        {
            var last = design.Curves[design.Curves.Count - 1];
            last.Issues.Add(new CurveIssue(CurveIssueCode.Overlap,
                $"Đ{last.Number}: tiếp tuyến T2 vượt quá điểm cuối tuyến {NumberFormat.Fixed(-tail, 2)} m."));
        }

        design.EndStation = station + tail;
        return design;
    }

    private static void Deflection(PlanPoint a, PlanPoint b, PlanPoint c, out double delta, out int turn)
    {
        double x1 = b.X - a.X, y1 = b.Y - a.Y, x2 = c.X - b.X, y2 = c.Y - b.Y;
        var cross = x1 * y2 - y1 * x2;
        var dot = x1 * x2 + y1 * y2;
        delta = Math.Abs(Math.Atan2(cross, dot));
        turn = cross > 0 ? -1 : 1;   // left turn = -1, matching AlignmentSegment.Turn
    }

    private static double Distance(PlanPoint a, PlanPoint b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
```

**Step 4:** Run the tests; all should pass. Also rerun `CurveRuleCheckerTests`. **Step 5:** Commit with `feat(core): route designer from PIs with stations and issues`.

---

### Task 6: Grid session: the dialog's brain, without WPF

`System.ComponentModel.INotifyPropertyChanged` exists in netstandard2.0. So the whole grid behaviour (defaults, copy down, recalculation, error flags) can live in Core and be unit-tested. The WPF window in Task 9 is then just bindings.

**Files:**
- Create: `src/C3DTools.Core/Curves/CurveDesignSession.cs` (`CurveDesignSession`, `CurveRow`)
- Create: `src/C3DTools.Core/Curves/NumberInput.cs`
- Test: `tests/C3DTools.Core.Tests/Curves/CurveDesignSessionTests.cs`, `NumberInputTests.cs`

**Behaviour (write these tests first):**

| Test | Expectation |
| --- | --- |
| `Load(pis, existingInputs: null)` on 5 PIs | 3 rows, each filled by `DefaultFor` (below). Write these default tests *after* the domain owner fills in `DefaultFor` |
| `Load(pis, existingInputs)` from an alignment | rows keep the alignment's R/L; no defaults applied |
| Setting `rows[1].Radius = 400` | raises `PropertyChanged` for that row's `T1Text`, `KText`, `StationsText`, `IssueText`, the session's `CanApply` and the rows *after* it (their stations move) |
| `CopyDown(rowIndex)` | R, L1, L2, Wb and Wl copied to every later row |
| `SuggestAll()` | R = NormalRadius, L = MinSpiral(V, R), Wb = SuggestedWidening ?? current |
| Changing `DesignSpeed` | recalculates the issues; does *not* change typed R values |
| A collinear PI | no row (only the PIs that `RouteDesigner` numbers) |
| `NumberInput.TryParse("12,5")` and `("12.5")` | both 12.5, whatever the current culture. Vietnamese users type commas; WPF bindings would otherwise use en-US and reject them |
| `NumberInput.TryParse("1.234,5")`, `("abc")`, `("")` | false |

`CurveRow` exposes: `Name` (Đn), `AText` (DMS, read-only), editable strings `RadiusText`, `SpiralInText`, `SpiralOutText`, `WbText`, `WlText` (parsed with `NumberInput`; an invalid string keeps the old value and sets `HasInputError`), and read-only `T1Text`, `T2Text`, `PText`, `KText`, `StationsText` (`TĐ 0+131.09 · P … · TC …`), `IssueText` and `Severity` (None/Warning/Error).

**Default policy (the domain owner writes this method).** Leave this stub in `CurveDesignSession`, and have the engineer who uses the tool fill in its 5–10 lines:

```csharp
/// <summary>
/// Values for a newly loaded row that has no existing curve.
/// previous: the row above (null for the first row); suggestion: TCVN values for V and this PI.
/// </summary>
private static CurveInput DefaultFor(CurveInput previous, CurveInput suggestion, double deltaRadians)
{
    // TODO(domain owner): YTC.lsp copies the previous PI; TCVN suggests Rmin thông thường.
    // Consider: small α with a large R gives a very long T; should L default to 0 when α is small?
    throw new NotImplementedException();
}
```

Commit with `feat(core): curve design session for the CTYTC dialog`.

---

### Task 7: Box text, box placement, table, station format

**Files:**
- Create: `src/C3DTools.Core/Curves/CurveBoxText.cs`, `CurveBoxPlacement.cs`, `CurveTableBuilder.cs`, `TextAngle.cs`
- Modify: `src/C3DTools.Core/Stations/StationFormatter.cs` (`Format(double, int, bool withKmPrefix = true)`; decision D8)
- Tests: `tests/C3DTools.Core.Tests/Curves/CurveBoxTextTests.cs`, `CurveBoxPlacementTests.cs`, `CurveTableBuilderTests.cs`, and one case added to `StationFormatterTests`

**Behaviour (write the tests first):**

`CurveBoxText.Build(DesignedCurve c, CurveBoxOptions o)` gives the LISP layout (D4, D7):

| Case | Lines |
| --- | --- |
| R200, α60°, L50/50, Wb 0.6, Wl 0 | `A=60°00'00"  P=31.54` · `R=200  K=259.44` · `T1=140.76  T2=140.76` · `L1=50  L2=50` · `Wb=0.6  Wl=0` |
| vi-VN culture | the same, with dots |
| `NumberFormat.Trimmed(90.0, 2)` | `90`; `(0.60, 2)` → `0.6`; `(12.345, 2)` → `12.35` (new helper next to `Fixed`) |

`CurveBoxPlacement.Place(PlanPoint pi, PlanPoint before, PlanPoint after, PlanPoint curveMid, double boxWidth, double boxHeight, double textHeight)` ports the second `ytc:bang` and returns `Centre`, `Rotation`, `LeaderStart` (= curveMid) and `LeaderEnd`. The outside direction is `out = -unit(unit(v2) - unit(v1))`; the centre is `pi + out · (4h + bh/2)`; the rotation is `angle(out) − π/2`, then passed through `TextAngle.Readable` so the text is never upside down; the leader ends at `pi + out · 4h` (the near edge of the box). Tests: for the square route (left turn at (100,0)), the centre is at x > 100 and y < 0; the rotation is −π/4 (the bisector is at 135°, so −45° after flipping); `LeaderEnd` lies on the segment from the PI to the centre at distance 4h; a PI whose bisector points down gives a rotation in (−π/2, π/2].

`CurveGeometryBuilder` (Core, tested) ports the drawing geometry of `c:YTC`: `ArcCentre`, `ArcStartEnd`, and `ClothoidPoints(L, R, 21 points)` from `ytc:clo` (series to l¹¹, exactly as the LISP). Tests: the 21st clothoid point of L50/R200 sits within 1 mm of the arc start computed by Task 2; the arc centre for the square route is at distance R from the arc start and end.

`WideningEstimator` (Core, tested) ports `ytca:nominal` and the W step: `Nominal(IReadOnlyList<double> offsets)` returns the smallest absolute offset; `Widening(double offsetAtMid, double nominal)` returns `max(0, |offsetAtMid| − nominal)` and treats anything below 0.005 m as 0. Tests: offsets `{3.5, 3.5, 4.1, 3.5}` give nominal 3.5 and W 0.6 at the widened sample; W below 5 mm is 0.

`CurveTableBuilder.Build(RouteDesign d, CurveBoxOptions o)` returns a `TableData` with the headers `Đỉnh, A, R, L1, L2, T1, T2, P, K, Wb, Wl, Lý trình NĐ, Lý trình TĐ, Lý trình P, Lý trình TC, Lý trình NC`. It always uses 2 decimals. For curves without spirals, NĐ/NC are empty. Test: 2 curves give 2 rows, and a curve that has an error still gives a row with empty elements.

`TextAngle.Readable(double radians)` returns an angle in (−π/2, π/2], for the stake text. Test: 0 → 0, 3π/4 → −π/4, −3π/4 → π/4, π/2 → π/2.

```csharp
public static double Readable(double radians)
{
    var a = Math.IEEERemainder(radians, 2 * Math.PI);   // (-π, π]
    if (a > Math.PI / 2 + 1e-9) a -= Math.PI;
    else if (a <= -Math.PI / 2 + 1e-9) a += Math.PI;
    return a;
}
```

Commit with `feat(core): curve box text, placement and summary table`.

---

### Task 8: Host: read an existing alignment (for reopening the dialog)

**Files:**
- Create: `src/C3DTools.Civil2021/Curves/AlignmentGeometryReader.cs`
- Create: `src/C3DTools.Core/Curves/PiExtractor.cs` + tests

`AlignmentGeometryReader.Read` flattens sub-entities into `AlignmentSegment`s (draft below; confirm member names in S1). `CurveGrouper` (Task 1) turns them into existing R/L1/L2 values, which prefill the grid.

The PIs for an existing alignment come from its tangents. `PiExtractor.FromTangents(IReadOnlyList<(PlanPoint start, PlanPoint end)> lines)` returns the first line's start, the intersection of each consecutive pair of lines, and the last line's end. It is Core and tested: two perpendicular lines give their intersection; parallel lines throw with a Vietnamese message. The host also reads each line's start and end points.

**Wb/Wl for an alignment** come from three sources, in this order: the existing boxes' XData (Task 10 stores them), then **Đọc Wb/Wl từ Offset Alignment…** (below), then zero. `OffsetWideningReader.Read(centreline, offsetAlignments)` ports `YTCA`: for each offset alignment, decide its side (left/right) at mid-route, sample the centreline offset at 60 stations (S13 route: `OffsetAlignmentInfo` if it gives the nominal offset and widening regions, otherwise `StationOffset` on sampled points), take `WideningEstimator.Nominal`, then for each curve compute `WideningEstimator.Widening(offset at StationArcMid, nominal)`. The inside of the curve (`Turn` matches the side) is Wb, the outside is Wl; with several offset alignments per side, the largest value wins, as in the LISP.

**Measured elements** (S14, `YTCA` method): with the tangent directions at `StationStart` and `StationEnd`, `PiExtractor.Intersect` gives the PI; `T1 = |PI − NĐ|`, `T2 = |PI − NC|`, `P = |PI − P point|`. In *Chỉ cắm cọc + khung* mode these measured values are what the box shows, so a non-clothoid or oddly built alignment still gets a truthful box. Core's formula values are printed alongside as a check when they differ by more than 0.01 m.

```csharp
internal static class AlignmentGeometryReader
{
    public static List<AlignmentSegment> Read(Alignment alignment, List<string> warnings)
    {
        var list = new List<AlignmentSegment>();
        for (var i = 0; i < alignment.Entities.Count; i++)
        {
            var entity = alignment.Entities.GetEntityByOrder(i);
            for (var j = 0; j < entity.SubEntityCount; j++)
            {
                switch (entity[j])
                {
                    case AlignmentSubEntityLine line:
                        list.Add(new AlignmentSegment(SegmentKind.Line, line.StartStation, line.Length, 0, 0));
                        break;
                    case AlignmentSubEntityArc arc:
                        list.Add(new AlignmentSegment(SegmentKind.Arc, arc.StartStation, arc.Length,
                            arc.Radius, arc.Clockwise ? 1 : -1));
                        break;
                    case AlignmentSubEntitySpiral spiral:
                        if (spiral.SpiralDefinition != SpiralType.Clothoid)
                            warnings.Add("Tuyến có đường cong chuyển tiếp không phải Clothoid; T, P có thể sai lệch.");
                        var r = IsFinite(spiral.RadiusIn) && spiral.RadiusIn > 0 ? spiral.RadiusIn : spiral.RadiusOut;
                        list.Add(new AlignmentSegment(SegmentKind.Spiral, spiral.StartStation, spiral.Length,
                            r, spiral.Direction == SpiralDirectionType.DirectionRight ? 1 : -1));
                        break;
                }
            }
        }

        list.Sort((a, b) => a.StartStation.CompareTo(b.StartStation));
        return list;
    }

    private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
}
```

`dotnet build src/C3DTools.Civil2021/C3DTools.Civil2021.csproj -c Release` must succeed on macOS. Commit with `feat(host): read alignment curves and PIs`.

---

### Task 9: Host: the `CTYTC` dialog (WPF)

**Files:**
- Modify: `src/C3DTools.Civil2021/C3DTools.Civil2021.csproj`: add `<UseWPF>true</UseWPF>` and `<EnableWindowsTargeting>true</EnableWindowsTargeting>` (S12 route). If S12 failed, instead reference `PresentationFramework`, `PresentationCore`, `WindowsBase` and `System.Xaml` and build the window in C# with no `.xaml` file.
- Create: `src/C3DTools.Civil2021/Ui/CurveDesignWindow.xaml` + `.xaml.cs`
- Create: `src/C3DTools.Civil2021/Commands/CurveDesignCommand.cs`
- Create: `src/C3DTools.Civil2021/Curves/PresetLocator.cs`

**Step 1:** Add the csproj flags, then run `dotnet build` on macOS and push, so CI builds it too. Both must be green before any UI work.

**Window (`CurveDesignWindow.xaml`)**, bound to a `CurveDesignSession`:
- Top row: source label + `Chọn trên bản vẽ…` button; `Lý trình đầu` TextBox (read-only for an alignment, where it comes from `StartingStation`); `V` ComboBox (speeds from the preset) and the Rmin line; for an alignment the **Chế độ** radio buttons (*Thiết kế lại cong* / *Chỉ cắm cọc + khung*, default the latter).
- In *Chỉ cắm cọc + khung* mode the R, L1 and L2 columns are read-only (grey), the `Tạo/cập nhật tuyến` checkbox is off and disabled, and a button **Đọc Wb/Wl từ Offset Alignment…** appears. It closes the dialog, prompts `Chọn các Offset Alignment (mép trái/phải) có Widening, Enter nếu không có:` with a filter on alignments, runs `OffsetWideningReader`, fills Wb/Wl in the grid and prints the nominal width per side (`Mép trái: bề rộng danh nghĩa = 3.5 m`) as the LISP does.
- `DataGrid` with `AutoGenerateColumns="False"`, one row per `CurveRow`. Editable columns: R, L1, L2, Wb, Wl (`UpdateSourceTrigger=PropertyChanged`, so it recalculates while typing). Read-only columns: Đỉnh, A, T1, T2, P, K, Cảnh báo. A `DataTrigger` on `Severity` colours the row: pale yellow for a warning, pale red for an error. The `IssueText` tooltip lists every message.
- Under the grid: the selected row's `StationsText`, plus `Phóng tới đỉnh`.
- Buttons: `Gợi ý R, L theo TCVN` (`SuggestAll`), `Áp dòng này cho các dòng dưới` (`CopyDown`). Checkboxes for the outputs. `Xem trước`, `Áp dụng` (bound to `IsEnabled="{Binding CanApply}"`) and `Hủy`.
- Keyboard: Enter moves down a row, Tab moves to the next editable cell, Esc means Hủy. Set the font to Segoe UI so Vietnamese renders correctly.

**Command loop (`CTYTC`).** The dialog is modal and closes whenever it needs the drawing (S11), then reopens with the same session:

```csharp
[CommandMethod("C3DTOOLS", "CTYTC", CommandFlags.Modal | CommandFlags.UsePickSet)]
public void CurveDesign()
{
    var doc = AcApp.DocumentManager.MdiActiveDocument;
    var preset = PresetLocator.LoadForDrawing(doc.Database);
    var session = new CurveDesignSession(preset);
    RouteSource source = RouteSource.PickFirst(doc.Editor) ?? RouteSource.Prompt(doc.Editor);  // polyline or alignment
    if (source == null) return;
    source.LoadInto(session);

    while (true)
    {
        var window = new CurveDesignWindow(session);
        AcApp.ShowModalWindow(window);
        switch (window.Action)
        {
            case DialogAction.Pick:
                var picked = RouteSource.Prompt(doc.Editor);
                if (picked != null) { source = picked; source.LoadInto(session); }
                continue;
            case DialogAction.ZoomToPi:
                RouteSource.ZoomAndMark(doc.Editor, session.SelectedPi);
                continue;
            case DialogAction.Preview:
                if (RouteWriter.Write(doc, source, session, askToKeep: true)) return;
                continue;   // Không: rolled back, back to the dialog
            case DialogAction.Apply:
                RouteWriter.Write(doc, source, session, askToKeep: false);
                return;
            default:
                return;
        }
    }
}
```

`RouteSource` wraps the selection. For a `Polyline`, it reads the vertices (with `RemoveDuplicatePoints`) and warns when there are bulges, as the LISP does. For an `Alignment`, it uses Task 8. `ZoomAndMark` zooms to the PI and draws a temporary X (the LISP's `grvecs` marker), using `TransientManager`.

**PresetLocator (minimal, until Plan 2):** load `<drawing folder>/*.c3dtools.json` if there is exactly one; otherwise load `Contents/Resources/tcvn4054.preset.json`; otherwise use `new ProjectPreset()`, in which case the dialog shows "Chưa có bảng TCVN – không kiểm tra R, L".

**Manual check (Windows):** open the dialog on a 7-PI polyline. Type in the grid in any order, type `12,5` in R, set R so that two curves overlap and check that Áp dụng turns grey, use Chọn lại and Phóng tới đỉnh, and check the layout at 150 % DPI.

Commit with `feat(host): CTYTC curve design dialog`.

---

### Task 10: Host: `RouteWriter` (Áp dụng / Xem trước)

**Files:**
- Create: `src/C3DTools.Civil2021/Curves/RouteWriter.cs`
- Create: `src/C3DTools.Civil2021/Curves/CurveBoxWriter.cs`, `StakeWriter.cs`
- Modify: `bundle/PackageContents.xml` (add `CTYTC`); `scripts/package-bundle.ps1` (copy `bundle/Resources/*` to `Contents/Resources`)

`Write(doc, source, session, askToKeep)` does the following in **one transaction** under `doc.LockDocument()`:

1. **Styles:** if `YTC_TCVN` / `TCVN_Tuyen` are missing, run `TcvnStyleImporter` (Task 11) in the same transaction.
2. **Curve geometry.** Default (*Vẽ đường cong*): erase tagged objects from the previous run, then per curve an `Arc` plus two clothoid `Polyline`s from `CurveGeometryBuilder` on layer `YTC_CONG`, as the LISP does. Optional (*Tạo Alignment Civil 3D*, and skipped entirely in *Chỉ cắm cọc + khung* mode):
   - *From a polyline:* `Alignment.Create(civDoc, new PolylineOptions { PlineId = id, AddCurvesBetweenTangents = false, EraseExistingEntities = false }, name, siteId: ObjectId.Null, layerId, styleId, labelSetId)`. This is the create path verified in spike #3. Set the start station (S10), then record the tangent entity IDs in order.
   - *From an alignment:* use the S9 route (edit in place, or remove the curve entities and re-add them).
   - For each `DesignedCurve` (with no error), add the curve between tangent `PiIndex-1` and tangent `PiIndex`: `AddFreeSCS(…, L1, L2, SpiralParamType.Length, R, …, SpiralType.Clothoid)` when L > 0, otherwise `AddFreeCurve(…, R, …)` (S8 signatures).
3. **Self-check:** read the alignment back (Task 8), then `CurveGrouper`, and compare R, L1, L2 and the TĐ/TC stations with the session. A difference over 0.01 m prints `"Cảnh báo: Đn lệch …"` but doesn't block. This is how Civil 3D's geometry keeps checking our Core math on every run.
4. **Boxes:** erase the old boxes that carry `C3DTOOLS_YTC` XData for this alignment handle. Then, per curve: an MText with the `CurveBoxText` lines (`\P` joined), `Attachment = MiddleCenter`, `Rotation` and `Location` from `CurveBoxPlacement.Place` using `ActualWidth/ActualHeight` (the curve midpoint comes from `alignment.PointLocation(StationArcMid, 0)`), a frame (S6 route), and a `Line` from `LeaderStart` to `LeaderEnd`, all on layer `YTC_BANG` and all carrying the XData below so a rerun erases them together. The XData is regapp `C3DTOOLS_YTC` with (1005 alignment handle, 1070 number, 1040 Wb, 1040 Wl), which lets reopening the dialog restore Wb/Wl.
5. **Stakes** (checkbox, decision D9): numbered TĐ1/P1/TC1/NĐ1/NC1 plus station, drawn like `ytc:coc`. There is a tick perpendicular to the alignment at `alignment.PointLocation(station, 0)`, and the text is rotated with `TextAngle.Readable` (Task 7), on layer `YTC_COC`, with XData tags. The start and end stakes carry only the station.
6. **Table/CSV** (checkbox): as in Task 12.
7. **Preview:** `QueueForGraphicsFlush()`, `UpdateScreen()`, then with `askToKeep` the prompt `"\nGiữ kết quả? [Co/Khong] <Co>: "`. Commit or abort, so the result is one undo.

Also print the summary `"Hoàn thành: n đường cong, chiều dài tuyến = … m."` (the same wording as the LISP's final message).

**Manual check:** on the S1 polyline, check that the boxes and stakes match the dialog numbers, that `U` removes everything in one step, and that `CTYTC` on the new alignment reloads the same values (Wb/Wl included). Change R2, then Áp dụng: the curves, boxes and stakes should update with no duplicates.

Commit with `feat(host): apply curve design to alignment, boxes and stakes`.

---

### Task 11: Host: `CTYTCMAU` (native styles, label set, abbreviations)

**Files:**
- Create: `bundle/Resources/C3DTools-TCVN.dwg`, hand-authored in Civil 3D 2021 by following the manual workflow: curve label style `YTC_TCVN` (DMS, 0.01, rectangular border, plan readable with bias 110°), a spiral label style for L, label set `TCVN_Tuyen` (Major Geometry Points and Curves with `YTC_TCVN`), and a curve table style.
- Modify: `.gitattributes`, adding `*.dwg binary`.
- Create: `src/C3DTools.Civil2021/Curves/TcvnStyleImporter.cs` (also used by `RouteWriter`)
- Create: `src/C3DTools.Civil2021/Commands/CurveStylesCommand.cs`
- Modify: `bundle/PackageContents.xml` (add `CTYTCMAU`)

**Flow (S4 route):**

1. Open the template with `new Database(false, true)` and `ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, true, null)`.
2. List the styles that already exist in the target. Preview message: `"Sẽ thêm: …; sẽ ghi đè: …"`, then the keyword prompt `[Them moi/Ghi de/Huy]`.
3. In one transaction: `style.ExportTo(db, resolver)` for each style, then set the abbreviations with the S3 API (PC→TĐ, PT→TC, TS→NĐ, ST→NC, SC→TĐ, CS→TC, PI→Đ, MP→P). If S3 found no API, print the manual steps from the workflow instead.
4. Commit, so it is one undo.

If S4 fails, use S5: build the styles in code with the field codes read back from the template.

Commit with `feat(host): CTYTCMAU imports TCVN curve styles and abbreviations`.

---

### Task 12: Host: `CTYTCBANG` (curve summary table)

**Files:**
- Create: `src/C3DTools.Civil2021/Curves/CurveTableWriter.cs` (also used by `RouteWriter`)
- Create: `src/C3DTools.Civil2021/Commands/CurveTableCommand.cs`
- Modify: `bundle/PackageContents.xml` (add `CTYTCBANG`)

Select an alignment and load it as in Task 8, then run `RouteDesigner` and `CurveTableBuilder.Build`. Pick the insertion point and create the AutoCAD `Table` directly (spike #7). Write the CSV next to the DWG as `<tên bản vẽ>_YEUTOCONG.csv` (the LISP's name) with `CsvTableWriter`, which gives UTF-8 with a BOM, so Excel shows Vietnamese correctly. Use preview/commit and one undo.

Commit with `feat(host): CTYTCBANG curve summary table and CSV`.

---

### Task 13: Host: ribbon button

**Files:**
- Create: `src/C3DTools.Civil2021/Ui/RibbonSetup.cs` (`IExtensionApplication`)

On `Initialize`, if `ComponentManager.Ribbon` is null, wait for `ComponentManager.ItemInitialized`. Then add the tab **C3DTools**, panel **Tuyến**, with the large button **Yếu tố cong** (`CommandParameter = "_CTYTC "`) and small buttons **Mẫu TCVN** (`_CTYTCMAU `) and **Bảng cong** (`_CTYTCBANG `). The `ICommand` handler calls `doc.SendStringToExecute(param, true, false, true)`. Use 16/32 px PNG icons embedded as resources. AdWindows comes from the `AutoCAD.NET` package, so no new reference is needed.

Manual check: the tab appears after a Civil 3D restart and after `NETLOAD`, and each button runs its command. Commit with `feat(host): C3DTools ribbon tab with curve tools`.

---

### Task 14 (Windows): Acceptance, LISP comparison, tester docs

1. **Against YTC.lsp:** on the same symmetric polyline with the same R, L and start station, compare our T, P, K and the NĐ/TĐ/P/TC/NC stations with the LISP's CSV. The limit is 0.01 m. The only expected difference is the LISP's truncated p series, which is under 1 mm for L/R < 0.5.
2. **Against YTCA:** on an alignment with two offset alignments that have widening regions, run `YTCA` and then `CTYTC` in *Chỉ cắm cọc + khung* mode. Wb/Wl, the measured T1/T2/P and the stake stations must match to 0.01 m, and the boxes must sit at the same place with the same rotation.
3. On a real project alignment with at least five curves, including some with L1 ≠ L2, compare with a hand calculation or an approved drawing.
4. Vietnamese regional settings: commas are accepted in the grid, and all output uses dots.
5. `docs/testing.md`: add a Vietnamese section **"Thiết kế yếu tố cong (CTYTC)"** with screenshots of the dialog, and a note that the dialog replaces `YTC.lsp`.
6. `README.md`: update the status and command list. `THIRD_PARTY.md`: nothing to add (no copied code; the LISP is the user's own).
7. Set `Version` to `0.2.0`, then tag `v0.2.0` so CI publishes the release.

Commit with `docs: CTYTC for testers`.

---

## Done criteria for this plan

- [ ] Spike 2 section recorded (S1–S14), with its route decisions
- [ ] Golden CSVs from the LISP saved and used by Core tests
- [ ] D2, D5, D7, D8 and D9 answered; `DefaultFor` written by the domain owner
- [ ] `tcvn4054.preset.json` seeded and checked by a second engineer
- [ ] Core tests pass on macOS and CI, including the vi-VN culture tests
- [ ] `CTYTC` dialog: live recalculation, errors block Áp dụng, reopening an alignment restores every value
- [ ] `CTYTC`, `CTYTCMAU` and `CTYTCBANG` each have preview, cancel and a single undo
- [ ] Symmetric-curve results match `YTC.lsp` to 0.01 m; the alignment mode matches `YTCA` (Wb/Wl, measured T/P, stakes)
- [ ] The bundle zip contains `Resources/` and no Autodesk DLLs
- [ ] PRD updated (C-09, C-10), and `docs/testing.md` has the Vietnamese steps
