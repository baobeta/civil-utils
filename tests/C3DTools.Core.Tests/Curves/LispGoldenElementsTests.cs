using System;
using System.Globalization;
using System.IO;
using C3DTools.Core.Curves;
using Xunit;

namespace C3DTools.Core.Tests.Curves;

/// <summary>Checks Core's curve elements against CSVs produced by YTC.lsp, the reference implementation.</summary>
public class LispGoldenElementsTests
{
    [Theory]
    [InlineData("symmetric_scs.csv")]
    [InlineData("simple_arc.csv")]
    public void Matches_lisp_output(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Curves", "Golden", fileName);
        var lines = File.ReadAllLines(path);

        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cols = lines[i].Split(',');

            var deltaDegrees = ParseDms(cols[1]);
            var radius = double.Parse(cols[2], NumberStyles.Float, CultureInfo.InvariantCulture);
            var length = double.Parse(cols[3], NumberStyles.Float, CultureInfo.InvariantCulture);
            var expectedT = double.Parse(cols[4], NumberStyles.Float, CultureInfo.InvariantCulture);
            var expectedP = double.Parse(cols[5], NumberStyles.Float, CultureInfo.InvariantCulture);
            var expectedK = double.Parse(cols[6], NumberStyles.Float, CultureInfo.InvariantCulture);

            var deltaRadians = deltaDegrees * Math.PI / 180.0;
            var e = CurveElementsCalculator.Compute(radius, deltaRadians, length, length);

            Assert.True(Math.Abs(e.T1 - expectedT) <= 0.01, $"{fileName} row {i}: T1 {e.T1} vs {expectedT}");
            Assert.True(Math.Abs(e.P - expectedP) <= 0.01, $"{fileName} row {i}: P {e.P} vs {expectedP}");
            Assert.True(Math.Abs(e.K - expectedK) <= 0.01, $"{fileName} row {i}: K {e.K} vs {expectedK}");
        }
    }

    /// <summary>Parses the LISP's angle text, e.g. "60d00'00" or "31d31'52.4", into decimal degrees.</summary>
    private static double ParseDms(string text)
    {
        var dIndex = text.IndexOf('d');
        var mIndex = text.IndexOf('\'');
        var degrees = double.Parse(text.Substring(0, dIndex), NumberStyles.Float, CultureInfo.InvariantCulture);
        var minutes = double.Parse(text.Substring(dIndex + 1, mIndex - dIndex - 1), NumberStyles.Float, CultureInfo.InvariantCulture);
        var seconds = double.Parse(text.Substring(mIndex + 1), NumberStyles.Float, CultureInfo.InvariantCulture);
        return degrees + minutes / 60.0 + seconds / 3600.0;
    }
}
