using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace C3DTools.Core.Stations;

/// <summary>The "Đánh lại toàn bộ tên cọc" options (AND Design's dialog, same names).</summary>
public sealed class StakeNamingOptions
{
    /// <summary>"Tiếp đầu của cọc": prefix of detail stakes.</summary>
    public string DetailPrefix { get; set; } = "C";

    /// <summary>"Số thứ tự cọc đầu": number of the first detail stake in the range.</summary>
    public int FirstDetailNumber { get; set; } = 1;

    /// <summary>
    /// "Thứ tự cọc quay lại theo KM": detail numbers start again at 1 after each Km stake. Off by default: C stakes are
    /// numbered continuously to the end of the route (H stakes always restart after a Km: H1…H9, Km1, H1…).
    /// </summary>
    public bool RestartPerKm { get; set; }

    /// <summary>
    /// "Không đánh số quay lại khi TT&gt;=100": a Km stake does not restart the numbering once the last detail
    /// number has reached 100. Interpretation to confirm with the users (see the plan's open questions).
    /// </summary>
    public bool NoRestartFrom100 { get; set; } = true;

    /// <summary>"Cọc H liên tục": detail numbers run on past H stakes (C1, C2, H1, C3). Off: C1, C2, H1, C1.</summary>
    public bool DetailContinuousThroughH { get; set; } = true;

    /// <summary>
    /// Off = "Cọc C bỏ qua vị trí cọc H" (default): an H stake takes no C number (C4, H1, C5). On: the H position is
    /// counted, so the C number after it jumps (C4, H1, C6).
    /// </summary>
    public bool CountHundredPositions { get; set; }

    /// <summary>Off = "Không tạo cọc H": 100 m stakes are named as detail stakes.</summary>
    public bool CreateHundreds { get; set; } = true;

    /// <summary>"Tên cọc theo kiểu lý trình": detail stakes are named by their station (Km0+120).</summary>
    public bool NameByStation { get; set; }

    /// <summary>"Đánh lại cọc cắm cong, siêu cao": NĐ/TĐ/P/TC/NC get the curve number (from FirstPiNumber).</summary>
    public bool RenameCurveKeys { get; set; } = true;

    /// <summary>"Số thứ tự đỉnh đầu": number of the first curve.</summary>
    public int FirstPiNumber { get; set; } = 1;

    /// <summary>"Để lại các cọc có tiếp đầu": stakes whose name starts with one of these keep it.</summary>
    public List<string> KeepPrefixes { get; set; } = new List<string>();

    public int StationDecimals { get; set; } = 2;

    public StakeNamingOptions Clone()
    {
        var copy = (StakeNamingOptions)MemberwiseClone();
        copy.KeepPrefixes = new List<string>(KeepPrefixes);
        return copy;
    }
}

/// <summary>Names the stakes of a route the way AND Design's "Đánh lại toàn bộ tên cọc" does.</summary>
public static class StakeNamer
{
    /// <summary>
    /// New names for stakes[fromIndex..toIndex] (toIndex -1 = the last stake); stakes outside the range keep theirs.
    /// stakes must be in station order.
    /// </summary>
    public static List<string> Name(IReadOnlyList<RouteStake> stakes, StakeNamingOptions options, int fromIndex = 0, int toIndex = -1)
    {
        if (stakes == null) throw new ArgumentNullException(nameof(stakes));
        options ??= new StakeNamingOptions();
        if (toIndex < 0) toIndex = stakes.Count - 1;
        if (stakes.Count > 0 && (fromIndex < 0 || toIndex >= stakes.Count || fromIndex > toIndex))
            throw new ArgumentOutOfRangeException(nameof(fromIndex), "Khoảng cọc không hợp lệ.");

        var names = stakes.Select(s => s.Name).ToList();
        var next = options.FirstDetailNumber;
        for (var i = fromIndex; i <= toIndex && i < stakes.Count; i++)
        {
            var s = stakes[i];
            if (IsKept(s.Name, options.KeepPrefixes)) continue;

            switch (s.Role)
            {
                case StakeRole.CurveKey:
                    names[i] = options.RenameCurveKeys || s.Name.Length == 0
                        ? s.CurvePrefix + (s.CurveNumber - 1 + options.FirstPiNumber).ToString(CultureInfo.InvariantCulture)
                        : s.Name;
                    break;
                case StakeRole.Km:
                    names[i] = KmName(s.Station);
                    // The first stake of the range keeps FirstDetailNumber, even when it is a Km stake.
                    if (i > fromIndex && options.RestartPerKm && !(options.NoRestartFrom100 && next - 1 >= 100)) next = 1;
                    break;
                case StakeRole.Hundred when options.CreateHundreds:
                    names[i] = HundredName(s.Station);
                    if (!options.DetailContinuousThroughH) next = 1;
                    else if (options.CountHundredPositions) next++;
                    break;
                default:
                    names[i] = options.NameByStation
                        ? StationName(s.Station, options.StationDecimals)
                        : options.DetailPrefix + (next++).ToString(CultureInfo.InvariantCulture);
                    break;
            }
        }

        return names;
    }

    /// <summary>
    /// Names safe to use as object names (sample lines must differ): a repeated name gets " (Km&lt;k&gt;)", then "-2", "-3".
    /// An empty name becomes the station.
    /// </summary>
    public static List<string> UniqueLabels(IReadOnlyList<string> names, IReadOnlyList<double> stations)
    {
        if (names == null) throw new ArgumentNullException(nameof(names));
        if (stations == null || stations.Count != names.Count) throw new ArgumentException("Cần một lý trình cho mỗi tên.", nameof(stations));

        var baseNames = names.Select((n, i) => string.IsNullOrWhiteSpace(n) ? StationName(stations[i], 2) : n.Trim()).ToList();
        var repeated = new HashSet<string>(baseNames.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key),
            StringComparer.OrdinalIgnoreCase);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(names.Count);
        for (var i = 0; i < baseNames.Count; i++)
        {
            var label = repeated.Contains(baseNames[i])
                ? baseNames[i] + " (Km" + ((long)Math.Floor(stations[i] / 1000.0 + 1e-9)).ToString(CultureInfo.InvariantCulture) + ")"
                : baseNames[i];
            var candidate = label;
            for (var k = 2; !used.Add(candidate); k++) candidate = label + "-" + k.ToString(CultureInfo.InvariantCulture);
            result.Add(candidate);
        }

        return result;
    }

    // AwayFromZero: Math.Round(9.5) is 10 under banker's rounding, which would turn station 950 into "H0".
    /// <summary>
    /// The name to print on the plan: a sample line name without the suffix UniqueLabels added ("H1 (Km1)" → "H1",
    /// "C3 (Km0)-2" → "C3").
    /// </summary>
    public static string DisplayName(string label)
    {
        var name = (label ?? "").Trim();
        var at = name.LastIndexOf(" (Km", StringComparison.Ordinal);
        if (at <= 0) return name;
        var close = name.IndexOf(')', at);
        if (close < 0) return name;
        var number = name.Substring(at + 4, close - at - 4);
        var rest = name.Substring(close + 1);
        var restOk = rest.Length == 0 || (rest[0] == '-' && rest.Length > 1 && rest.Skip(1).All(char.IsDigit));
        return number.Length > 0 && number.All(char.IsDigit) && restOk ? name.Substring(0, at) : name;
    }

    public static string KmName(double station) => "Km" + ((long)Math.Round(station / 1000.0, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);

    public static string HundredName(double station) => "H" + ((long)Math.Round(station / 100.0, MidpointRounding.AwayFromZero) % 10).ToString(CultureInfo.InvariantCulture);

    /// <summary>"Km0+120", or "Km0+125.50" when the station is not a whole metre.</summary>
    public static string StationName(double station, int decimals)
    {
        var whole = Math.Abs(station - Math.Round(station)) < 0.005;
        return StationFormatter.Format(station, whole ? 0 : Math.Max(0, Math.Min(6, decimals)));
    }

    private static bool IsKept(string name, IEnumerable<string> prefixes) =>
        !string.IsNullOrEmpty(name) && prefixes != null
        && prefixes.Any(p => !string.IsNullOrWhiteSpace(p) && name.StartsWith(p.Trim(), StringComparison.OrdinalIgnoreCase));
}
