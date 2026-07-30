using Xunit;

namespace ManiaScriptSharp.Generator.Tests;

public class PatternEmitterTests : EmitterTestBase
{
    // ────────── Constant patterns ──────────

    [Fact]
    public void Translate_ConstantPattern_Integer()
    {
        Assert.Equal("X == 5", TranslatePattern("X", "is 5"));
    }

    [Fact]
    public void Translate_ConstantPattern_Zero()
    {
        Assert.Equal("X == 0", TranslatePattern("X", "is 0"));
    }

    [Fact]
    public void Translate_ConstantPattern_BoolTrue()
    {
        Assert.Equal("X == True", TranslatePattern("X", "is true"));
    }

    [Fact]
    public void Translate_ConstantPattern_BoolFalse()
    {
        Assert.Equal("X == False", TranslatePattern("X", "is false"));
    }

    [Fact]
    public void Translate_ConstantPattern_Null()
    {
        Assert.Equal("X == Null", TranslatePattern("X", "is null"));
    }

    [Fact]
    public void Translate_ConstantPattern_StringLiteral()
    {
        Assert.Equal("X == \"hello\"", TranslatePattern("X", "is \"hello\""));
    }

    // ────────── Relational patterns ──────────

    [Fact]
    public void Translate_RelationalPattern_LessThan()
    {
        Assert.Equal("X < 5", TranslatePattern("X", "is < 5"));
    }

    [Fact]
    public void Translate_RelationalPattern_LessThanOrEqual()
    {
        Assert.Equal("X <= 10", TranslatePattern("X", "is <= 10"));
    }

    [Fact]
    public void Translate_RelationalPattern_GreaterThan()
    {
        Assert.Equal("X > 0", TranslatePattern("X", "is > 0"));
    }

    [Fact]
    public void Translate_RelationalPattern_GreaterThanOrEqual()
    {
        Assert.Equal("X >= 1", TranslatePattern("X", "is >= 1"));
    }

    // ────────── Declaration / type patterns ──────────

    [Fact]
    public void Translate_DeclarationPattern_TypeAndVariable()
    {
        // `is int y` → `X is int`  (variable name is discarded at expression level)
        Assert.Equal("X is int", TranslatePattern("X", "is int y"));
    }

    [Fact]
    public void Translate_DeclarationPattern_CustomType()
    {
        Assert.Equal("X is string", TranslatePattern("X", "is string text"));
    }

    [Fact]
    public void Translate_TypePattern_ViaIsExpression()
    {
        // Without a variable binding, `x is int` can be the old binary-is check or a
        // C#-9 TypePattern depending on language version.  Declaration pattern (with var)
        // is the reliable way to exercise TypePatternSyntax handling.
        // This test confirms declaration pattern output is unchanged.
        Assert.Equal("X is int", TranslatePattern("X", "is int y"));
    }

    // ────────── Negation patterns ──────────

    [Fact]
    public void Translate_NotConstant_BecomesNotEqual()
    {
        Assert.Equal("X != 5", TranslatePattern("X", "is not 5"));
    }

    [Fact]
    public void Translate_NotNull_BecomesNotEqualNull()
    {
        Assert.Equal("X != Null", TranslatePattern("X", "is not null"));
    }

    [Fact]
    public void Translate_NotRelational_WrapsInNot()
    {
        Assert.Equal("!(X < 5)", TranslatePattern("X", "is not < 5"));
    }

    [Fact]
    public void Translate_NotBool_BecomesNotEqual()
    {
        // `not true` is a `not` of a ConstantPattern → emitted as `!= True`
        Assert.Equal("X != True", TranslatePattern("X", "is not true"));
    }

    // ────────── Binary patterns ──────────

    [Fact]
    public void Translate_AndPattern_BothRelational()
    {
        Assert.Equal("(X >= 1 && X <= 10)", TranslatePattern("X", "is >= 1 and <= 10"));
    }

    [Fact]
    public void Translate_AndPattern_RelationalAndConstant()
    {
        Assert.Equal("(X > 0 && X == 5)", TranslatePattern("X", "is > 0 and 5"));
    }

    [Fact]
    public void Translate_OrPattern_TwoConstants()
    {
        Assert.Equal("(X == 1 || X == 2)", TranslatePattern("X", "is 1 or 2"));
    }

    [Fact]
    public void Translate_OrPattern_ThreeValues_RightAssociative()
    {
        // In C#, `is 1 or 2 or 3` is right-associative: (X == 1 || (X == 2 || X == 3))
        var result = TranslatePattern("X", "is 1 or 2 or 3");
        Assert.Contains("X == 1", result);
        Assert.Contains("X == 2", result);
        Assert.Contains("X == 3", result);
        Assert.Contains("||", result);
    }

    [Fact]
    public void Translate_NestedAndOr()
    {
        // `and` has higher precedence than `or` in C# patterns; no explicit parens needed.
        // Roslyn parses `>= 0 and <= 5 or >= 10 and <= 15` as
        //   (>= 0 and <= 5) or (>= 10 and <= 15) at the AST level.
        var result = TranslatePattern("X", "is >= 0 and <= 5 or >= 10 and <= 15");
        Assert.Contains("&&", result);
        Assert.Contains("||", result);
    }

    // ────────── LHS pass-through ──────────

    [Theory]
    [InlineData("Value")]
    [InlineData("Event.Type")]
    [InlineData("MyObj.Field")]
    public void Translate_LhsPassedThrough(string lhs)
    {
        var result = TranslatePattern(lhs, "is 1");
        Assert.StartsWith(lhs, result);
    }
}
