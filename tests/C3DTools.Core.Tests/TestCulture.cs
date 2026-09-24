using System;
using System.Globalization;

namespace C3DTools.Core.Tests;

/// <summary>Runs code under a given culture, e.g. vi-VN where the decimal separator is a comma.</summary>
public static class TestCulture
{
    public static void Run(string name, Action action)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(name);
        try { action(); }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
