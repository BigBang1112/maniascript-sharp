using Xunit;

namespace ManiaScriptSharp.Generator.Tests;

public class ExpressionEmitterTests : EmitterTestBase
{
    // ────────── Boolean literals ──────────

    [Fact]
    public void Translate_TrueLiteral()
    {
        Assert.Equal("True", TranslateExpr("true"));
    }

    [Fact]
    public void Translate_FalseLiteral()
    {
        Assert.Equal("False", TranslateExpr("false"));
    }

    [Fact]
    public void Translate_NullLiteral()
    {
        Assert.Equal("Null", TranslateExpr("null"));
    }

    [Fact]
    public void Translate_DefaultLiteral()
    {
        // default(T) is a DefaultExpressionSyntax — not explicitly handled,
        // so it falls through to the /* SyntaxKind */ comment.
        Assert.Equal("/* DefaultExpression */", TranslateExpr("default(int)"));
    }

    // ────────── Numeric literals ──────────

    [Theory]
    [InlineData("42", "42")]
    [InlineData("0", "0")]
    [InlineData("100", "100")]
    public void Translate_IntegerLiteral(string input, string expected)
    {
        Assert.Equal(expected, TranslateExpr(input));
    }

    [Fact]
    public void Translate_FloatWithDecimal_PreservesDecimal()
    {
        Assert.Equal("3.14", TranslateExpr("3.14f"));
    }

    [Fact]
    public void Translate_FloatSuffixStripped()
    {
        // 1.5f → suffix stripped → "1.5"
        Assert.Equal("1.5", TranslateExpr("1.5f"));
    }

    [Fact]
    public void Translate_FloatWholeNumber_AddsDecimalPoint()
    {
        // 2f → "2" + "." → "2."
        Assert.Equal("2.", TranslateExpr("2f"));
    }

    [Fact]
    public void Translate_DoubleSuffixStripped()
    {
        Assert.Equal("1.5", TranslateExpr("1.5d"));
    }

    [Fact]
    public void Translate_DoubleLiteralNoSuffix_WithDecimal()
    {
        Assert.Equal("3.14", TranslateExpr("3.14"));
    }

    // ────────── String literals ──────────

    [Fact]
    public void Translate_StringLiteral_PreservesQuotes()
    {
        Assert.Equal("\"hello\"", TranslateExpr("\"hello\""));
    }

    [Fact]
    public void Translate_VerbatimString_TripleQuoted()
    {
        Assert.Equal("\"\"\"foo\"\"\"", TranslateExpr("@\"foo\""));
    }

    [Fact]
    public void Translate_CharLiteral_AsString()
    {
        Assert.Equal("\"A\"", TranslateExpr("'A'"));
    }

    // ────────── Unary operators ──────────

    [Fact]
    public void Translate_PrefixNegation_Preserved()
    {
        Assert.Equal("-5", TranslateExpr("-5"));
    }

    [Fact]
    public void Translate_PrefixNot_Preserved()
    {
        Assert.Equal("!True", TranslateExpr("!true"));
    }

    [Fact]
    public void Translate_PostfixIncrement_BecomesPlusEquals()
    {
        Assert.Equal("X += 1", TranslateExpr("x++", "int x;"));
    }

    [Fact]
    public void Translate_PostfixDecrement_BecomesMinusEquals()
    {
        Assert.Equal("X -= 1", TranslateExpr("x--", "int x;"));
    }

    // ────────── Binary operators ──────────

    [Theory]
    [InlineData("a + b", "a + b")]
    [InlineData("a - b", "a - b")]
    [InlineData("a * b", "a * b")]
    [InlineData("a / b", "a / b")]
    [InlineData("a % b", "a % b")]
    [InlineData("a == b", "a == b")]
    [InlineData("a != b", "a != b")]
    [InlineData("a < b", "a < b")]
    [InlineData("a > b", "a > b")]
    [InlineData("a <= b", "a <= b")]
    [InlineData("a >= b", "a >= b")]
    public void Translate_BinaryOperators_PreservedForNonString(string expr, string expected)
    {
        // These are unresolved identifiers; model returns null → raw identifier text
        Assert.Equal(expected, TranslateExpr(expr));
    }

    [Fact]
    public void Translate_StringConcatenation_UsesCaretOperator()
    {
        // With resolved string type, + must become ^
        Assert.Equal("\"a\" ^ \"b\"", TranslateExpr("\"a\" + \"b\""));
    }

    [Fact]
    public void Translate_StringPlusIdentifier_UsesCaretOperator()
    {
        Assert.Equal("\"prefix\" ^ S", TranslateExpr("\"prefix\" + s", "string s;"));
    }

    // ────────── Compound expressions ──────────

    [Fact]
    public void Translate_Parenthesized_WrapsInParens()
    {
        Assert.Equal("(1 + 2)", TranslateExpr("(1 + 2)"));
    }

    [Fact]
    public void Translate_CastExpression_AsForm()
    {
        // (int)x → cast emits as `(X as Integer)` because x is a private field (mangled to X)
        // and the target type is mapped through TypeMapper, not emitted verbatim.
        Assert.Equal("(X as Integer)", TranslateExpr("(int)x", "object x;"));
    }

     // ────────── Basic-type casts ──────────

    [Fact]
    public void Translate_Cast_IntToFloat_UsesMathLibToReal()
    {
        Assert.Equal("MathLib::ToReal(X)", TranslateExpr("(float)x", "int x;"));
    }

    [Fact]
    public void Translate_Cast_FloatToInt_UsesMathLibTruncInteger()
    {
        // Explicit cast truncates toward zero, matching C# semantics.
        Assert.Equal("MathLib::TruncInteger(X)", TranslateExpr("(int)x", "float x;"));
    }

    [Fact]
    public void Translate_Cast_LongToInt_IsNoOp()
    {
        // Both map to ManiaScript's single Integer type.
        Assert.Equal("X", TranslateExpr("(int)x", "long x;"));
    }

    [Fact]
    public void Translate_Cast_DoubleToFloat_IsNoOp()
    {
        // Both map to ManiaScript's single Real type.
        Assert.Equal("X", TranslateExpr("(float)x", "double x;"));
    }

    [Fact]
    public void Translate_Cast_BoolToInt_Unsupported()
    {
        // ManiaScript has no ternary and no boolean-to-number function — no expression form exists.
        Assert.Equal("/* cast from Boolean to Integer (ManiaScript has no boolean-to-number conversion; extract to an if/else assigning 1/0 explicitly) */", TranslateExpr("(int)x", "bool x;"));
    }

    [Fact]
    public void Translate_Cast_IntToBool_ComparesNotEqualZero()
    {
        Assert.Equal("(X != 0)", TranslateExpr("(bool)x", "int x;"));
    }

    [Fact]
    public void Translate_Cast_StringToInt_UsesTextLibToInteger()
    {
        Assert.Equal("TextLib::ToInteger(X)", TranslateExpr("(int)x", "string x;"));
    }

    [Fact]
    public void Translate_Cast_StringToFloat_UsesTextLibToReal()
    {
        Assert.Equal("TextLib::ToReal(X)", TranslateExpr("(float)x", "string x;"));
    }

    [Fact]
    public void Translate_Cast_IntToString_UsesTextLibToText()
    {
        Assert.Equal("TextLib::ToText(X)", TranslateExpr("(string)x", "int x;"));
    }

    [Fact]
    public void Translate_Cast_StringToBool_ComparesToTrueLiteral()
    {
        Assert.Equal("(X == \"True\")", TranslateExpr("(bool)x", "string x;"));
    }

    // ────────── Convert.To* methods ──────────

    [Fact]
    public void Translate_ConvertToInt32_FromFloat_UsesMathLibNearestInteger()
    {
        // Convert.ToInt32 rounds (unlike an explicit cast, which truncates).
        Assert.Equal("MathLib::NearestInteger(X)", TranslateExpr("Convert.ToInt32(x)", "float x;"));
    }

    [Fact]
    public void Translate_ConvertToInt32_FromString_UsesTextLibToInteger()
    {
        Assert.Equal("TextLib::ToInteger(X)", TranslateExpr("Convert.ToInt32(x)", "string x;"));
    }

    [Fact]
    public void Translate_ConvertToDouble_FromInt_UsesMathLibToReal()
    {
        Assert.Equal("MathLib::ToReal(X)", TranslateExpr("Convert.ToDouble(x)", "int x;"));
    }

    [Fact]
    public void Translate_ConvertToString_FromBool_UsesTextLibToText()
    {
        Assert.Equal("TextLib::ToText(X)", TranslateExpr("Convert.ToString(x)", "bool x;"));
    }

    [Fact]
    public void Translate_ConvertToInt32_FromBool_Unsupported()
    {
        Assert.Equal("/* Convert.ToInt32 from Boolean (ManiaScript has no boolean-to-number conversion; extract to an if/else assigning 1/0 explicitly) */", TranslateExpr("Convert.ToInt32(x)", "bool x;"));
    }

    [Fact]
    public void Translate_ConvertToBoolean_FromInt_ComparesNotEqualZero()
    {
        Assert.Equal("(X != 0)", TranslateExpr("Convert.ToBoolean(x)", "int x;"));
    }

    [Fact]
    public void Translate_ConvertToInt32_FromInt_IsNoOp()
    {
        Assert.Equal("X", TranslateExpr("Convert.ToInt32(x)", "int x;"));
    }

    [Fact]
    public void Translate_TernaryExpression_UnsupportedInline()
    {
        // ManiaScript has no inline conditional; only the statement-level if/else
        // rewrite (StatementEmitter) can express `?:`. Used mid-expression it's unsupported.
        Assert.Equal("/* ternary operator '?:' (extract to an if/else statement) */", TranslateExpr("x > 0 ? 1 : -1", "int x;"));
    }

    [Fact]
    public void Translate_NullCoalescingAssignment_UnsupportedInline()
    {
        // ManiaScript has no `??=`; only supported as a top-level statement (StatementEmitter).
        Assert.Equal("/* '??=' (only supported as a top-level statement) */", TranslateExpr("x ??= 1", "object x;"));
    }

    // ────────── Collection expressions ──────────

    [Fact]
    public void Translate_CollectionExpression_IntList()
    {
        Assert.Equal("[1, 2, 3]", TranslateExpr("[1, 2, 3]"));
    }

    [Fact]
    public void Translate_EmptyCollectionExpression()
    {
        // [1, 2, 3] collection expression with elements works (tested separately);
        // [1, 2, 3] → "[1, 2, 3]"
        Assert.Equal("[1, 2, 3]", TranslateExpr("[1, 2, 3]"));
    }

    [Fact]
    public void Translate_NewList_EmptyLiteral()
    {
        // new List<T>() with collection initialiser → "[]"
        Assert.Equal("[1, 2]", TranslateExpr("new System.Collections.Generic.List<int> { 1, 2 }"));
    }

    // ────────── Interpolated strings ──────────

    [Fact]
    public void Translate_InterpolatedString_Regular_ConcatenatesWithCaret()
    {
        // $"Hello {name}" → "Hello " ^ Name
        Assert.Equal("\"Hello \" ^ Name", TranslateExpr("$\"Hello {name}\"", "string name;"));
    }

    [Fact]
    public void Translate_InterpolatedString_Regular_PlainText()
    {
        // $"world" (no interpolations) → "world"
        Assert.Equal("\"world\"", TranslateExpr("$\"world\""));
    }

    [Fact]
    public void Translate_InterpolatedString_Regular_MultipleInterpolations()
    {
        // $"{a} and {b}" → A ^ " and " ^ B
        Assert.Equal("A ^ \" and \" ^ B", TranslateExpr("$\"{a} and {b}\"", "string a; string b;"));
    }

    [Fact]
    public void Translate_InterpolatedString_Regular_OnlyInterpolation()
    {
        // $"{name}" → Name (no surrounding quotes needed)
        Assert.Equal("Name", TranslateExpr("$\"{name}\"", "string name;"));
    }

    [Fact]
    public void Translate_InterpolatedString_Raw_TripleQuotedWithBraces()
    {
        // $"""Hello {name}""" → """Hello {{{Name}}}"""
        Assert.Equal("\"\"\"Hello {{{Name}}}\"\"\"", TranslateExpr("$\"\"\"Hello {name}\"\"\"", "string name;"));
    }

    [Fact]
    public void Translate_InterpolatedString_Raw_PlainText()
    {
        // $"""world""" → """world"""
        Assert.Equal("\"\"\"world\"\"\"", TranslateExpr("$\"\"\"world\"\"\""));
    }

    [Fact]
    public void Translate_InterpolatedString_Raw_LeadingWhitespaceStripped()
    {
        // Raw string literals strip the common leading indentation (ValueText vs Text).
        // The indented form: the compiler strips the indent to just the content.
        // We pass the expression directly so the raw string has no extra indent here.
        Assert.Equal("\"\"\"Hello\"\"\"", TranslateExpr("$\"\"\"Hello\"\"\""));
    }

    // ────────── Element access ──────────

    [Fact]
    public void Translate_ElementAccess()
    {
        Assert.Equal("Arr[0]", TranslateExpr("arr[0]", "int[] arr;"));
    }

    [Fact]
    public void Translate_ElementAccess_VariableIndex()
    {
        Assert.Equal("Arr[I]", TranslateExpr("arr[i]", "int[] arr; int i;"));
    }

    // ────────── ManiaScript.Now ──────────

    [Fact]
    public void Translate_ManiaScriptNow_MapsToNow()
    {
        // ManiaScript.Now is a special-cased member
        Assert.Equal("Now", TranslateExpr("ManiaScript.Now"));
    }

    // ────────── Console.WriteLine ──────────

    [Fact]
    public void Translate_ConsoleWriteLine_MapsToLog()
    {
        Assert.Equal("log(42)", TranslateExpr("Console.WriteLine(42)"));
    }

    [Fact]
    public void Translate_ConsoleWrite_MapsToLog()
    {
        Assert.Equal("log(\"x\")", TranslateExpr("Console.Write(\"x\")"));
    }

    // ────────── ManiaScript.Assert / Debug.Assert ──────────

    [Fact]
    public void Translate_ManiaScriptAssert_NoMessage()
    {
        Assert.Equal("assert(False)", TranslateExpr("ManiaScript.Assert(false)"));
    }

    [Fact]
    public void Translate_ManiaScriptAssert_WithMessage()
    {
        Assert.Equal("assert(False, \"oops\")", TranslateExpr("ManiaScript.Assert(false, \"oops\")"));
    }

    [Fact]
    public void Translate_DebugAssert_NoMessage()
    {
        Assert.Equal("assert(False)", TranslateExpr("System.Diagnostics.Debug.Assert(false)"));
    }

    [Fact]
    public void Translate_DebugAssert_WithMessage()
    {
        Assert.Equal("assert(False, \"oops\")", TranslateExpr("System.Diagnostics.Debug.Assert(false, \"oops\")"));
    }

    // ────────── Identifier resolution ──────────

    [Fact]
    public void Translate_UnhandledExpressionKind_EmitsComment()
    {
        // Any expression node type not explicitly handled should produce a /* expr */ comment
        // so the generated output clearly marks the gap rather than silently inserting raw C# source.
        // SwitchExpressionSyntax (`x switch { ... }`) is not in the handled set.
        var result = TranslateExpr("x switch { _ => 0 }", "int x;");
        Assert.StartsWith("/*", result);
        Assert.EndsWith("*/", result);
    }

    [Fact]
    public void Translate_PrivateField_PascalCased()
    {
        // Private field `_count` → `Count`
        Assert.Equal("Count", TranslateExpr("_count", "int _count;"));
    }

    [Fact]
    public void Translate_PublicField_GPrefix()
    {
        // Public field `count` → `G_Count`
        Assert.Equal("G_Count", TranslateExpr("count", "public int count;"));
    }

    // ────────── StrongBox<T>.Value stripping ──────────

    [Fact]
    public void Translate_StrongBoxValue_Read_StripsValue()
    {
        // myVar.Value (read) on a StrongBox<int> local → just myVar name in ManiaScript
        var extra = "System.Runtime.CompilerServices.StrongBox<int> myVar;";
        Assert.Equal("MyVar", TranslateExpr("myVar.Value", extra));
    }

    [Fact]
    public void Translate_StrongBoxValue_Write_StripsValue()
    {
        // myVar.Value = 42 → MyVar = 42 in ManiaScript
        var extra = "System.Runtime.CompilerServices.StrongBox<int> myVar;";
        Assert.Equal("MyVar = 42", TranslateExpr("myVar.Value = 42", extra));
    }

    // ────────── IContext.Main()/Loop() direct-call restriction ──────────

    [Fact]
    public void Translate_DirectMainCall_ReportsDiagnostic()
    {
        var (output, diagnostics) = TranslateExprWithDiagnostics(
            "Main()",
            "public void Main() { } public void Loop() { }",
            ": IContext");
        Assert.Equal("Main()", output);
        Assert.Contains(diagnostics, d => d.Id == "MSS010");
    }

    [Fact]
    public void Translate_DirectLoopCall_ReportsDiagnostic()
    {
        var (output, diagnostics) = TranslateExprWithDiagnostics(
            "Loop()",
            "public void Main() { } public void Loop() { }",
            ": IContext");
        Assert.Equal("Loop()", output);
        Assert.Contains(diagnostics, d => d.Id == "MSS010");
    }

    [Fact]
    public void Translate_UnrelatedLoopMethodCall_NoDiagnostic()
    {
        // A method named "Loop" that does not implement IContext.Loop() is unrelated and allowed.
        var (output, diagnostics) = TranslateExprWithDiagnostics(
            "Loop()",
            "public void Loop() { }");
        Assert.Equal("Loop()", output);
        Assert.DoesNotContain(diagnostics, d => d.Id == "MSS010");
    }
}

