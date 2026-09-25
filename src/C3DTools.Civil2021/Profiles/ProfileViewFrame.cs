using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

namespace C3DTools.Civil2021.Profiles;

/// <summary>
/// Station/elevation → drawing XY in a profile view. First ProfileView.FindXYAtStationAndElevation; when that
/// fails or returns false, the view origin (Location = StationStart, ElevationMin) + (station − StationStart)·1 and
/// (elevation − ElevationMin)·VerticalExaggeration of the view style's GraphStyle (1 when unreadable).
/// </summary>
internal sealed class ProfileViewFrame
{
    private readonly ProfileView _view;
    private bool _findFailed;

    public ProfileViewFrame(Transaction tr, ProfileView view)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        Origin = view.Location;
        StationStart = view.StationStart;
        StationEnd = view.StationEnd;
        ElevationMin = view.ElevationMin;
        ElevationMax = view.ElevationMax;
        VerticalExaggeration = 1;
        try
        {
            if (tr.GetObject(view.StyleId, OpenMode.ForRead) is ProfileViewStyle style && style.GraphStyle.VerticalExaggeration > 0)
                VerticalExaggeration = style.GraphStyle.VerticalExaggeration;
        }
        catch (Exception)
        {
            // Only the fallback needs it; 1 keeps boxes and table on the view, if stretched.
        }
    }

    public Point3d Origin { get; }
    public double StationStart { get; }
    public double StationEnd { get; }
    public double ElevationMin { get; }
    public double ElevationMax { get; }
    public double VerticalExaggeration { get; }

    /// <summary>True once FindXYAtStationAndElevation failed for a point and the origin + scale fallback was used.</summary>
    public bool UsedFallback => _findFailed;

    public Point2d ToXY(double station, double elevation)
    {
        try
        {
            double x = 0, y = 0;
            if (_view.FindXYAtStationAndElevation(station, elevation, ref x, ref y)) return new Point2d(x, y);
        }
        catch (Exception)
        {
            // e.g. station outside the view: fall through to the computed position.
        }

        _findFailed = true;
        return new Point2d(Origin.X + (station - StationStart), Origin.Y + (elevation - ElevationMin) * VerticalExaggeration);
    }
}
