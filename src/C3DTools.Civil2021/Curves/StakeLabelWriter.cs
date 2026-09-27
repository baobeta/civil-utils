using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Civil2021.Drawing;
using C3DTools.Core.Curves;
using C3DTools.Core.Presets;
using C3DTools.Core.Stations;

namespace C3DTools.Civil2021.Curves;

/// <summary>
/// "Ghi tên cọc lên bình đồ": the stake name beyond the left end of each sample line and its station beyond the right
/// end, written along the route, on layer COC_TEN. Tagged with the sample line group, so the next run replaces them.
/// </summary>
internal static class StakeLabelWriter
{
    public const string Tool = "COC";
    public const string Layer = "COC_TEN";

    /// <summary>Text height in drawing units: 2.5 mm at the plan scale CTTUYEN stored, else the preset's.</summary>
    public static double TextHeight(Alignment alignment, ProjectPreset preset)
    {
        if (RouteTag.TryRead(alignment, out var scale, out _, out _) && scale > 0)
            return RouteCreationSession.PaperTextHeight * scale / 1000;
        return preset?.CurveBox?.TextHeight > 0 ? preset.CurveBox.TextHeight : 2.5;
    }

    /// <summary>
    /// Replaces the group's labels: every stake of the group, the curve stakes (NĐ, TĐ, P, TC, NC) included.
    /// Returns how many stakes were labelled.
    /// </summary>
    public static int Write(Transaction tr, Database db, Alignment alignment, ObjectId groupId,
        IReadOnlyList<RouteStake> stakes, IReadOnlyList<string> labels, double textHeight, StakeLabelOptions options, Action<string> warn)
    {
        var group = tr.GetObject(groupId, OpenMode.ForRead);
        var d = new TaggedDrawing(tr, db, Tool, group.Handle.ToString());
        d.EnsureLayer(Layer, 2);
        d.EraseTagged(null, null);

        var lines = SampleLineStakes.Read(tr, groupId);
        var count = 0;
        var failed = 0;
        for (var i = 0; i < stakes.Count; i++)
        {
            try
            {
                var station = Math.Max(alignment.StartingStation, Math.Min(alignment.EndingStation, stakes[i].Station));
                var lineId = lines.Where(l => Math.Abs(l.stake.Station - stakes[i].Station) <= StakePlanner.Tolerance).Select(l => l.id).FirstOrDefault();
                if (lineId.IsNull) throw new InvalidOperationException("không tìm thấy trắc ngang của cọc");
                Ends((SampleLine)tr.GetObject(lineId, OpenMode.ForRead), out var a, out var b);
                // count, not i: skipped stakes must not break the left/right alternation.
                var layout = StakeLabelLayout.AtEnds(count, a, b, MeasuredElements.Direction(alignment, station), stakes[i].Station,
                    StakeNamer.DisplayName(labels[i]), textHeight, options, stakes[i].Role);
                if (layout.NameText.Length > 0) AddText(d, layout.NameText, layout.NamePoint, layout.Rotation, textHeight, i + 1);
                if (layout.StationText.Length > 0) AddText(d, layout.StationText, layout.StationPoint, layout.Rotation, textHeight, i + 1);
                count++;
            }
            catch (Exception ex)
            {
                if (failed++ < 3) warn?.Invoke($"Cọc {labels[i]}: không ghi được tên ({ex.Message}); bỏ qua.");
            }
        }

        return count;
    }

    /// <summary>The two ends of the sample line (its first and last vertex).</summary>
    private static void Ends(SampleLine line, out PlanPoint a, out PlanPoint b)
    {
        var vertices = line.Vertices;
        if (vertices.Count < 2) throw new InvalidOperationException("trắc ngang có ít hơn 2 đỉnh");
        var first = vertices[0].Location;
        var last = vertices[vertices.Count - 1].Location;
        a = new PlanPoint(first.X, first.Y);
        b = new PlanPoint(last.X, last.Y);
    }

    /// <summary>Erases the labels of a group (its stakes are being removed or relabelled elsewhere).</summary>
    public static void Erase(Transaction tr, Database db, ObjectId groupId)
    {
        var group = tr.GetObject(groupId, OpenMode.ForRead);
        new TaggedDrawing(tr, db, Tool, group.Handle.ToString()).EraseTagged(null, null);
    }

    /// <summary>TEXT justified middle-centre, as CTYTC's stake texts.</summary>
    private static void AddText(TaggedDrawing d, string value, PlanPoint at, double rotation, double height, int number)
    {
        var p = P3(at);
        var text = new DBText
        {
            TextString = value,
            Height = height,
            Rotation = rotation,
            Position = p,
            Justify = AttachmentPoint.MiddleCenter,
            AlignmentPoint = p,
        };
        d.Add(text, Layer, new ToolTag(Tool) { Kind = 1, Number = number });
        text.TextStyleId = d.Database.Textstyle;
        text.AdjustAlignment(d.Database);
    }

    private static Point3d P3(PlanPoint p) => new Point3d(p.X, p.Y, 0);
}
