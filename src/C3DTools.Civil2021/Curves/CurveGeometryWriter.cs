using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using C3DTools.Core.Curves;
using C3DTools.Core.Tables;
using AcArc = Autodesk.AutoCAD.DatabaseServices.Arc;
using AcPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace C3DTools.Civil2021.Curves;

/// <summary>Curve geometry: plain ARC + clothoid polylines (c:YTC), or a Civil 3D alignment (optional, API risk: S8/S9).</summary>
internal static class CurveGeometryWriter
{
    private const double CheckTolerance = 0.01;

    /// <summary>Per curve an ARC and the two clothoid LWPOLYLINEs on YTC_CONG, as the LISP draws them.</summary>
    public static void WritePlain(RouteDrawing d, IReadOnlyList<PlanPoint> pis, RouteDesign design)
    {
        foreach (var c in design.Curves)
        {
            if (c.Elements == null) continue;
            var input = c.Input;
            var g = CurveGeometryBuilder.Build(pis[c.PiIndex], pis[c.PiIndex - 1], pis[c.PiIndex + 1],
                input.Radius, input.SpiralIn, input.SpiralOut, c.Turn, c.Elements);
            d.Add(new AcArc(new Point3d(g.ArcCentre.X, g.ArcCentre.Y, 0), Vector3d.ZAxis, input.Radius,
                g.ArcDrawStartAngle, g.ArcDrawEndAngle), RouteDrawing.CurveLayer, YtcTag.For(c, YtcKind.Curve));
            if (g.SpiralIn.Count > 1) d.Add(Polyline(g.SpiralIn, false), RouteDrawing.CurveLayer, YtcTag.For(c, YtcKind.Curve));
            if (g.SpiralOut.Count > 1) d.Add(Polyline(g.SpiralOut, false), RouteDrawing.CurveLayer, YtcTag.For(c, YtcKind.Curve));
        }
    }

    /// <summary>
    /// Tangents from the polyline, then one free curve per PI, in a nested transaction: either the whole alignment
    /// is created or nothing is. False (with a message) when it fails, so the caller draws plain geometry.
    /// </summary>
    public static bool CreateAlignment(RouteDrawing d, ObjectId polylineId, CurveDesignSession session, Editor ed)
    {
        const string failed = "Không tạo được Alignment";
        if (!Supported(session, ed, failed)) return false;

        var layerId = d.LayerId(RouteDrawing.CurveLayer);
        ObjectId id;
        using (var nested = d.Database.TransactionManager.StartTransaction())
        {
            try
            {
                var civil = CivilDocument.GetCivilDocument(d.Database);
                var options = new PolylineOptions { PlineId = polylineId, AddCurvesBetweenTangents = false, EraseExistingEntities = false };
                id = Alignment.Create(civil, options, UniqueName(civil, nested), ObjectId.Null, layerId,
                    StyleId(civil.Styles.AlignmentStyles, "TCVN_Tuyen"),
                    StyleId(civil.Styles.LabelSetStyles.AlignmentLabelSetStyles, "YTC_TCVN"));
                var alignment = (Alignment)nested.GetObject(id, OpenMode.ForWrite);
                d.Tag(alignment, new YtcTag { Kind = YtcKind.Alignment });
                try
                {
                    alignment.ReferencePointStation = session.StartStation;
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\nKhông đặt được lý trình đầu {NumberFormat.Trimmed(session.StartStation, 2)} cho Alignment: {ex.Message}");
                }

                AddCurves(alignment, CheckedTangents(alignment, session), session);
                nested.Commit();
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n{failed}: {ex.Message} Đã vẽ hình học thường thay thế.");
                return false;   // nested transaction aborted: no alignment at all
            }
        }

        var created = (Alignment)d.Transaction.GetObject(id, OpenMode.ForRead);
        ed.WriteMessage($"\nĐã tạo Alignment {created.Name}.");
        SelfCheck(created, session.Design, ed);
        return true;
    }

    /// <summary>
    /// "Thiết kế lại cong" on an existing alignment: removes its curves and adds the designed ones between the same
    /// tangents. Everything is checked first and done in a nested transaction, so a failure leaves the alignment as it was.
    /// </summary>
    public static bool UpdateAlignment(RouteDrawing d, ObjectId alignmentId, CurveDesignSession session, Editor ed)
    {
        const string failed = "Không cập nhật được Alignment";
        if (!Supported(session, ed, failed)) return false;
        try
        {
            CheckedTangents((Alignment)d.Transaction.GetObject(alignmentId, OpenMode.ForRead), session);
        }
        catch (Exception ex)
        {
            ed.WriteMessage($"\n{failed}: {ex.Message} Đã vẽ hình học thường thay thế.");
            return false;
        }

        using (var nested = d.Database.TransactionManager.StartTransaction())
        {
            try
            {
                var alignment = (Alignment)nested.GetObject(alignmentId, OpenMode.ForWrite);
                var entities = alignment.Entities;
                var curves = new List<AlignmentEntity>();
                for (var i = 0; i < entities.Count; i++)
                {
                    var e = entities.GetEntityByOrder(i);
                    if (e.EntityType != AlignmentEntityType.Line) curves.Add(e);
                }

                foreach (var e in curves) entities.Remove(e);
                AddCurves(alignment, CheckedTangents(alignment, session), session);
                nested.Commit();
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n{failed}: {ex.Message} Đã vẽ hình học thường thay thế.");
                return false;   // nested transaction aborted: the alignment is unchanged
            }
        }

        SelfCheck((Alignment)d.Transaction.GetObject(alignmentId, OpenMode.ForRead), session.Design, ed);
        return true;
    }

    /// <summary>
    /// Civil3D2021.Base has AddFreeSCS (two spirals) and AddFreeCurve (none) between tangents, but no verified
    /// spiral–curve / curve–spiral variant, so a curve with exactly one spiral cannot go into an alignment.
    /// </summary>
    private static bool Supported(CurveDesignSession session, Editor ed, string failed)
    {
        var oneSided = session.Design.Curves
            .Where(c => c.Elements != null && (c.Input.SpiralIn > 0) != (c.Input.SpiralOut > 0))
            .ToList();
        foreach (var c in oneSided)
            ed.WriteMessage($"\nĐ{c.Number}: chỉ có một đường cong chuyển tiếp (L1 = {NumberFormat.Trimmed(c.Input.SpiralIn, 2)}, " +
                            $"L2 = {NumberFormat.Trimmed(c.Input.SpiralOut, 2)}); Civil 3D 2021 API không thêm được loại cong này vào Alignment.");
        if (oneSided.Count == 0) return true;
        ed.WriteMessage($"\n{failed}. Đã vẽ hình học thường thay thế.");
        return false;
    }

    /// <summary>Tangent entity ids in order; throws when they don't match the PI polygon one to one.</summary>
    private static List<int> CheckedTangents(Alignment alignment, CurveDesignSession session)
    {
        var entities = alignment.Entities;
        var tangents = new List<int>();
        for (var i = 0; i < entities.Count; i++)
        {
            var e = entities.GetEntityByOrder(i);
            if (e.EntityType == AlignmentEntityType.Line) tangents.Add(e.EntityId);
        }

        if (tangents.Count != session.Pis.Count - 1)
            throw new InvalidOperationException(
                $"Alignment có {tangents.Count} đoạn thẳng, cần {session.Pis.Count - 1} (polyline kín, có cung tròn hoặc đỉnh trùng?).");
        return tangents;
    }

    /// <summary>One free curve per designed curve; any failure throws, so the caller aborts the whole alignment.</summary>
    private static void AddCurves(Alignment alignment, List<int> tangents, CurveDesignSession session)
    {
        var entities = alignment.Entities;
        foreach (var c in session.Design.Curves)
        {
            if (c.Elements == null) continue;
            int previous = tangents[c.PiIndex - 1], next = tangents[c.PiIndex];
            var input = c.Input;
            try
            {
                if (input.SpiralIn > 0 && input.SpiralOut > 0)
                    entities.AddFreeSCS(previous, next, input.SpiralIn, input.SpiralOut, SpiralParamType.Length,
                        input.Radius, false, SpiralType.Clothoid);
                else
                    entities.AddFreeCurve(previous, next, input.Radius, CurveParamType.Radius, false, CurveType.Compound);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Đ{c.Number}: không thêm được đường cong ({ex.Message}).", ex);
            }
        }
    }

    /// <summary>Reads the alignment back and compares it with Core's numbers; a difference only warns.</summary>
    private static void SelfCheck(Alignment alignment, RouteDesign design, Editor ed)
    {
        var groups = CurveGrouper.Group(AlignmentGeometryReader.Read(alignment, null)).Groups;
        var curves = design.Curves.Where(c => c.Elements != null).ToList();
        if (groups.Count != curves.Count)
        {
            ed.WriteMessage($"\nCảnh báo: Alignment có {groups.Count} đường cong, thiết kế có {curves.Count}.");
            return;
        }

        for (var k = 0; k < curves.Count; k++)
        {
            var c = curves[k];
            var g = groups[k];
            var diffs = new List<string>();
            void Compare(string name, double actual, double expected)
            {
                if (Math.Abs(actual - expected) > CheckTolerance) diffs.Add($"{name} {NumberFormat.Fixed(actual - expected, 3)} m");
            }

            Compare("R", g.Radius, c.Input.Radius);
            Compare("L1", g.SpiralIn, c.Input.SpiralIn);
            Compare("L2", g.SpiralOut, c.Input.SpiralOut);
            Compare("TĐ", g.ArcStartStation, c.StationArcStart);
            Compare("TC", g.ArcEndStation, c.StationArcEnd);
            if (diffs.Count > 0) ed.WriteMessage($"\nCảnh báo: Đ{c.Number} lệch {string.Join(", ", diffs)}.");
        }
    }

    internal static AcPolyline Polyline(IReadOnlyList<PlanPoint> points, bool closed)
    {
        var pline = new AcPolyline();
        for (var i = 0; i < points.Count; i++) pline.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0, 0, 0);
        pline.Closed = closed;
        return pline;
    }

    private static ObjectId StyleId(StyleCollectionBase styles, string preferred)
    {
        if (styles.Contains(preferred)) return styles[preferred];
        return styles.Count > 0 ? styles[0] : ObjectId.Null;
    }

    private static string UniqueName(CivilDocument civil, Transaction tr)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // openErased: the previous run's alignment was just erased in this transaction but is still listed.
        foreach (ObjectId id in civil.GetAlignmentIds())
            if (tr.GetObject(id, OpenMode.ForRead, true) is Alignment a) names.Add(a.Name);
        for (var i = 1; ; i++)
        {
            var name = "YTC - " + i;
            if (!names.Contains(name)) return name;
        }
    }
}
