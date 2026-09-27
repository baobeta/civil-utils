using System;
using C3DTools.Core.Curves;

namespace C3DTools.Core.Stations;

/// <summary>What a stake marks: a detail stake (C), a 100 m stake (H), a kilometre (Km) or a main curve point (NĐ, TĐ, P, TC, NC).</summary>
public enum StakeRole { Detail, Hundred, Km, CurveKey }

/// <summary>One stake (cọc) of a route, as CTPHATCOC plans it and CTDANHCOC renames it.</summary>
public sealed class RouteStake
{
    public RouteStake(double station, StakeRole role, string name = "", StakeKind curveKind = StakeKind.Start, int curveNumber = 0)
    {
        if (role == StakeRole.CurveKey && (curveKind == StakeKind.Start || curveKind == StakeKind.End || curveNumber <= 0))
            throw new ArgumentException("Cọc chủ yếu cần loại (NĐ, TĐ, P, TC, NC) và số đỉnh.", nameof(curveKind));
        Station = station;
        Role = role;
        Name = name ?? "";
        CurveKind = curveKind;
        CurveNumber = curveNumber;
    }

    public double Station { get; }
    public StakeRole Role { get; }

    /// <summary>The name the stake has now; empty for a new stake.</summary>
    public string Name { get; }

    /// <summary>Nd, Td, P, Tc or Nc for a CurveKey stake.</summary>
    public StakeKind CurveKind { get; }

    /// <summary>Đ number of the curve (1-based) for a CurveKey stake.</summary>
    public int CurveNumber { get; }

    /// <summary>"NĐ", "TĐ", "P", "TC", "NC"; empty for other roles.</summary>
    public string CurvePrefix => Role != StakeRole.CurveKey ? "" : CurveKind switch
    {
        StakeKind.Nd => "NĐ",
        StakeKind.Td => "TĐ",
        StakeKind.P => "P",
        StakeKind.Tc => "TC",
        StakeKind.Nc => "NC",
        _ => "",
    };

    public RouteStake WithName(string name) => new RouteStake(Station, Role, name, CurveKind, CurveNumber);
}
