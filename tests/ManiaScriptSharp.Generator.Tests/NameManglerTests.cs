using ManiaScriptSharp.Generator.Naming;
using Xunit;

namespace ManiaScriptSharp.Generator.Tests;

public class NameManglerTests
{
    [Theory]
    [InlineData("_score", "Score")]
    [InlineData("__score", "Score")]
    [InlineData("score", "Score")]
    [InlineData("Score", "Score")]
    [InlineData("_s", "S")]
    [InlineData("___", "___")]
    [InlineData("", "")]
    [InlineData("a", "A")]
    [InlineData("_abc", "Abc")]
    public void PascalCase_TransformsCorrectly(string input, string expected)
    {
        Assert.Equal(expected, NameMangler.PascalCase(input));
    }

    [Fact]
    public void Const_AddsPrefix()
    {
        var field = SymbolHelper.CreateField("_maxPlayers", isConst: true);
        Assert.Equal("C_MaxPlayers", NameMangler.Const(field));
    }

    [Fact]
    public void Setting_AddsPrefix()
    {
        var field = SymbolHelper.CreateField("_timeLimit");
        Assert.Equal("S_TimeLimit", NameMangler.Setting(field));
    }

    [Fact]
    public void Net_AddsPrefix()
    {
        var field = SymbolHelper.CreateField("_playerName");
        Assert.Equal("Net_PlayerName", NameMangler.Net(field));
    }

    [Fact]
    public void Persistent_AddsPrefix()
    {
        var field = SymbolHelper.CreateField("_bestScore");
        Assert.Equal("Persistent_BestScore", NameMangler.Persistent(field));
    }

    [Fact]
    public void Global_PublicField_AddsGPrefix()
    {
        var field = SymbolHelper.CreateField("count", accessibility: Microsoft.CodeAnalysis.Accessibility.Public);
        Assert.Equal("G_Count", NameMangler.Global(field));
    }

    [Fact]
    public void Global_PrivateField_NoPrefixJustPascalCase()
    {
        var field = SymbolHelper.CreateField("_count", accessibility: Microsoft.CodeAnalysis.Accessibility.Private);
        Assert.Equal("Count", NameMangler.Global(field));
    }

    [Fact]
    public void Parameter_AddsUnderscorePrefix()
    {
        var param = SymbolHelper.CreateParameter("value");
        Assert.Equal("_Value", NameMangler.Parameter(param));
    }

    [Fact]
    public void Local_PascalCases()
    {
        Assert.Equal("MyVar", NameMangler.Local("_myVar"));
        Assert.Equal("X", NameMangler.Local("x"));
    }

    [Fact]
    public void Method_Private_AddsPrefix()
    {
        var method = SymbolHelper.CreateMethod("doWork", Microsoft.CodeAnalysis.Accessibility.Private);
        Assert.Equal("Private_doWork", NameMangler.Method(method));
    }

    [Fact]
    public void Method_Public_NoPrefix()
    {
        var method = SymbolHelper.CreateMethod("DoWork", Microsoft.CodeAnalysis.Accessibility.Public);
        Assert.Equal("DoWork", NameMangler.Method(method));
    }
}
