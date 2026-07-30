using ManiaScriptSharp.Generator.Emission;
using Xunit;

namespace ManiaScriptSharp.Generator.Tests;

public class LiteralEmitterTests
{
    private readonly LiteralEmitter _emitter = new();

    [Fact]
    public void Format_Null_ReturnsNull()
    {
        Assert.Equal("Null", _emitter.Format(null));
    }

    [Theory]
    [InlineData(true, "True")]
    [InlineData(false, "False")]
    public void Format_Bool_ReturnsManiaScriptBool(bool value, string expected)
    {
        Assert.Equal(expected, _emitter.Format(value));
    }

    [Theory]
    [InlineData("hello", "\"hello\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData("path\\to", "\"path\\\\to\"")]
    [InlineData("", "\"\"")]
    public void Format_String_EscapesAndQuotes(string value, string expected)
    {
        Assert.Equal(expected, _emitter.Format(value));
    }

    [Fact]
    public void Format_Char_WrapsInQuotes()
    {
        Assert.Equal("\"A\"", _emitter.Format('A'));
    }

    [Theory]
    [InlineData(42, "42")]
    [InlineData(0, "0")]
    [InlineData(-1, "-1")]
    public void Format_Integer_ReturnsPlainNumber(int value, string expected)
    {
        Assert.Equal(expected, _emitter.Format(value));
    }

    [Fact]
    public void Format_Float_EnsuresDecimalPoint()
    {
        // 1.0f should be "1" with a decimal
        var result = _emitter.Format(1.0f);
        Assert.Contains(".", result);
        Assert.Equal("1.", result.TrimEnd('0'));
    }

    [Fact]
    public void Format_Double_EnsuresDecimalPoint()
    {
        var result = _emitter.Format(5.0);
        Assert.Contains(".", result);
    }

    [Fact]
    public void Format_FloatWithFraction_PreservesDecimal()
    {
        var result = _emitter.Format(3.14f);
        Assert.StartsWith("3.14", result);
    }

    [Fact]
    public void Default_Boolean_ReturnsFalse()
    {
        var type = SymbolHelper.CreateType(Microsoft.CodeAnalysis.SpecialType.System_Boolean);
        Assert.Equal("False", _emitter.Default(type));
    }

    [Fact]
    public void Default_String_ReturnsEmptyString()
    {
        var type = SymbolHelper.CreateType(Microsoft.CodeAnalysis.SpecialType.System_String);
        Assert.Equal("\"\"", _emitter.Default(type));
    }

    [Fact]
    public void Default_Float_ReturnsZeroDot()
    {
        var type = SymbolHelper.CreateType(Microsoft.CodeAnalysis.SpecialType.System_Single);
        Assert.Equal("0.", _emitter.Default(type));
    }

    [Fact]
    public void Default_Int_ReturnsZero()
    {
        var type = SymbolHelper.CreateType(Microsoft.CodeAnalysis.SpecialType.System_Int32);
        Assert.Equal("0", _emitter.Default(type));
    }

    [Fact]
    public void Default_Void_ReturnsEmpty()
    {
        var type = SymbolHelper.CreateType(Microsoft.CodeAnalysis.SpecialType.System_Void);
        Assert.Equal("", _emitter.Default(type));
    }
}
