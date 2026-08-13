using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp;

public sealed partial class TextLib
{
    public partial float ToReal(string _Text)
    {
        return float.TryParse(_Text, NumberStyles.Any, CultureInfo.InvariantCulture, out float result) ? result : -1f;
    }

    public partial int ToInteger(string _Text)
    {
        return int.TryParse(_Text, out int result) ? result : -1;
    }

    public partial Vec3 ToColor(string _Text)
    {
        if (string.IsNullOrWhiteSpace(_Text)) return default!;
        _Text = _Text.TrimStart('#');
        if (_Text.Length == 6 && int.TryParse(_Text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex))
        {
            return new Vec3(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f);
        }
        return default!;
    }

    public partial string SubString(string _Text, int _Start, int _Length)
    {
        if (string.IsNullOrEmpty(_Text) || _Start >= _Text.Length || _Length <= 0 || _Start < 0) return string.Empty;
        if (_Start + _Length > _Text.Length) _Length = _Text.Length - _Start;
        return _Text.Substring(_Start, _Length);
    }

    public partial string SubText(string _Text, int _Start, int _Length)
    {
        return SubString(_Text, _Start, _Length);
    }

    public partial int Length(string _Text)
    {
        return string.IsNullOrEmpty(_Text) ? 0 : _Text.Length;
    }

    public partial string ToText(int _Integer) => _Integer.ToString(CultureInfo.InvariantCulture);

    public partial string ToText(float _Real) => _Real.ToString(CultureInfo.InvariantCulture);

    public partial string ToText(bool _Boolean) => _Boolean ? "True" : "False";

    public partial string ToText(Int3 _Int3) => _Int3.ToString() ?? string.Empty;

    public partial string ToText(Vec3 _Vec3) => _Vec3.ToString() ?? string.Empty;

    public partial string TimeToText(int _Time, bool _IncludeCentiSeconds)
    {
        var ts = TimeSpan.FromMilliseconds(_Time);
        var baseTime = $"{(int)Math.Floor(ts.TotalMinutes):D2}:{ts.Seconds:D2}";
        return _IncludeCentiSeconds ? $"{baseTime}:{ts.Milliseconds / 10:D2}" : baseTime;
    }

    public partial string TimeToText(int _Time) => TimeToText(_Time, false);

    public partial string ColorToText(Vec3 _Color) => _Color.ToString() ?? string.Empty;

    public partial string FormatInteger(int Argument1, int Argument2) => Argument1.ToString().PadLeft(Argument2, '0');

    public partial string FormatReal(float _Value, int _FPartLength, bool _HideZeroes, bool _HideDot)
    {
        var format = "0." + new string(_HideZeroes ? '#' : '0', _FPartLength);
        var result = _Value.ToString(format, CultureInfo.InvariantCulture);
        if (_HideDot && result.EndsWith(".")) result = result.TrimEnd('.');
        return result;
    }

    public partial string ToUpperCase(string _TextToChange) => _TextToChange?.ToUpperInvariant() ?? string.Empty;

    public partial string ToLowerCase(string _TextToChange) => _TextToChange?.ToLowerInvariant() ?? string.Empty;

    public partial string CloseStyleTags(string _String)
    {
        if (string.IsNullOrEmpty(_String)) return string.Empty;
        var openCount = _String.Split(["$<"], StringSplitOptions.None).Length - 1;
        var closeCount = _String.Split(["$>"], StringSplitOptions.None).Length - 1;
        var missing = openCount - closeCount;
        if (missing > 0) _String += string.Concat(Enumerable.Repeat("$>", missing));
        return _String;
    }

    public partial bool CompareWithoutFormat(string _Text1, string _Text2, bool _IsCaseSensitive)
    {
        var t1 = StripFormatting(_Text1);
        var t2 = StripFormatting(_Text2);
        return string.Equals(t1, t2, _IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
    }

    public partial bool Find(string _TextToFind, string _TextToSearchIn, bool _IsFormatSensitive, bool _IsCaseSensitive)
    {
        if (string.IsNullOrEmpty(_TextToFind) || string.IsNullOrEmpty(_TextToSearchIn)) return false;

        var target = _IsFormatSensitive ? _TextToSearchIn : StripFormatting(_TextToSearchIn);
        var query = _IsFormatSensitive ? _TextToFind : StripFormatting(_TextToFind);

        return target.IndexOf(query, _IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public partial bool EndsWith(string _TextToFind, string _TextToSearchIn) => EndsWith(_TextToFind, _TextToSearchIn, true, true);

    public partial bool EndsWith(string _TextToFind, string _TextToSearchIn, bool _IsFormatSensitive, bool _IsCaseSensitive)
    {
        string target = _IsFormatSensitive ? _TextToSearchIn : StripFormatting(_TextToSearchIn);
        string query = _IsFormatSensitive ? _TextToFind : StripFormatting(_TextToFind);
        return target?.EndsWith(query, _IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public partial bool StartsWith(string _TextToFind, string _TextToSearchIn) => StartsWith(_TextToFind, _TextToSearchIn, true, true);

    public partial bool StartsWith(string _TextToFind, string _TextToSearchIn, bool _IsFormatSensitive, bool _IsCaseSensitive)
    {
        var target = _IsFormatSensitive ? _TextToSearchIn : StripFormatting(_TextToSearchIn);
        var query = _IsFormatSensitive ? _TextToFind : StripFormatting(_TextToFind);
        return target?.StartsWith(query, _IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public partial string Compose(string Argument1) => Argument1;
    public partial string Compose(string Argument1, string Argument2) => Argument1?.Replace("%1", Argument2) ?? string.Empty;
    public partial string Compose(string Argument1, string Argument2, string Argument3) => Compose(Argument1, Argument2).Replace("%2", Argument3);
    public partial string Compose(string Argument1, string Argument2, string Argument3, string Argument4) => Compose(Argument1, Argument2, Argument3).Replace("%3", Argument4);
    public partial string Compose(string Argument1, string Argument2, string Argument3, string Argument4, string Argument5) => Compose(Argument1, Argument2, Argument3, Argument4).Replace("%4", Argument5);
    public partial string Compose(string Argument1, string Argument2, string Argument3, string Argument4, string Argument5, string Argument6) => Compose(Argument1, Argument2, Argument3, Argument4, Argument5).Replace("%5", Argument6);

    public partial string MLEncode(string Argument1) => WebUtility.HtmlEncode(Argument1);
    public partial string URLEncode(string Argument1) => Uri.EscapeDataString(Argument1);

    public partial string StripFormatting(string Argument1)
    {
        if (string.IsNullOrEmpty(Argument1)) return string.Empty;
        return Regex.Replace(Argument1, @"\$(?:(\$)|[0-9a-fA-F]{2,3}|[lhLH]\[.*?\]|[lhLH]\[|.)", "");
    }

    public partial string[] Split(string _Separators, string _Text) =>
        _Text?.Split(_Separators.ToCharArray(), StringSplitOptions.None) ?? [];

    public partial string Join(string _Separator, string[] _Texts) => string.Join(_Separator, _Texts);

    public partial string Trim(string Argument1) => Argument1?.Trim() ?? string.Empty;

    public partial string ReplaceChars(string Argument1, string Argument2, string Argument3)
    {
        if (string.IsNullOrEmpty(Argument1) || string.IsNullOrEmpty(Argument2) || string.IsNullOrEmpty(Argument3)) return Argument1 ?? string.Empty;
        var chars = Argument1.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            int index = Argument2.IndexOf(chars[i]);
            if (index >= 0 && index < Argument3.Length)
            {
                chars[i] = Argument3[index];
            }
        }
        return new string(chars);
    }

    public partial string Replace(string _Text, string _ToReplace, string _Replacement) =>
        _Text?.Replace(_ToReplace, _Replacement) ?? string.Empty;

    private RegexOptions GetRegexOptions(string flags)
    {
        RegexOptions options = RegexOptions.None;
        if (flags.Contains("i")) options |= RegexOptions.IgnoreCase;
        if (flags.Contains("m")) options |= RegexOptions.Multiline;
        return options;
    }

    public partial string[] RegexFind(string _Pattern, string _Text, string _Flags)
    {
        if (string.IsNullOrEmpty(_Pattern) || string.IsNullOrEmpty(_Text)) return [];
        var matches = Regex.Matches(_Text, _Pattern, GetRegexOptions(_Flags));
        return matches.Cast<Match>().Select(m => m.Value).ToArray();
    }

    public partial string[] RegexMatch(string _Pattern, string _Text, string _Flags)
    {
        if (string.IsNullOrEmpty(_Pattern) || string.IsNullOrEmpty(_Text)) return [];
        var match = Regex.Match(_Text, _Pattern, GetRegexOptions(_Flags));
        if (!match.Success) return [];

        var result = new List<string> { match.Value };
        for (int i = 1; i < match.Groups.Count; i++) result.Add(match.Groups[i].Value);
        return result.ToArray();
    }

    public partial string RegexReplace(string _Pattern, string _Text, string _Flags, string _Replacement)
    {
        if (string.IsNullOrEmpty(_Pattern) || _Text == null) return _Text ?? string.Empty;
        var regex = new Regex(_Pattern, GetRegexOptions(_Flags));
        return _Flags.Contains("g") ? regex.Replace(_Text, _Replacement) : regex.Replace(_Text, _Replacement, 1);
    }

    public partial string GetTranslatedText(string _Text)
    {
        return _Text;
    }
}
