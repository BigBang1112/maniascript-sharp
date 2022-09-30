using ManiaScriptSharp.DocH.Blocks;
using ManiaScriptSharp.DocH.Inlines;
using ManiaScriptSharp.DocH.Tests.Mocks;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;

namespace ManiaScriptSharp.DocH.Tests.Unit.Blocks;

public class ClassOrStructHBlockTests
{
    [Fact]
    public void AfterRead_AppendsEndCurlyBracket()
    {
        // Arrange
        var hBlock = new ClassOrStructHBlock();
        var builder = new StringBuilder();

        // Act
        hBlock.AfterRead(builder);

        // Assert
        Assert.Equal($"{Environment.NewLine}\tprotected internal {hBlock.Name}() {{ }}{Environment.NewLine}}}{Environment.NewLine}{Environment.NewLine}", builder.ToString());
    }

    [Fact]
    public void BeforeAttemptToEnd_TryReadComment()
    {
        // Arrange
        var hBlock = new ClassOrStructHBlock();

        var expected = $"\t/// <summary>{Environment.NewLine}" +
            $"\t/// Save a matchsettings file.{Environment.NewLine}" +
            $"\t/// </summary>{Environment.NewLine}";

        var exampleString = @"/*!
Save a matchsettings file.
*/";
        using var exampleStream = new MemoryStream(Encoding.UTF8.GetBytes(exampleString));
        using var reader = new StreamReader(exampleStream);
        var builder = new StringBuilder();

        // Act
        hBlock.BeforeAttemptToEnd(reader.ReadLine()!, reader, builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void End_ReturnsCorrect()
    {
        // Arrange
        var hBlock = new ClassOrStructHBlock();

        // Act
        var actual = hBlock.End;

        // Assert
        Assert.Equal("};", actual);
    }

    [Fact]
    public void BeforeRead_StartsCode()
    {
        // Arrange
        var hBlock = new ClassOrStructHBlock();
        var builder = new StringBuilder();
        var expected = $"public class CUIConfigMarker : CNod{Environment.NewLine}{{{Environment.NewLine}";
        var exampleString = "class CUIConfigMarker : public CNod {";
        var match = hBlock.IdentifierRegex!.Match(exampleString);

        // Act
        var result = hBlock.BeforeRead(exampleString, match, builder);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void BeforeRead_MatchNull_Throws()
    {
        // Arrange
        var hBlock = new ClassOrStructHBlock();
        var builder = new StringBuilder();

        // Act & Assert
        Assert.Throws<Exception>(() => hBlock.BeforeRead("class CUIConfigMarker : public CNod {", match: null, builder));
    }

    [Fact]
    public void BeforeRead_IsIgnoredClass()
    {
        // Arrange
        var hBlock = new ClassOrStructHBlock();
        var builder = new StringBuilder();
        var exampleString = "struct Void {};";
        var match = hBlock.IdentifierRegex!.Match(exampleString);

        // Act
        var result = hBlock.BeforeRead(exampleString, match, builder);

        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public void BeforeRead_SpecificSymbolExists()
    {
        // Arrange
        var namedTypeSymbol = new Mock<INamedTypeSymbol>();
        namedTypeSymbol.SetupGet(x => x.Name).Returns("CUIConfigMarker");
        namedTypeSymbol.Setup(x => x.GetMembers()).Returns(ImmutableArray<ISymbol>.Empty);
        namedTypeSymbol.Setup(x => x.GetAttributes()).Returns(ImmutableArray<AttributeData>.Empty);

        var dict = ImmutableDictionary.CreateBuilder<string, ISymbol>();
        dict.Add("CUIConfigMarker", namedTypeSymbol.Object);
        
        var context = new SymbolContext(ImmutableDictionary<string, ISymbol>.Empty, dict.ToImmutable());
        var hBlock = new ClassOrStructHBlock(context);
        var builder = new StringBuilder();
        var expected = $"public partial class CUIConfigMarker : CNod{Environment.NewLine}{{{Environment.NewLine}";
        var exampleString = "class CUIConfigMarker : public CNod {";
        var match = hBlock.IdentifierRegex!.Match(exampleString);

        // Act
        var result = hBlock.BeforeRead(exampleString, match, builder);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void ReadLine_ReadsFromHGenerals_FindsNothing()
    {
        // Arrange
        var exampleString = "";
        using var exampleStream = new MemoryStream(Encoding.UTF8.GetBytes(exampleString));
        using var reader = new StreamReader(exampleStream);
        var hBlock = new ClassOrStructHBlock();
        var builder = new StringBuilder();

        // Act
        var result = hBlock.ReadLine(exampleString, reader, builder);

        // Assert
        Assert.False(result);
    }
}
