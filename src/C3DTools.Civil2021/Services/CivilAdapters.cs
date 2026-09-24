using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Core.Drainage;
using C3DTools.Core.Volumes;
using CivilSurface = Autodesk.Civil.DatabaseServices.Surface;

namespace C3DTools.Civil2021.Services;

internal sealed class VolumeSamplingResult
{
    public VolumeSamplingResult(IReadOnlyList<SectionAreas> sections, IReadOnlyList<double> skippedStations)
    {
        Sections = sections;
        SkippedStations = skippedStations;
    }

    public IReadOnlyList<SectionAreas> Sections { get; }
    public IReadOnlyList<double> SkippedStations { get; }
}

internal static class VolumeSamplingService
{
    public static VolumeSamplingResult Sample(
        Alignment alignment,
        CivilSurface baseSurface,
        CivilSurface designSurface,
        double startStation,
        double endStation,
        double interval,
        double halfWidth,
        double sampleSpacing)
    {
        if (alignment == null) throw new ArgumentNullException(nameof(alignment));
        if (baseSurface == null) throw new ArgumentNullException(nameof(baseSurface));
        if (designSurface == null) throw new ArgumentNullException(nameof(designSurface));
        if (!(endStation > startStation)) throw new ArgumentException("Lý trình cuối phải lớn hơn lý trình đầu.");
        if (!(interval > 0)) throw new ArgumentOutOfRangeException(nameof(interval));
        if (!(halfWidth > 0)) throw new ArgumentOutOfRangeException(nameof(halfWidth));
        if (!(sampleSpacing > 0)) throw new ArgumentOutOfRangeException(nameof(sampleSpacing));

        var width = halfWidth * 2.0;
        var segmentCount = Math.Max(1, (int)Math.Ceiling(width / sampleSpacing));
        if (segmentCount > 200) throw new ArgumentOutOfRangeException(nameof(sampleSpacing), "Quá nhiều điểm cắt ngang.");
        var actualSpacing = width / segmentCount;
        var offsets = Enumerable.Range(0, segmentCount + 1)
            .Select(i => -halfWidth + i * actualSpacing)
            .ToArray();

        var stationCount = (int)Math.Ceiling((endStation - startStation) / interval) + 1;
        if (stationCount > 501) throw new ArgumentOutOfRangeException(nameof(interval), "Quá nhiều cắt ngang.");

        var sections = new List<SectionAreas>();
        var skipped = new List<double>();
        for (var stationIndex = 0; stationIndex < stationCount; stationIndex++)
        {
            var station = Math.Min(endStation, startStation + stationIndex * interval);
            var baseElevations = new List<double>(offsets.Length);
            var designElevations = new List<double>(offsets.Length);
            var valid = true;
            foreach (var offset in offsets)
            {
                double east = 0;
                double north = 0;
                alignment.PointLocation(station, offset, ref east, ref north);
                if (!TryElevation(baseSurface, east, north, out var baseZ) ||
                    !TryElevation(designSurface, east, north, out var designZ))
                {
                    valid = false;
                    break;
                }
                baseElevations.Add(baseZ);
                designElevations.Add(designZ);
            }
            if (!valid)
            {
                skipped.Add(station);
                continue;
            }
            var area = CrossSectionArea.Compute(baseElevations, designElevations, actualSpacing);
            sections.Add(new SectionAreas(station, area.CutArea, area.FillArea));
        }

        if (sections.Count < 2)
            throw new InvalidOperationException("Có ít hơn hai cắt ngang hợp lệ. Kiểm tra phạm vi offset và hai mặt bằng.");
        return new VolumeSamplingResult(sections, skipped);
    }

    private static bool TryElevation(CivilSurface surface, double x, double y, out double elevation)
    {
        try
        {
            elevation = surface.FindElevationAtXY(x, y);
            return !double.IsNaN(elevation) && !double.IsInfinity(elevation);
        }
        catch
        {
            elevation = double.NaN;
            return false;
        }
    }
}

internal static class PipeAdapter
{
    public static PipeData ToData(Transaction transaction, Pipe pipe, bool endpointsAreCenterlines)
    {
        if (pipe == null) throw new ArgumentNullException(nameof(pipe));
        var innerDiameter = pipe.InnerDiameterOrWidth;
        var outerDiameter = pipe.OuterDiameterOrWidth;
        if (!(innerDiameter > 0) || !(outerDiameter > 0))
            throw new InvalidOperationException("CTCONG hiện hỗ trợ Pipe tròn có đường kính trong và ngoài.");
        var wall = Math.Max(0, (outerDiameter - innerDiameter) / 2.0);
        var innerHeight = pipe.InnerHeight > 0 ? pipe.InnerHeight : innerDiameter;
        if (wall <= 0) wall = Math.Max(0, ((pipe.OuterHeight > 0 ? pipe.OuterHeight : outerDiameter) - innerHeight) / 2.0);

        var startInvert = endpointsAreCenterlines ? pipe.StartPoint.Z - innerHeight / 2.0 - wall : pipe.StartPoint.Z;
        var endInvert = endpointsAreCenterlines ? pipe.EndPoint.Z - innerHeight / 2.0 - wall : pipe.EndPoint.Z;
        var startGround = GroundAtStructure(transaction, pipe.StartStructureId) ??
                         startInvert + innerHeight + wall + pipe.CoverOfStartPoint;
        var endGround = GroundAtStructure(transaction, pipe.EndStructureId) ??
                       endInvert + innerHeight + wall + pipe.CoverOfEndpoint;
        if (double.IsNaN(startGround) || double.IsInfinity(startGround) ||
            double.IsNaN(endGround) || double.IsInfinity(endGround))
            throw new InvalidOperationException("Không xác định được cao độ mặt đất tại hai đầu cống.");

        var length = pipe.Length2DCenterToCenter;
        if (double.IsNaN(length) || length <= 0) length = pipe.Length2DToInsideEdge;
        return new PipeData
        {
            Name = pipe.Name,
            Length = length,
            StartInvert = startInvert,
            EndInvert = endInvert,
            InnerDiameter = innerDiameter,
            WallThickness = wall,
            StartGround = startGround,
            EndGround = endGround
        };
    }

    private static double? GroundAtStructure(Transaction transaction, ObjectId id)
    {
        if (id.IsNull || !id.IsValid || id.IsErased) return null;
        var structure = transaction.GetObject(id, OpenMode.ForRead) as Structure;
        return structure?.RimElevation;
    }
}
