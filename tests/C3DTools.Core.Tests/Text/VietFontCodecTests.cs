using System.Linq;
using System.Text;
using C3DTools.Core.Text;
using Xunit;

namespace C3DTools.Core.Tests.Text;

public class VietFontCodecTests
{
    // Written by hand from TCVN3 (ABC) font charts, independent of the codec's tables: legacy text → Unicode.
    public static readonly TheoryData<string, string> Tcvn3Pairs = new TheoryData<string, string>
    {
        { "\u00B5", "à" }, { "\u00B6", "ả" }, { "\u00B7", "ã" }, { "\u00B8", "á" }, { "\u00B9", "ạ" },
        { "\u00A8", "ă" }, { "\u00BE", "ắ" }, { "\u00C6", "ặ" },
        { "\u00A9", "â" }, { "\u00CA", "ấ" }, { "\u00C7", "ầ" }, { "\u00CB", "ậ" },
        { "\u00D0", "é" }, { "\u00CC", "è" },
        { "\u00AA", "ê" }, { "\u00D5", "ế" }, { "\u00D6", "ệ" },
        { "\u00DD", "í" }, { "\u00DC", "ĩ" }, { "\u00DE", "ị" },
        { "\u00E3", "ó" }, { "\u00E4", "ọ" },
        { "\u00AB", "ô" }, { "\u00E8", "ố" }, { "\u00E9", "ộ" },
        { "\u00AC", "ơ" }, { "\u00ED", "ớ" }, { "\u00EA", "ờ" },
        { "\u00F3", "ú" }, { "\u00F1", "ủ" },
        { "\u00AD", "ư" }, { "\u00F8", "ứ" }, { "\u00F5", "ừ" },
        { "\u00FD", "ý" }, { "\u00FE", "ỵ" },
        { "\u00AE", "đ" }, { "\u00A7", "Đ" }, { "\u00A1", "Ă" }, { "\u00A2", "Â" }, { "\u00A4", "Ô" }, { "\u00A6", "Ư" },
        { "\u00AE\u00AD\u00EAng", "đường" },
        { "Ti\u00AAu chu\u00C8n", "Tiêu chuẩn" },
        { "C\u00E9ng ho\u00B5 x\u00B7 h\u00E9i ch\u00F1 ngh\u00DCa Vi\u00D6t Nam", "Cộng hoà xã hội chủ nghĩa Việt Nam" },
        { "C\u00E8ng tho\u00B8t n\u00ADíc", "Cống thoát nước" },
    };

    // VNI-Windows by hand: base letter + mark.
    public static readonly TheoryData<string, string> VniPairs = new TheoryData<string, string>
    {
        { "a\u00F9", "á" }, { "a\u00F8", "à" }, { "a\u00FB", "ả" }, { "a\u00F5", "ã" }, { "a\u00EF", "ạ" },
        { "a\u00EA", "ă" }, { "a\u00E9", "ắ" }, { "a\u00EB", "ặ" },
        { "a\u00E2", "â" }, { "a\u00E1", "ấ" }, { "a\u00E0", "ầ" }, { "a\u00E4", "ậ" },
        { "e\u00F9", "é" }, { "e\u00E2", "ê" }, { "e\u00E1", "ế" }, { "e\u00E4", "ệ" },
        { "\u00ED", "í" }, { "\u00EC", "ì" }, { "\u00E6", "ỉ" }, { "\u00F3", "ĩ" }, { "\u00F2", "ị" },
        { "o\u00E2", "ô" }, { "o\u00E1", "ố" }, { "o\u00E4", "ộ" },
        { "\u00F4", "ơ" }, { "\u00F4\u00F9", "ớ" }, { "\u00F4\u00F8", "ờ" },
        { "u\u00F9", "ú" }, { "\u00F6", "ư" }, { "\u00F6\u00F9", "ứ" },
        { "y\u00F9", "ý" }, { "\u00EE", "ỵ" }, { "\u00F1", "đ" }, { "\u00D1", "Đ" },
        { "A\u00D9", "Á" }, { "O\u00C1", "Ố" }, { "\u00D4\u00D8", "Ờ" }, { "\u00D6", "Ư" },
        { "\u00F1\u00F6\u00F4\u00F8ng", "đường" },
        { "Tie\u00E2u chua\u00E5n", "Tiêu chuẩn" },
        { "Co\u00E4ng ho\u00F8a xa\u00F5 ho\u00E4i chu\u00FB ngh\u00F3a Vie\u00E4t Nam", "Cộng hòa xã hội chủ nghĩa Việt Nam" },
        { "\u00D1\u00D6\u00D4\u00D8NG", "ĐƯỜNG" },
    };

    [Theory]
    [MemberData(nameof(Tcvn3Pairs))]
    public void Tcvn3_known_pairs(string legacy, string unicode)
    {
        Assert.Equal(unicode, VietFontCodec.ToUnicode(legacy, VietEncoding.Tcvn3));
        Assert.Equal(legacy, VietFontCodec.FromUnicode(unicode, VietEncoding.Tcvn3));
    }

    [Theory]
    [MemberData(nameof(VniPairs))]
    public void Vni_known_pairs(string legacy, string unicode)
    {
        Assert.Equal(unicode, VietFontCodec.ToUnicode(legacy, VietEncoding.Vni));
        Assert.Equal(legacy, VietFontCodec.FromUnicode(unicode, VietEncoding.Vni));
    }

    [Fact]
    public void Alphabet_has_134_letters()
    {
        Assert.Equal(134, VietFontCodec.VietnameseLetters.Count);
        Assert.Equal(134, VietFontCodec.VietnameseLetters.Distinct().Count());
        Assert.Equal(VietFontCodec.UnicodeLower.ToUpperInvariant(), VietFontCodec.UnicodeUpper);
    }

    [Fact]
    public void Vni_whole_alphabet_round_trips_both_ways()
    {
        var alphabet = new string(VietFontCodec.VietnameseLetters.ToArray());
        foreach (var letter in alphabet)
        {
            var code = VietFontCodec.FromUnicode(letter.ToString(), VietEncoding.Vni);
            Assert.True(code.All(c => c <= 0xFF), $"{letter}: {code}");
            Assert.Equal(letter.ToString(), VietFontCodec.ToUnicode(code, VietEncoding.Vni));
        }

        // Every letter written in one string, with spaces so a mark never joins the previous letter's code.
        var spaced = string.Join(" ", alphabet.Select(c => c.ToString()));
        var legacy = VietFontCodec.FromUnicode(spaced, VietEncoding.Vni);
        Assert.Equal(spaced, VietFontCodec.ToUnicode(legacy, VietEncoding.Vni));
        Assert.Equal(legacy, VietFontCodec.FromUnicode(VietFontCodec.ToUnicode(legacy, VietEncoding.Vni), VietEncoding.Vni));
    }

    [Fact]
    public void Tcvn3_whole_alphabet_round_trips_both_ways()
    {
        // Lowercase and the 7 capitals TCVN3 has: exact both ways.
        var exact = VietFontCodec.VietnameseLetters.Where(c => char.IsLower(c) || "ĂÂÊÔƠƯĐ".IndexOf(c) >= 0).ToArray();
        Assert.Equal(74, exact.Length);
        var text = new string(exact);
        var legacy = VietFontCodec.FromUnicode(text, VietEncoding.Tcvn3);
        Assert.Equal(74, legacy.Length);
        Assert.True(legacy.All(c => c >= 0xA1 && c <= 0xFE));
        Assert.Equal(text, VietFontCodec.ToUnicode(legacy, VietEncoding.Tcvn3));

        // Every TCVN3 code 0xA1–0xFE the table uses decodes to one letter and encodes back.
        var codes = Enumerable.Range(0xA1, 0xFE - 0xA1 + 1).Select(b => (char)b).Where(b => legacy.IndexOf(b) >= 0).ToArray();
        Assert.Equal(74, codes.Length);
        foreach (var code in codes)
            Assert.Equal(code.ToString(), VietFontCodec.FromUnicode(VietFontCodec.ToUnicode(code.ToString(), VietEncoding.Tcvn3), VietEncoding.Tcvn3));
    }

    [Fact]
    public void Tcvn3_toned_capitals_use_the_lowercase_code_and_come_back_with_an_all_capitals_font()
    {
        var capitals = VietFontCodec.VietnameseLetters.Where(c => char.IsUpper(c) && "ĂÂÊÔƠƯĐ".IndexOf(c) < 0).ToArray();
        Assert.Equal(60, capitals.Length);
        foreach (var capital in capitals)
        {
            var code = VietFontCodec.FromUnicode(capital.ToString(), VietEncoding.Tcvn3);
            Assert.Equal(VietFontCodec.FromUnicode(char.ToLowerInvariant(capital).ToString(), VietEncoding.Tcvn3), code);
            Assert.Equal(capital.ToString(), VietFontCodec.ToUnicode(code, VietEncoding.Tcvn3, upperCaseFont: true));
        }

        Assert.Equal("CỐNG", VietFontCodec.ToUnicode("C\u00E8ng", VietEncoding.Tcvn3, upperCaseFont: true));
    }

    [Fact]
    public void Decomposed_unicode_is_composed_first()
    {
        var decomposed = "Tiêu chuẩn".Normalize(NormalizationForm.FormD);

        Assert.Equal("Ti\u00AAu chu\u00C8n", VietFontCodec.FromUnicode(decomposed, VietEncoding.Tcvn3));
    }

    [Fact]
    public void Ascii_and_unknown_characters_are_kept()
    {
        Assert.Equal("Km0+100 (m²)", VietFontCodec.ToUnicode("Km0+100 (m²)", VietEncoding.Vni));
        Assert.Equal("R=250m", VietFontCodec.Convert("R=250m", VietEncoding.Tcvn3, VietEncoding.Vni));
    }

    [Fact]
    public void Converts_between_the_two_legacy_encodings()
    {
        Assert.Equal("Tie\u00E2u chua\u00E5n", VietFontCodec.Convert("Ti\u00AAu chu\u00C8n", VietEncoding.Tcvn3, VietEncoding.Vni));
    }

    [Theory]
    [InlineData("\u00F1\u00F6\u00F4\u00F8ng", VietEncoding.Vni)]              // ñöôøng
    [InlineData("\u00AE\u00AD\u00EAng", VietEncoding.Tcvn3)]                    // ®­êng
    [InlineData("đường", VietEncoding.Unicode)]
    [InlineData("Tie\u00E2u chua\u00E5n", VietEncoding.Vni)]
    [InlineData("Ti\u00AAu chu\u00C8n", VietEncoding.Tcvn3)]
    [InlineData("Vi\u00D6t Nam", VietEncoding.Tcvn3)]                           // ViÖt: not VNI "ViƯt"
    [InlineData("C\u00E9ng ho\u00B5 x\u00B7 h\u00E9i", VietEncoding.Tcvn3)]
    [InlineData("Co\u00E4ng ho\u00F8a xa\u00F5 ho\u00E4i", VietEncoding.Vni)]
    [InlineData("C\u00B8t", VietEncoding.Tcvn3)]                                // C¸t = Cát
    [InlineData("Tiêu", VietEncoding.Unicode)]                                  // Latin-1 only, still Unicode
    [InlineData("Cát", VietEncoding.Unicode)]
    [InlineData("Km0+100", VietEncoding.Unicode)]
    [InlineData("", VietEncoding.Unicode)]
    public void Detects_the_encoding(string text, VietEncoding expected)
    {
        Assert.Equal(expected, VietFontCodec.Detect(text));
    }

    [Theory]
    [InlineData("Ø600")]
    [InlineData("D=Ø600")]
    [InlineData("2×3")]
    [InlineData("µm")]
    [InlineData("½")]
    [InlineData("ÐƯỜNG")]
    [InlineData("naïve")]
    [InlineData("±0.5 m², 25°C, L÷2, ¼")]
    [InlineData("Cống D=Ø1500, i=0.5‰")]
    public void Symbols_and_western_letters_stay_unicode(string text)
    {
        Assert.Equal(VietEncoding.Unicode, VietFontCodec.Detect(text));
        Assert.Equal(VietEncoding.Unicode, VietFontCodec.Detect(text, VietEncoding.Unicode, "Arial"));
    }

    [Fact]
    public void Font_hint_is_a_prior()
    {
        Assert.Equal(VietEncoding.Tcvn3, VietFontCodec.Detect("\u00AE\u00AD\u00EAng", VietEncoding.Unicode, ".VnTime"));
        // Ties or small margins follow the font: ".VnTime" "Cèng" is "Cống", VNI-Times "hoø" is "hò".
        Assert.Equal(VietEncoding.Tcvn3, VietFontCodec.Detect("C\u00E8ng", VietEncoding.Unicode, ".VnTime"));
        Assert.Equal(VietEncoding.Tcvn3, VietFontCodec.Detect("ho\u00B5", VietEncoding.Unicode, "VNTIME.TTF"));
        Assert.Equal(VietEncoding.Vni, VietFontCodec.Detect("ho\u00F8", VietEncoding.Unicode, "VNI-Times"));
        // A Unicode font needs a clear legacy reading.
        Assert.Equal(VietEncoding.Unicode, VietFontCodec.Detect("ho\u00B5", VietEncoding.Tcvn3, "Arial"));
        Assert.Equal(VietEncoding.Tcvn3, VietFontCodec.Detect("\u00AE\u00AD\u00EAng", VietEncoding.Unicode, "Arial"));
    }

    [Theory]
    [InlineData(".VnTime", VietEncoding.Tcvn3)]
    [InlineData("VnArial", VietEncoding.Tcvn3)]
    [InlineData("vntime.shx", VietEncoding.Tcvn3)]
    [InlineData(@"C:\Fonts\VNTIMEH.TTF", VietEncoding.Tcvn3)]
    [InlineData("VNI-Times", VietEncoding.Vni)]
    [InlineData("VNI-Helve.ttf", VietEncoding.Vni)]
    [InlineData("Arial", VietEncoding.Unicode)]
    [InlineData("romans.shx", VietEncoding.Unicode)]
    [InlineData("Symbol", VietEncoding.Unicode)]
    public void Font_names_imply_an_encoding(string font, VietEncoding expected)
    {
        Assert.Equal(expected, VietFontCodec.FontEncoding(font));
    }

    [Fact]
    public void No_font_implies_nothing()
    {
        Assert.Null(VietFontCodec.FontEncoding(null));
        Assert.Null(VietFontCodec.FontEncoding(" "));
    }

    [Fact]
    public void MText_non_legacy_fonts_are_kept_and_their_text_not_decoded()
    {
        // \fSymbol: "a" is alpha and ¸ a Symbol glyph, not TCVN3; the .VnTime part is converted and its font replaced.
        var result = VietFontCodec.ConvertMText("{\\fSymbol|b0;a\u00B8}{\\f.VnTime;C\u00B8t}\\fArial;\u00B8", VietEncoding.Tcvn3, VietEncoding.Unicode, "Arial");

        Assert.Equal("{\\fSymbol|b0;a\u00B8}{\\fArial;Cát}\\fArial;\u00B8", result);
    }

    [Fact]
    public void MText_font_hint_is_the_first_legacy_font()
    {
        Assert.Equal(".VnTime", VietFontCodec.MTextFontHint("{\\fArial;x}{\\f.VnTime|b1;y}"));
        Assert.Equal("VNI-Times", VietFontCodec.MTextFontHint("\\FVNI-Times;y"));
        Assert.Null(VietFontCodec.MTextFontHint("{\\fArial;x}"));
        Assert.Null(VietFontCodec.MTextFontHint(null));
    }

    [Fact]
    public void A_tie_goes_to_the_preferred_encoding()
    {
        Assert.Equal(VietEncoding.Unicode, VietFontCodec.Detect("C\u00E8ng"));
        Assert.Equal(VietEncoding.Tcvn3, VietFontCodec.Detect("C\u00E8ng", VietEncoding.Tcvn3));
        Assert.Equal(VietEncoding.Vni, VietFontCodec.Detect("\u00F1\u00F6\u00F4\u00F8ng", VietEncoding.Tcvn3));   // not a tie
    }

    [Theory]
    [InlineData(".VnTimeH", true)]
    [InlineData(".VnArialH", true)]
    [InlineData("VNTIMEH.TTF", true)]
    [InlineData("vntimeh.ttf", true)]
    [InlineData(".VnTime", false)]
    [InlineData("VnTime", false)]
    [InlineData("Arial", false)]
    [InlineData("VNI-Times", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Recognises_tcvn3_all_capitals_fonts(string font, bool expected)
    {
        Assert.Equal(expected, VietFontCodec.IsUpperCaseFont(font));
    }

    [Fact]
    public void MText_font_is_replaced_and_text_converted()
    {
        // TCVN3 "Cống" is C + 0xE8 (ố).
        var result = VietFontCodec.ConvertMText("{\\fVnTime|b0|i0|c0|p34;C\u00E8ng}", VietEncoding.Tcvn3, VietEncoding.Unicode, "Arial");

        Assert.Equal("{\\fArial|b0|i0|c0|p34;Cống}", result);
    }

    [Fact]
    public void MText_control_codes_are_kept()
    {
        const string contents = "\\A1;\\H2.5;\\C1;\\W0.8;\\Q15;\\T1.1;\\pxi-3,l3;\\L\u00AE\u00AD\u00EAng\\l\\P\\O%%d%%c%%p%%u%%o%%%%%123\\\\\\{\\}\\~\\U+00B0\\M+18140x";

        var result = VietFontCodec.ConvertMText(contents, VietEncoding.Tcvn3, VietEncoding.Unicode, "Arial");

        Assert.Equal("\\A1;\\H2.5;\\C1;\\W0.8;\\Q15;\\T1.1;\\pxi-3,l3;\\Lđường\\l\\P\\O%%d%%c%%p%%u%%o%%%%%123\\\\\\{\\}\\~\\U+00B0\\M+18140x", result);
    }

    [Fact]
    public void MText_codes_that_look_like_legacy_letters_are_not_converted()
    {
        // ª (0xAA, TCVN3 ê) inside a \f name and the ¸ of a %%nnn must not change; the visible ª does.
        var result = VietFontCodec.ConvertMText("\\f.Vn\u00AA;\u00AA", VietEncoding.Tcvn3, VietEncoding.Vni);

        Assert.Equal("\\f.Vn\u00AA;e\u00E2", result);
    }

    [Fact]
    public void MText_stack_is_converted()
    {
        Assert.Equal("\\Sđ^ư;", VietFontCodec.ConvertMText("\\S\u00AE^\u00AD;", VietEncoding.Tcvn3, VietEncoding.Unicode));
    }

    [Fact]
    public void MText_all_capitals_font_upper_cases_its_group_only()
    {
        var result = VietFontCodec.ConvertMText("{\\f.VnTimeH;c\u00E8ng} c\u00E8ng", VietEncoding.Tcvn3, VietEncoding.Unicode, "Arial");

        Assert.Equal("{\\fArial;CỐNG} cống", result);
    }

    [Fact]
    public void MText_style_font_all_capitals_applies_until_a_font_change()
    {
        var result = VietFontCodec.ConvertMText("c\u00E8ng\\f.VnTime;c\u00E8ng", VietEncoding.Tcvn3, VietEncoding.Unicode, null, upperCaseFont: true);

        Assert.Equal("CỐNG\\f.VnTime;cống", result);
    }

    [Fact]
    public void MText_vni_to_unicode()
    {
        Assert.Equal("{\\fArial;Tiêu chuẩn}\\PĐường",
            VietFontCodec.ConvertMText("{\\fVNI-Times;Tie\u00E2u chua\u00E5n}\\P\u00D1\u00F6\u00F4\u00F8ng", VietEncoding.Vni, VietEncoding.Unicode, "Arial"));
    }

    [Fact]
    public void MText_unicode_to_legacy_keeps_fonts()
    {
        Assert.Equal("{\\fArial;C\u00E8ng}", VietFontCodec.ConvertMText("{\\fArial;Cống}", VietEncoding.Unicode, VietEncoding.Tcvn3, "Arial"));
    }

    [Fact]
    public void MText_without_semicolon_does_not_throw()
    {
        Assert.Equal("\\fArial", VietFontCodec.ConvertMText("\\fVnTime", VietEncoding.Tcvn3, VietEncoding.Unicode, "Arial"));
        Assert.Equal("\\H2", VietFontCodec.ConvertMText("\\H2", VietEncoding.Tcvn3, VietEncoding.Unicode));
        Assert.Equal("x\\", VietFontCodec.ConvertMText("x\\", VietEncoding.Tcvn3, VietEncoding.Unicode));
    }
}
