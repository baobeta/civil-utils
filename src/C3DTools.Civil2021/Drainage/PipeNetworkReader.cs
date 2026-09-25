using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using C3DTools.Core.Drainage;
using C3DTools.Core.Presets;
using CivilSurface = Autodesk.Civil.DatabaseServices.Surface;

namespace C3DTools.Civil2021.Drainage;

/// <summary>
/// CTBANGCONG: one CulvertRecord per pipe of the chosen networks, located on the alignment by its plan midpoint.
/// Members used (verified by reflection over Civil3D2021.Base): Network.GetPipeIds, Pipe.StartPoint/EndPoint,
/// Length2DCenterToCenter, InnerDiameterOrWidth, OuterDiameterOrWidth, InnerHeight, CrossSectionalShape, FlowDirection,
/// StartStructureId/EndStructureId, Part.PartDescription/PartSizeName/PartSubType, Structure.RimElevation,
/// Alignment.StationOffset/PointLocation, Surface.FindElevationAtXY. Whether endpoint Z is the invert or the
/// centreline is not verified on 2021 (spike #6): preset Culvert.EndpointIsCentreline decides.
/// </summary>
internal static class PipeNetworkReader
{
    /// <summary>m either side of the station for the alignment direction at the crossing.</summary>
    private const double TangentStep = 0.5;

    private const int MaxMessages = 5;

    /// <param name="surface">Ground at pipe ends without a structure; null for none.</param>
    /// <param name="messages">Why pipes were skipped or a value is missing (at most a few of each).</param>
    public static List<CulvertRecord> Read(Transaction tr, Alignment alignment, IEnumerable<ObjectId> networkIds,
        CivilSurface surface, CulvertOptions options, List<string> messages)
    {
        var result = new List<CulvertRecord>();
        var outside = 0;
        var failed = 0;
        foreach (var networkId in networkIds)
        {
            Network network;
            ObjectIdCollection pipeIds;
            try
            {
                network = (Network)tr.GetObject(networkId, OpenMode.ForRead);
                pipeIds = network.GetPipeIds();
            }
            catch (Exception ex)
            {
                messages.Add($"Không đọc được mạng cống ({ex.Message}); bỏ qua.");
                continue;
            }

            foreach (ObjectId pipeId in pipeIds)
            {
                if (!(tr.GetObject(pipeId, OpenMode.ForRead) is Pipe pipe)) continue;
                string pipeName = Try(() => pipe.Name, pipe.Handle.ToString());
                try
                {
                    var record = ReadPipe(tr, alignment, network.Name, pipe, surface, options);
                    if (record == null)
                    {
                        if (outside++ < MaxMessages) messages.Add($"Ống {pipeName} ({network.Name}) nằm ngoài phạm vi tuyến; bỏ qua.");
                        continue;
                    }

                    result.Add(record);
                }
                catch (Exception ex)
                {
                    if (failed++ < MaxMessages) messages.Add($"Không đọc được ống {pipeName} ({ex.Message}); bỏ qua.");
                }
            }
        }

        if (outside > MaxMessages) messages.Add($"… tổng cộng {outside} ống ngoài phạm vi tuyến.");
        if (failed > MaxMessages) messages.Add($"… tổng cộng {failed} ống không đọc được.");
        return result;
    }

    /// <summary>Null when the pipe midpoint does not project onto the alignment.</summary>
    private static CulvertRecord ReadPipe(Transaction tr, Alignment alignment, string networkName, Pipe pipe,
        CivilSurface surface, CulvertOptions options)
    {
        var start = pipe.StartPoint;
        var end = pipe.EndPoint;
        var upstreamStructure = pipe.StartStructureId;
        var downstreamStructure = pipe.EndStructureId;
        if (Try(() => pipe.FlowDirection, FlowDirectionType.StartToEnd) == FlowDirectionType.EndToStart)
        {
            (start, end) = (end, start);
            (upstreamStructure, downstreamStructure) = (downstreamStructure, upstreamStructure);
        }

        double station = 0, offset = 0;
        try
        {
            alignment.StationOffset((start.X + end.X) / 2, (start.Y + end.Y) / 2, ref station, ref offset);
        }
        catch (Exception)
        {
            return null;   // beyond the alignment's ends
        }

        if (station < alignment.StartingStation - 1e-6 || station > alignment.EndingStation + 1e-6) return null;

        var width = pipe.InnerDiameterOrWidth;
        var height = Try(() => pipe.InnerHeight, width);
        if (!(height > 0)) height = width;
        var outer = Try(() => pipe.OuterDiameterOrWidth, width);
        var wall = outer > width ? (outer - width) / 2 : 0;   // 2021 Pipe has no WallThickness of its own (spike #6)

        var shape = Try(() => pipe.CrossSectionalShape.ToString(), "");
        var description = Try(() => pipe.PartDescription, "") + " " + Try(() => pipe.PartSizeName, "") + " " + Try(() => pipe.PartSubType, "");
        var kind = Culverts.Kind(shape, description);

        return new CulvertRecord
        {
            Key = pipe.Handle.ToString(),
            PipeName = Try(() => pipe.Name, ""),
            NetworkName = networkName ?? "",
            Station = station,
            Offset = offset,
            SkewDeg = Skew(alignment, station, end.X - start.X, end.Y - start.Y),
            Kind = kind,
            SizeText = Culverts.SizeText(kind, width, height),
            Length = pipe.Length2DCenterToCenter,   // Length2D is obsolete in 2021 and points here; matches the endpoint inverts used for the slope
            InnerHeight = height,
            WallThickness = wall,
            InvertUpstream = Culverts.Invert(start.Z, height, options.EndpointIsCentreline),
            InvertDownstream = Culverts.Invert(end.Z, height, options.EndpointIsCentreline),
            GroundUpstream = Ground(tr, upstreamStructure, surface, start),
            GroundDownstream = Ground(tr, downstreamStructure, surface, end),
        };
    }

    /// <summary>Deviation from a square crossing, from the alignment direction between station ± TangentStep. NaN when unknown.</summary>
    private static double Skew(Alignment alignment, double station, double dx, double dy)
    {
        try
        {
            var from = Math.Max(alignment.StartingStation, station - TangentStep);
            var to = Math.Min(alignment.EndingStation, station + TangentStep);
            if (!(to > from)) return double.NaN;
            double x1 = 0, y1 = 0, x2 = 0, y2 = 0;
            alignment.PointLocation(from, 0, ref x1, ref y1);
            alignment.PointLocation(to, 0, ref x2, ref y2);
            return Culverts.SkewDegrees(dx, dy, x2 - x1, y2 - y1);
        }
        catch (Exception)
        {
            return double.NaN;
        }
    }

    /// <summary>Rim of the end structure, else the surface at the pipe end; null when neither gives a value.</summary>
    private static double? Ground(Transaction tr, ObjectId structureId, CivilSurface surface, Point3d at)
    {
        if (!structureId.IsNull)
        {
            try
            {
                if (tr.GetObject(structureId, OpenMode.ForRead) is Structure structure && Finite(structure.RimElevation))
                    return structure.RimElevation;
            }
            catch (Exception)
            {
                // Fall back to the surface.
            }
        }

        if (surface == null) return null;
        try
        {
            var z = surface.FindElevationAtXY(at.X, at.Y);
            return Finite(z) ? z : (double?)null;
        }
        catch (Exception)
        {
            return null;   // outside the surface
        }
    }

    private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

    private static T Try<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch (Exception)
        {
            return fallback;
        }
    }
}
