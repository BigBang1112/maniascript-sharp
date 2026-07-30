using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp;

public sealed partial class TextLib
{
    public partial float ToReal(string text)
    {
        return float.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out float result) ? result : -1f;
    }

    public partial int ToInteger(string text)
    {
        return int.TryParse(text, out int result) ? result : -1;
    }

    public partial Vec3 ToColor(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return default!;
        text = text.TrimStart('#');
        if (text.Length == 6 && int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex))
        {
            return new Vec3(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f);
        }
        return default!;
    }

    public partial string SubString(string text, int start, int length)
    {
        if (string.IsNullOrEmpty(text) || start >= text.Length || length <= 0 || start < 0) return string.Empty;
        if (start + length > text.Length) length = text.Length - start;
        return text.Substring(start, length);
    }

    public partial string SubText(string text, int start, int length)
    {
        return SubString(text, start, length);
    }

    public partial int Length(string text)
    {
        return string.IsNullOrEmpty(text) ? 0 : text.Length;
    }

    public partial string ToText(int integer) => integer.ToString(CultureInfo.InvariantCulture);

    public partial string ToText(float real) => real.ToString(CultureInfo.InvariantCulture);

    public partial string ToText(bool boolean) => boolean ? "True" : "False";

    public partial string ToText(Int3 int3) => int3.ToString();

    public partial string ToText(Vec3 vec3) => vec3.ToString();

    public partial string TimeToText(int time, bool includeCentiSeconds)
    {
        var ts = TimeSpan.FromMilliseconds(time);
        var baseTime = $"{(int)Math.Floor(ts.TotalMinutes):D2}:{ts.Seconds:D2}";
        return includeCentiSeconds ? $"{baseTime}:{ts.Milliseconds / 10:D2}" : baseTime;
    }

    public partial string TimeToText(int time) => TimeToText(time, false);

    public partial string ColorToText(Vec3 color) => color.ToString();

    public partial string FormatInteger(int argument1, int argument2) => argument1.ToString().PadLeft(argument2, '0');

    public partial string FormatReal(float value, int fPartLength, bool hideZeroes, bool hideDot)
    {
        var format = "0." + new string(hideZeroes ? '#' : '0', fPartLength);
        var result = value.ToString(format, CultureInfo.InvariantCulture);
        if (hideDot && result.EndsWith(".")) result = result.TrimEnd('.');
        return result;
    }

    public partial string ToUpperCase(string textToChange) => textToChange?.ToUpperInvariant() ?? string.Empty;

    public partial string ToLowerCase(string textToChange) => textToChange?.ToLowerInvariant() ?? string.Empty;

    public partial string CloseStyleTags(string @string)
    {
        if (string.IsNullOrEmpty(@string)) return string.Empty;
        var openCount = @string.Split(["$<"], StringSplitOptions.None).Length - 1;
        var closeCount = @string.Split(["$>"], StringSplitOptions.None).Length - 1;
        var missing = openCount - closeCount;
        if (missing > 0) @string += string.Concat(Enumerable.Repeat("$>", missing));
        return @string;
    }

    public partial bool CompareWithoutFormat(string text1, string text2, bool isCaseSensitive)
    {
        var t1 = StripFormatting(text1);
        var t2 = StripFormatting(text2);
        return string.Equals(t1, t2, isCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
    }

    public partial bool Find(string textToFind, string textToSearchIn, bool isFormatSensitive, bool isCaseSensitive)
    {
        if (string.IsNullOrEmpty(textToFind) || string.IsNullOrEmpty(textToSearchIn)) return false;

        var target = isFormatSensitive ? textToSearchIn : StripFormatting(textToSearchIn);
        var query = isFormatSensitive ? textToFind : StripFormatting(textToFind);

        return target.IndexOf(query, isCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public partial bool EndsWith(string textToFind, string textToSearchIn) => EndsWith(textToFind, textToSearchIn, true, true);

    public partial bool EndsWith(string textToFind, string textToSearchIn, bool isFormatSensitive, bool isCaseSensitive)
    {
        string target = isFormatSensitive ? textToSearchIn : StripFormatting(textToSearchIn);
        string query = isFormatSensitive ? textToFind : StripFormatting(textToFind);
        return target?.EndsWith(query, isCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public partial bool StartsWith(string textToFind, string textToSearchIn) => StartsWith(textToFind, textToSearchIn, true, true);

    public partial bool StartsWith(string textToFind, string textToSearchIn, bool isFormatSensitive, bool isCaseSensitive)
    {
        var target = isFormatSensitive ? textToSearchIn : StripFormatting(textToSearchIn);
        var query = isFormatSensitive ? textToFind : StripFormatting(textToFind);
        return target?.StartsWith(query, isCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public partial string Compose(string argument1) => argument1;
    public partial string Compose(string argument1, string argument2) => argument1?.Replace("%1", argument2) ?? string.Empty;
    public partial string Compose(string argument1, string argument2, string argument3) => Compose(argument1, argument2).Replace("%2", argument3);
    public partial string Compose(string argument1, string argument2, string argument3, string argument4) => Compose(argument1, argument2, argument3).Replace("%3", argument4);
    public partial string Compose(string argument1, string argument2, string argument3, string argument4, string argument5) => Compose(argument1, argument2, argument3, argument4).Replace("%4", argument5);
    public partial string Compose(string argument1, string argument2, string argument3, string argument4, string argument5, string argument6) => Compose(argument1, argument2, argument3, argument4, argument5).Replace("%5", argument6);

    public partial string MLEncode(string argument1) => WebUtility.HtmlEncode(argument1);
    public partial string URLEncode(string argument1) => Uri.EscapeDataString(argument1);

    public partial string StripFormatting(string argument1)
    {
        if (string.IsNullOrEmpty(argument1)) return string.Empty;
        return Regex.Replace(argument1, @"\$(?:(\$)|[0-9a-fA-F]{2,3}|[lhLH]\[.*?\]|[lhLH]\[|.)", "");
    }

    public partial string[] Split(string separators, string text) =>
        text?.Split(separators.ToCharArray(), StringSplitOptions.None) ?? [];

    public partial string Join(string separator, string[] texts) => string.Join(separator, texts);

    public partial string Trim(string argument1) => argument1?.Trim() ?? string.Empty;

    public partial string ReplaceChars(string argument1, string argument2, string argument3)
    {
        if (string.IsNullOrEmpty(argument1) || string.IsNullOrEmpty(argument2) || string.IsNullOrEmpty(argument3)) return argument1 ?? string.Empty;
        var chars = argument1.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            int index = argument2.IndexOf(chars[i]);
            if (index >= 0 && index < argument3.Length)
            {
                chars[i] = argument3[index];
            }
        }
        return new string(chars);
    }

    public partial string Replace(string text, string toReplace, string replacement) =>
        text?.Replace(toReplace, replacement) ?? string.Empty;

    private RegexOptions GetRegexOptions(string flags)
    {
        RegexOptions options = RegexOptions.None;
        if (flags.Contains("i")) options |= RegexOptions.IgnoreCase;
        if (flags.Contains("m")) options |= RegexOptions.Multiline;
        return options;
    }

    public partial string[] RegexFind(string pattern, string text, string flags)
    {
        if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(text)) return [];
        var matches = Regex.Matches(text, pattern, GetRegexOptions(flags));
        return matches.Cast<Match>().Select(m => m.Value).ToArray();
    }

    public partial string[] RegexMatch(string pattern, string text, string flags)
    {
        if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(text)) return [];
        var match = Regex.Match(text, pattern, GetRegexOptions(flags));
        if (!match.Success) return [];

        var result = new List<string> { match.Value };
        for (int i = 1; i < match.Groups.Count; i++) result.Add(match.Groups[i].Value);
        return result.ToArray();
    }

    public partial string RegexReplace(string pattern, string text, string flags, string replacement)
    {
        if (string.IsNullOrEmpty(pattern) || text == null) return text ?? string.Empty;
        var regex = new Regex(pattern, GetRegexOptions(flags));
        return flags.Contains("g") ? regex.Replace(text, replacement) : regex.Replace(text, replacement, 1);
    }

    public partial string GetTranslatedText(string text)
    {
        return text;
    }
}
