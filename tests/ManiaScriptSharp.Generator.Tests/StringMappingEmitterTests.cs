using Xunit;

namespace ManiaScriptSharp.Generator.Tests;

/// <summary>
/// Tests that C# string instance/static methods and int/float Parse are translated to
/// the correct TextLib:: expressions by ExpressionEmitter.
/// </summary>
public class StringMappingEmitterTests : EmitterTestBase
{
    // ────────── string.Length ──────────

    [Fact]
    public void Translate_StringLength_MapsToTextLibLength()
    {
        Assert.Equal("TextLib::Length(S)", TranslateExpr("s.Length", "string s;"));
    }

    [Fact]
    public void Translate_StringLength_OnLiteral()
    {
        Assert.Equal("TextLib::Length(\"hello\")", TranslateExpr("\"hello\".Length"));
    }

    // ────────── Case conversion ──────────

    [Fact]
    public void Translate_ToUpper_MapsToToUpperCase()
    {
        Assert.Equal("TextLib::ToUpperCase(S)", TranslateExpr("s.ToUpper()", "string s;"));
    }

    [Fact]
    public void Translate_ToUpperInvariant_MapsToToUpperCase()
    {
        Assert.Equal("TextLib::ToUpperCase(S)", TranslateExpr("s.ToUpperInvariant()", "string s;"));
    }

    [Fact]
    public void Translate_ToLower_MapsToToLowerCase()
    {
        Assert.Equal("TextLib::ToLowerCase(S)", TranslateExpr("s.ToLower()", "string s;"));
    }

    [Fact]
    public void Translate_ToLowerInvariant_MapsToToLowerCase()
    {
        Assert.Equal("TextLib::ToLowerCase(S)", TranslateExpr("s.ToLowerInvariant()", "string s;"));
    }

    // ────────── Trim ──────────

    [Fact]
    public void Translate_Trim_MapsToTextLibTrim()
    {
        Assert.Equal("TextLib::Trim(S)", TranslateExpr("s.Trim()", "string s;"));
    }

    // ────────── Substring ──────────

    [Fact]
    public void Translate_Substring_TwoArgs_MapsToSubString()
    {
        Assert.Equal("TextLib::SubString(S, 2, 3)", TranslateExpr("s.Substring(2, 3)", "string s;"));
    }

    [Fact]
    public void Translate_Substring_OneArg_UsesLengthAsUpperBound()
    {
        // Single-arg form: SubString(s, start, Length(s)) — receiver emitted twice intentionally.
        Assert.Equal("TextLib::SubString(S, 1, TextLib::Length(S))", TranslateExpr("s.Substring(1)", "string s;"));
    }

    // ────────── Contains ──────────

    [Fact]
    public void Translate_Contains_MapsToFind()
    {
        // Find(needle, haystack, formatSensitive=True, caseSensitive=True)
        Assert.Equal("TextLib::Find(\"x\", S, True, True)", TranslateExpr("s.Contains(\"x\")", "string s;"));
    }

    // ────────── StartsWith / EndsWith ──────────

    [Fact]
    public void Translate_StartsWith_MapsToTextLibStartsWith()
    {
        Assert.Equal("TextLib::StartsWith(\"pre\", S)", TranslateExpr("s.StartsWith(\"pre\")", "string s;"));
    }

    [Fact]
    public void Translate_EndsWith_MapsToTextLibEndsWith()
    {
        Assert.Equal("TextLib::EndsWith(\"suf\", S)", TranslateExpr("s.EndsWith(\"suf\")", "string s;"));
    }

    // ────────── Replace ──────────

    [Fact]
    public void Translate_Replace_MapsToTextLibReplace()
    {
        Assert.Equal("TextLib::Replace(S, \"old\", \"new\")", TranslateExpr("s.Replace(\"old\", \"new\")", "string s;"));
    }

    // ────────── Split ──────────

    [Fact]
    public void Translate_Split_StringSep_MapsToTextLibSplit()
    {
        Assert.Equal("TextLib::Split(\",\", S)", TranslateExpr("s.Split(\",\")", "string s;"));
    }

    [Fact]
    public void Translate_Split_CharSep_MapsToTextLibSplit()
    {
        // Char literal 'x' is emitted as "x" by TranslateLiteral, so separator becomes a string.
        Assert.Equal("TextLib::Split(\",\", S)", TranslateExpr("s.Split(',')", "string s;"));
    }

    // ────────── Static: string.Join ──────────

    [Fact]
    public void Translate_StringJoin_MapsToTextLibJoin()
    {
        Assert.Equal("TextLib::Join(\",\", Arr)", TranslateExpr("string.Join(\",\", arr)", "string[] arr;"));
    }

    // ────────── Static: string.IsNullOrEmpty ──────────

    [Fact]
    public void Translate_IsNullOrEmpty_MapsToEmptyStringCheck()
    {
        Assert.Equal("(S == \"\")", TranslateExpr("string.IsNullOrEmpty(s)", "string s;"));
    }

    [Fact]
    public void Translate_IsNullOrWhiteSpace_MapsToEmptyStringCheck()
    {
        Assert.Equal("(S == \"\")", TranslateExpr("string.IsNullOrWhiteSpace(s)", "string s;"));
    }

    // ────────── Static: string.Concat ──────────

    [Fact]
    public void Translate_StringConcat_TwoArgs_UsesCaretOperator()
    {
        Assert.Equal("A ^ B", TranslateExpr("string.Concat(a, b)", "string a; string b;"));
    }

    [Fact]
    public void Translate_StringConcat_ThreeArgs_UsesCaretOperator()
    {
        Assert.Equal("A ^ B ^ C", TranslateExpr("string.Concat(a, b, c)", "string a; string b; string c;"));
    }

    // ────────── int.Parse / float.Parse ──────────

    [Fact]
    public void Translate_IntParse_MapsToToInteger()
    {
        Assert.Equal("TextLib::ToInteger(S)", TranslateExpr("int.Parse(s)", "string s;"));
    }

    [Fact]
    public void Translate_FloatParse_MapsToToReal()
    {
        Assert.Equal("TextLib::ToReal(S)", TranslateExpr("float.Parse(s)", "string s;"));
    }
}
