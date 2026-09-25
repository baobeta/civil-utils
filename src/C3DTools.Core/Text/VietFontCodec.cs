using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace C3DTools.Core.Text;

/// <summary>How Vietnamese letters are stored in a drawing's text.</summary>
public enum VietEncoding { Unicode, Tcvn3, Vni }

/// <summary>
/// Converts Vietnamese text between Unicode and the two legacy font encodings still found in old drawings:
/// TCVN3 (ABC, fonts .VnTime / .VnArial…) and VNI-Windows (fonts VNI-Times…).
/// Legacy text reaches .NET as Latin-1 characters (U+00A0–U+00FF), one per font byte.
/// </summary>
/// <remarks>
/// TCVN3 has one code per lowercase letter plus Ă Â Ê Ô Ơ Ư Đ; toned capitals are written with the lowercase code in an
/// all-capitals font (.VnTimeH…). So Unicode → TCVN3 writes a toned capital as its lowercase code, and TCVN3 → Unicode
/// upper-cases the text only when told the font is an all-capitals one (IsUpperCaseFont).
/// VNI writes a letter as its base letter followed by one mark character (circumflex/breve already combined with the tone);
/// i, ỵ, đ, ơ, ư have codes of their own.
/// </remarks>
public static class VietFontCodec
{
    /// <summary>12 vowel groups × (plain, huyền, hỏi, ngã, sắc, nặng), then đ. Lowercase.</summary>
    public const string UnicodeLower =
        "aàảãáạ" + "ăằẳẵắặ" + "âầẩẫấậ" +
        "eèẻẽéẹ" + "êềểễếệ" +
        "iìỉĩíị" +
        "oòỏõóọ" + "ôồổỗốộ" + "ơờởỡớợ" +
        "uùủũúụ" + "ưừửữứự" +
        "yỳỷỹýỵ" +
        "đ";

    public const string UnicodeUpper =
        "AÀẢÃÁẠ" + "ĂẰẲẴẮẶ" + "ÂẦẨẪẤẬ" +
        "EÈẺẼÉẸ" + "ÊỀỂỄẾỆ" +
        "IÌỈĨÍỊ" +
        "OÒỎÕÓỌ" + "ÔỒỔỖỐỘ" + "ƠỜỞỠỚỢ" +
        "UÙỦŨÚỤ" + "ƯỪỬỮỨỰ" +
        "YỲỶỸÝỴ" +
        "Đ";

    /// <summary>TCVN3 code of each UnicodeLower letter, same order.</summary>
    private static readonly string[] Tcvn3Lower =
    {
        "a", "µ", "¶", "·", "¸", "¹",           // a à ả ã á ạ
        "¨", "»", "¼", "½", "¾", "Æ",      // ă ằ ẳ ẵ ắ ặ
        "©", "Ç", "È", "É", "Ê", "Ë",      // â ầ ẩ ẫ ấ ậ
        "e", "Ì", "Î", "Ï", "Ð", "Ñ",           // e è ẻ ẽ é ẹ
        "ª", "Ò", "Ó", "Ô", "Õ", "Ö",      // ê ề ể ễ ế ệ
        "i", "×", "Ø", "Ü", "Ý", "Þ",           // i ì ỉ ĩ í ị
        "o", "ß", "á", "â", "ã", "ä",           // o ò ỏ õ ó ọ
        "«", "å", "æ", "ç", "è", "é",      // ô ồ ổ ỗ ố ộ
        "¬", "ê", "ë", "ì", "í", "î",      // ơ ờ ở ỡ ớ ợ
        "u", "ï", "ñ", "ò", "ó", "ô",           // u ù ủ ũ ú ụ
        "­", "õ", "ö", "÷", "ø", "ù",      // ư ừ ử ữ ứ ự
        "y", "ú", "û", "ü", "ý", "þ",           // y ỳ ỷ ỹ ý ỵ
        "®",                                                        // đ
    };

    /// <summary>The only capitals TCVN3 encodes.</summary>
    private static readonly (char letter, char code)[] Tcvn3Capitals =
    {
        ('Ă', '¡'), ('Â', '¢'), ('Ê', '£'), ('Ô', '¤'), ('Ơ', '¥'), ('Ư', '¦'), ('Đ', '§'),
    };

    /// <summary>VNI-Windows code of each UnicodeLower letter, same order. Capitals: every character upper-cased (Latin-1 pairs).</summary>
    private static readonly string[] VniLower =
    {
        "a", "aø", "aû", "aõ", "aù", "aï",      // a aø aû aõ aù aï
        "aê", "aè", "aú", "aü", "aé", "aë", // aê aè aú aü aé aë
        "aâ", "aà", "aå", "aã", "aá", "aä", // aâ aà aå aã aá aä
        "e", "eø", "eû", "eõ", "eù", "eï",
        "eâ", "eà", "eå", "eã", "eá", "eä",
        "i", "ì", "æ", "ó", "í", "ò",           // i ì æ ó í ò
        "o", "oø", "oû", "oõ", "où", "oï",
        "oâ", "oà", "oå", "oã", "oá", "oä",
        "ô", "ôø", "ôû", "ôõ", "ôù", "ôï", // ô ôø ôû ôõ ôù ôï
        "u", "uø", "uû", "uõ", "uù", "uï",
        "ö", "öø", "öû", "öõ", "öù", "öï", // ö öø öû öõ öù öï
        "y", "yø", "yû", "yõ", "yù", "î",       // y yø yû yõ yù î
        "ñ",                                                        // ñ
    };

    private const string Vowels = "aăâeêioôơuưy";

    private static readonly Dictionary<char, string> ToTcvn3 = new Dictionary<char, string>();
    private static readonly Dictionary<char, char> FromTcvn3 = new Dictionary<char, char>();
    private static readonly Dictionary<char, string> ToVni = new Dictionary<char, string>();
    private static readonly Dictionary<string, char> FromVni = new Dictionary<string, char>(StringComparer.Ordinal);

    /// <summary>Letter → (base vowel, tone 0–5), for Detect's syllable checks.</summary>
    private static readonly Dictionary<char, (char vowel, int tone)> Letters = new Dictionary<char, (char, int)>();

    private static readonly HashSet<string> Nuclei = new HashSet<string>(StringComparer.Ordinal)
    {
        "a", "ă", "â", "e", "ê", "i", "o", "ô", "ơ", "u", "ư", "y",
        "ai", "ao", "au", "ay", "âu", "ây", "eo", "êu", "ia", "iê", "iu", "oa", "oă", "oe", "oi", "oo", "ôi", "ơi",
        "ua", "uâ", "uê", "ui", "uô", "uơ", "uy", "ưa", "ưi", "ươ", "ưu", "yê",
        "iêu", "yêu", "oai", "oay", "oao", "oeo", "uai", "uay", "uao", "uây", "uêu", "uôi", "ươi", "ươu", "uyê", "uya", "uyu",
    };

    static VietFontCodec()
    {
        for (var i = 0; i < UnicodeLower.Length; i++)
        {
            char lower = UnicodeLower[i], upper = UnicodeUpper[i];
            var tcvn = Tcvn3Lower[i];
            ToTcvn3[lower] = tcvn;
            ToTcvn3[upper] = tcvn.Length == 1 && tcvn[0] < 0x80 ? upper.ToString() : tcvn;
            if (tcvn[0] >= 0x80) FromTcvn3[tcvn[0]] = lower;

            var vni = VniLower[i];
            var vniUpper = vni.ToUpperInvariant();
            ToVni[lower] = vni;
            ToVni[upper] = vniUpper;
            if (vni.Length > 1 || vni[0] >= 0x80)
            {
                FromVni[vni] = lower;
                FromVni[vniUpper] = upper;
            }

            if (i < 72)
            {
                Letters[lower] = (Vowels[i / 6], i % 6);
                Letters[upper] = (Vowels[i / 6], i % 6);
            }
            else
            {
                Letters[lower] = ('d', 0);
                Letters[upper] = ('d', 0);
            }
        }

        foreach (var (letter, code) in Tcvn3Capitals)
        {
            ToTcvn3[letter] = code.ToString();
            FromTcvn3[code] = letter;
        }

        // VNI text often mixes cases ("Aù" typed with Caps Lock off for the mark): the base letter decides the case.
        foreach (var pair in FromVni.ToList())
        {
            if (pair.Key.Length != 2) continue;
            var mixed1 = pair.Key.Substring(0, 1) + char.ToLowerInvariant(pair.Key[1]);
            var mixed2 = pair.Key.Substring(0, 1) + char.ToUpperInvariant(pair.Key[1]);
            foreach (var key in new[] { mixed1, mixed2 })
                if (!FromVni.ContainsKey(key)) FromVni[key] = pair.Value;
        }
    }

    /// <summary>All 134 Vietnamese letters that are not plain ASCII, lowercase then uppercase.</summary>
    public static IReadOnlyList<char> VietnameseLetters { get; } =
        (UnicodeLower + UnicodeUpper).Where(c => c >= 0x80).ToArray();

    /// <summary>The legacy code of a Unicode letter (for tests and the preview), or null when the letter has none.</summary>
    public static string Code(char letter, VietEncoding encoding)
    {
        switch (encoding)
        {
            case VietEncoding.Tcvn3: return ToTcvn3.TryGetValue(letter, out var t) ? t : null;
            case VietEncoding.Vni: return ToVni.TryGetValue(letter, out var v) ? v : null;
            default: return letter.ToString();
        }
    }

    /// <summary>Legacy text → Unicode. upperCaseFont: the text uses a TCVN3 all-capitals font (.VnTimeH), so the result is upper-cased.</summary>
    public static string ToUnicode(string text, VietEncoding from, bool upperCaseFont = false)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        switch (from)
        {
            case VietEncoding.Tcvn3:
            {
                var sb = new StringBuilder(text.Length);
                foreach (var c in text) sb.Append(FromTcvn3.TryGetValue(c, out var u) ? u : c);
                var result = sb.ToString();
                return upperCaseFont ? result.ToUpperInvariant() : result;
            }
            case VietEncoding.Vni:
            {
                var sb = new StringBuilder(text.Length);
                for (var i = 0; i < text.Length; i++)
                {
                    if (i + 1 < text.Length && FromVni.TryGetValue(text.Substring(i, 2), out var pair))
                    {
                        sb.Append(pair);
                        i++;
                    }
                    else sb.Append(FromVni.TryGetValue(text[i].ToString(), out var single) ? single : text[i]);
                }

                return sb.ToString();
            }
            default:
                return text;
        }
    }

    /// <summary>Unicode (composed or decomposed) → legacy text. Characters the encoding has no code for are kept.</summary>
    public static string FromUnicode(string text, VietEncoding to)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        if (to == VietEncoding.Unicode) return text;
        var map = to == VietEncoding.Tcvn3 ? ToTcvn3 : ToVni;
        var sb = new StringBuilder(text.Length + 8);
        foreach (var c in text.Normalize(NormalizationForm.FormC)) sb.Append(map.TryGetValue(c, out var code) ? code : c.ToString());
        return sb.ToString();
    }

    public static string Convert(string text, VietEncoding from, VietEncoding to, bool upperCaseFont = false)
    {
        if (from == to) return text ?? "";
        return FromUnicode(ToUnicode(text, from, upperCaseFont), to);
    }

    /// <summary>
    /// The most plausible encoding of text: each reading is scored by the Vietnamese letters it gives, minus stray Latin-1
    /// symbols, syllables with more than one tone, impossible vowel clusters and capitals inside a lowercase word.
    /// Plain ASCII goes to Unicode. A tie goes to preferred (e.g. the encoding most other texts of the drawing use), else Unicode:
    /// "Cát" reads as Unicode "Cát" or TCVN3 "Cỏt" equally well.
    /// </summary>
    public static VietEncoding Detect(string text, VietEncoding preferred = VietEncoding.Unicode)
    {
        if (string.IsNullOrEmpty(text) || text.All(c => c < 0x80)) return VietEncoding.Unicode;
        if (text.Any(c => c > 0xFF)) return VietEncoding.Unicode;   // legacy codes are all ≤ U+00FF
        var scores = new Dictionary<VietEncoding, int>
        {
            [VietEncoding.Unicode] = Score(text),
            [VietEncoding.Tcvn3] = Score(ToUnicode(text, VietEncoding.Tcvn3)),
            [VietEncoding.Vni] = Score(ToUnicode(text, VietEncoding.Vni)),
        };
        var bestScore = scores.Values.Max();
        if (scores[preferred] == bestScore) return preferred;
        if (scores[VietEncoding.Unicode] == bestScore) return VietEncoding.Unicode;
        return scores[VietEncoding.Tcvn3] == bestScore ? VietEncoding.Tcvn3 : VietEncoding.Vni;
    }

    /// <summary>A .VnXxxH font: TCVN3 all-capitals variant (".VnTimeH", "VNARIALH.TTF"). Path and extension are ignored.</summary>
    public static bool IsUpperCaseFont(string fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName)) return false;
        string name;
        try { name = Path.GetFileName(fontName.Trim()); }
        catch (ArgumentException) { return false; }
        // Not GetFileNameWithoutExtension: ".VnTimeH" would be all extension.
        var ext = Path.GetExtension(name);
        if (new[] { ".ttf", ".otf", ".ttc", ".shx" }.Contains(ext, StringComparer.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - ext.Length);
        if (name.StartsWith(".", StringComparison.Ordinal)) name = name.Substring(1);
        if (name.Length < 4 || !name.StartsWith("Vn", StringComparison.OrdinalIgnoreCase)) return false;
        var last = name[name.Length - 1];
        return last == 'H' || (last == 'h' && name == name.ToLowerInvariant());
    }

    /// <summary>
    /// Converts the visible text of MText contents; control codes (\P \L \O \f…; \H…; \C…; \S…; { } \\ %%d …) are kept.
    /// Stacked text (\S…;) is converted. When converting to Unicode, every \f/\F font becomes unicodeFont (if given),
    /// keeping its |b|i|c|p options. upperCaseFont: the text style's font is a TCVN3 all-capitals font; \f changes it per group.
    /// </summary>
    public static string ConvertMText(string contents, VietEncoding from, VietEncoding to, string unicodeFont = null, bool upperCaseFont = false)
    {
        if (string.IsNullOrEmpty(contents) || from == to) return contents ?? "";
        var sb = new StringBuilder(contents.Length + 16);
        var run = new StringBuilder();
        var groups = new Stack<bool>();
        var upper = upperCaseFont;

        void Flush()
        {
            if (run.Length == 0) return;
            sb.Append(Convert(run.ToString(), from, to, upper && from == VietEncoding.Tcvn3));
            run.Clear();
        }

        var i = 0;
        while (i < contents.Length)
        {
            var c = contents[i];
            if (c == '\\' && i + 1 < contents.Length)
            {
                Flush();
                var code = contents[i + 1];
                int end;
                switch (code)
                {
                    case 'f':
                    case 'F':
                    {
                        end = Semicolon(contents, i + 2);
                        var body = contents.Substring(i + 2, end - (i + 2));
                        var bar = body.IndexOf('|');
                        var font = bar < 0 ? body : body.Substring(0, bar);
                        upper = IsUpperCaseFont(font);
                        if (to == VietEncoding.Unicode && !string.IsNullOrEmpty(unicodeFont))
                            sb.Append("\\f").Append(unicodeFont).Append(bar < 0 ? "" : body.Substring(bar)).Append(end < contents.Length ? ";" : "");
                        else sb.Append(contents, i, Math.Min(end + 1, contents.Length) - i);
                        i = end + 1;
                        continue;
                    }
                    case 'S':
                        end = Semicolon(contents, i + 2);
                        sb.Append("\\S").Append(Convert(contents.Substring(i + 2, end - (i + 2)), from, to, upper && from == VietEncoding.Tcvn3));
                        if (end < contents.Length) sb.Append(';');
                        i = end + 1;
                        continue;
                    case 'H':
                    case 'C':
                    case 'c':
                    case 'W':
                    case 'Q':
                    case 'A':
                    case 'T':
                    case 'p':
                        end = Semicolon(contents, i + 2);
                        sb.Append(contents, i, Math.Min(end + 1, contents.Length) - i);
                        i = end + 1;
                        continue;
                    case 'U':
                        // \U+XXXX: already a Unicode character.
                        end = i + 2 < contents.Length && contents[i + 2] == '+' ? Math.Min(i + 7, contents.Length) : i + 2;
                        sb.Append(contents, i, end - i);
                        i = end;
                        continue;
                    case 'M':
                        // \M+nXXXX: a double-byte character.
                        end = i + 2 < contents.Length && contents[i + 2] == '+' ? Math.Min(i + 8, contents.Length) : i + 2;
                        sb.Append(contents, i, end - i);
                        i = end;
                        continue;
                    default:
                        // \P \L \l \O \o \K \k \X \N \~ \\ \{ \} and anything unknown: two characters, kept.
                        sb.Append(contents, i, 2);
                        i += 2;
                        continue;
                }
            }

            if (c == '{')
            {
                Flush();
                groups.Push(upper);
                sb.Append(c);
            }
            else if (c == '}')
            {
                Flush();
                if (groups.Count > 0) upper = groups.Pop();
                sb.Append(c);
            }
            else if (c == '%' && i + 2 < contents.Length && contents[i + 1] == '%')
            {
                // %%d %%c %%p %%u %%o %%% and %%nnn.
                Flush();
                var end = i + 3;
                if (char.IsDigit(contents[i + 2]))
                    while (end < contents.Length && end < i + 5 && char.IsDigit(contents[end])) end++;
                sb.Append(contents, i, end - i);
                i = end;
                continue;
            }
            else run.Append(c);

            i++;
        }

        Flush();
        return sb.ToString();
    }

    /// <summary>Index of the ';' ending a control code, or contents.Length when it is missing.</summary>
    private static int Semicolon(string contents, int from)
    {
        var end = contents.IndexOf(';', Math.Min(from, contents.Length));
        return end < 0 ? contents.Length : end;
    }

    private static int Score(string reading)
    {
        var score = 0;
        foreach (var c in reading)
        {
            if (c < 0x80) continue;
            if (Letters.ContainsKey(c)) score++;
            else if (c >= 0xA0 && c <= 0xFF) score -= 2;
        }

        var word = new StringBuilder();
        foreach (var c in reading + " ")
        {
            if (char.IsLetter(c))
            {
                word.Append(c);
                continue;
            }

            if (word.Length > 0) score -= WordPenalty(word.ToString());
            word.Clear();
        }

        return score;
    }

    private static int WordPenalty(string word)
    {
        if (word.All(c => c < 0x80)) return 0;
        var penalty = 0;
        var tones = word.Count(c => Letters.TryGetValue(c, out var l) && l.tone > 0);
        if (tones > 1) penalty += 3 * (tones - 1);

        for (var i = 1; i < word.Length; i++)
            if (word[i] >= 0x80 && char.IsUpper(word[i]) && char.IsLower(word[i - 1])) penalty += 2;

        // Vowel clusters, with the i of "gi" and the u of "qu" read as part of the consonant.
        var bases = new StringBuilder();
        foreach (var c in word.ToLowerInvariant())
            bases.Append(Letters.TryGetValue(c, out var l) ? l.vowel : c);
        var s = bases.ToString();
        var k = 0;
        while (k < s.Length)
        {
            if (Vowels.IndexOf(s[k]) < 0)
            {
                k++;
                continue;
            }

            var start = k;
            while (k < s.Length && Vowels.IndexOf(s[k]) >= 0) k++;
            var cluster = s.Substring(start, k - start);
            if (cluster.Length > 1 && start > 0 && ((s[start - 1] == 'g' && cluster[0] == 'i') || (s[start - 1] == 'q' && cluster[0] == 'u')))
                cluster = cluster.Substring(1);
            if (!Nuclei.Contains(cluster)) penalty += 2;
        }

        return penalty;
    }
}
