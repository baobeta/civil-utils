using System.Collections.Generic;
using System.Linq;
using C3DTools.Core.Presets;
using C3DTools.Core.Text;
using Xunit;

namespace C3DTools.Core.Tests.Text;

public class FontConversionSessionTests
{
    private const string Tcvn3Road = "®­êng";          // đường
    private const string VniRoad = "ñöôøng";       // đường

    private static FontConversionSession Session(params FontTextItem[] items)
    {
        var s = new FontConversionSession(new FontConversionOptions());
        s.Load(items);
        return s;
    }

    private static List<string> Watch(FontConversionSession s)
    {
        var names = new List<string>();
        s.PropertyChanged += (o, e) => names.Add(e.PropertyName);
        return names;
    }

    [Fact]
    public void Defaults_auto_to_unicode_with_the_preset_font()
    {
        var s = new FontConversionSession(new FontConversionOptions { TargetFont = "Times New Roman" });

        Assert.Null(s.Source);
        Assert.Equal(VietEncoding.Unicode, s.Target);
        Assert.Equal("Times New Roman", s.TargetFont);
        Assert.True(s.ReplaceStyleFont);
        Assert.True(s.CanReplaceStyleFont);
        Assert.False(s.ConvertBlockDefinitions);
        Assert.False(s.WholeDrawing);
        Assert.False(s.CanApply);
        Assert.Equal(new[] { "Tự nhận dạng", "Unicode", "TCVN3 (ABC)", "VNI Windows" }, s.SourceNames);
        Assert.Equal(new[] { "Unicode", "TCVN3 (ABC)", "VNI Windows" }, s.TargetNames);
    }

    [Fact]
    public void Counts_detected_encodings_per_kind_and_changes()
    {
        var s = Session(
            new FontTextItem("DBText", Tcvn3Road, false, false),
            new FontTextItem("DBText", VniRoad, false, false),
            new FontTextItem("DBText", "Km0", false, false),
            new FontTextItem("MText", "{\\f.VnTime;" + Tcvn3Road + "}", true, false),
            new FontTextItem("MText", "đường", true, false));

        Assert.Equal(3, s.ChangeCount);
        Assert.True(s.CanApply);
        Assert.Equal("3 chuỗi sẽ được chuyển", s.SummaryText);
        Assert.Equal("DBText: TCVN3 1, VNI 1\nMText: TCVN3 1, Unicode 1", s.DetectedText);
        Assert.Equal(new[] { "đường", "đường", "{\\fArial;đường}" }, s.Preview.Select(p => p.After));
        Assert.Equal("DBText", s.Preview[0].Kind);
        Assert.Equal(Tcvn3Road, s.Preview[0].Before);
    }

    [Fact]
    public void Preview_shows_the_first_twenty()
    {
        var items = Enumerable.Range(0, 25).Select(i => new FontTextItem("DBText", Tcvn3Road + i, false, false)).ToArray();
        var s = Session(items);

        Assert.Equal(25, s.ChangeCount);
        Assert.Equal(20, s.Preview.Count);
        Assert.Equal("đường19", s.Preview[19].After);
    }

    [Fact]
    public void Forced_source_overrides_detection()
    {
        // "Cát" is plain Unicode to Detect; forced TCVN3 reads á (0xE1) as ỏ.
        var s = Session(new FontTextItem("DBText", "Cát", false, false));
        Assert.Equal(0, s.ChangeCount);

        s.SourceIndex = 2;

        Assert.Equal(VietEncoding.Tcvn3, s.Source);
        Assert.Equal(1, s.ChangeCount);
        Assert.Equal("Cỏt", s.Preview.Single().After);
    }

    [Fact]
    public void Changing_the_target_recomputes_and_disables_style_font()
    {
        var s = Session(new FontTextItem("DBText", "đường", false, false), new FontTextItem("DBText", Tcvn3Road, false, false));
        var names = Watch(s);

        s.TargetIndex = 1;

        Assert.Equal(VietEncoding.Tcvn3, s.Target);
        Assert.False(s.CanReplaceStyleFont);
        Assert.Equal(1, s.ChangeCount);
        Assert.Equal(Tcvn3Road, s.Preview.Single().After);
        Assert.Contains("TargetIndex", names);
        Assert.Contains("CanReplaceStyleFont", names);
        Assert.Contains("SummaryText", names);
        Assert.Contains("CanApply", names);
    }

    [Fact]
    public void Tcvn3_all_capitals_font_upper_cases()
    {
        var s = Session(new FontTextItem("DBText", Tcvn3Road, false, true));

        Assert.Equal("ĐƯỜNG", s.Convert(s.Items[0]));
    }

    [Fact]
    public void Ambiguous_strings_follow_the_encoding_the_other_strings_use()
    {
        // "Cèng" alone ties Unicode "Cèng" and TCVN3 "Cống"; with clear TCVN3 strings around it, TCVN3 wins.
        var alone = Session(new FontTextItem("DBText", "Cèng", false, false));
        Assert.Equal(0, alone.ChangeCount);

        var s = Session(new FontTextItem("DBText", "Cèng", false, false), new FontTextItem("DBText", Tcvn3Road, false, false));

        Assert.Equal(new[] { "Cống", "đường" }, s.Preview.Select(p => p.After));
        Assert.Equal("DBText: TCVN3 2", s.DetectedText);
    }

    [Fact]
    public void Style_font_guides_detection()
    {
        // "Cèng" in a .VnTime style is TCVN3 "Cống"; "Ø600" in an Arial style stays.
        var s = Session(
            new FontTextItem("DBText", "C\u00E8ng", false, false, ".VnTime"),
            new FontTextItem("DBText", "Ø600", false, false, "Arial"),
            new FontTextItem("MText", "{\\f.VnTime;C\u00E8ng}", true, false, "Arial"));

        Assert.Equal(new[] { "Cống", "{\\fArial;Cống}" }, s.Preview.Select(p => p.After));
    }

    [Fact]
    public void Engineering_symbols_are_not_touched_in_a_tcvn3_drawing()
    {
        var s = Session(
            new FontTextItem("DBText", Tcvn3Road, false, false),
            new FontTextItem("DBText", "D=Ø600", false, false),
            new FontTextItem("DBText", "2×3", false, false),
            new FontTextItem("DBText", "½", false, false));

        Assert.Equal(1, s.ChangeCount);
    }

    [Fact]
    public void Convert_returns_null_when_nothing_changes()
    {
        var s = Session();

        Assert.Null(s.Convert(new FontTextItem("DBText", "Km0+100", false, false)));
        Assert.Null(s.Convert(new FontTextItem("MText", "\\Pđường", true, false)));
        Assert.Equal("\\Pđường", s.Convert(new FontTextItem("MText", "\\P" + Tcvn3Road, true, false)));
    }

    [Fact]
    public void Scope_changes_make_the_preview_stale_until_the_next_load()
    {
        var s = Session(new FontTextItem("DBText", Tcvn3Road, false, false));
        var names = Watch(s);

        s.WholeDrawing = true;

        Assert.True(s.IsStale);
        Assert.Empty(s.Preview);
        Assert.Equal("Bấm Xem trước để quét lại", s.SummaryText);
        Assert.True(s.CanApply);
        Assert.Contains("WholeDrawing", names);

        s.Load(new[] { new FontTextItem("DBText", Tcvn3Road, false, false) });
        Assert.False(s.IsStale);
        Assert.Single(s.Preview);

        s.ConvertBlockDefinitions = true;
        Assert.True(s.IsStale);
    }

    [Fact]
    public void Nothing_to_convert()
    {
        var s = Session(new FontTextItem("DBText", "Km0", false, false));

        Assert.False(s.CanApply);
        Assert.Equal("Không có chuỗi nào cần chuyển", s.SummaryText);
        Assert.Equal("Không có chữ tiếng Việt", s.DetectedText);
    }
}
