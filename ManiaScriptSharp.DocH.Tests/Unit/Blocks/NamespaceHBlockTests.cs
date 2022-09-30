using ManiaScriptSharp.DocH.Blocks;
using ManiaScriptSharp.DocH.Tests.Mocks;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;

namespace ManiaScriptSharp.DocH.Tests.Unit.Blocks;

public class NamespaceHBlockTests
{
    [Fact]
    public void AfterRead_AppendsEndCurlyBracket()
    {
        // Arrange
        var hBlock = new NamespaceHBlock();
        var builder = new StringBuilder();

        // Act
        hBlock.AfterRead(builder);

        // Assert
        Assert.Equal($"}}{Environment.NewLine}{Environment.NewLine}", builder.ToString());
    }

    [Fact]
    public void BeforeAttemptToEnd_TryReadComment()
    {
        // Arrange
        var hBlock = new NamespaceHBlock();

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
        var hBlock = new NamespaceHBlock();

        // Act
        var actual = hBlock.End;

        // Assert
        Assert.Equal("};", actual);
    }

    [Fact]
    public void BeforeRead_StartsCode()
    {
        // Arrange
        var hBlock = new NamespaceHBlock();
        var builder = new StringBuilder();
        var expected = $"public static class TextLib{Environment.NewLine}{{{Environment.NewLine}";
        var exampleString = "namespace TextLib {";
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
        var hBlock = new NamespaceHBlock();
        var builder = new StringBuilder();

        // Act & Assert
        Assert.Throws<Exception>(() => hBlock.BeforeRead("namespace TextLib {", match: null, builder));
    }

    [Fact]
    public void BeforeRead_SymbolExists_NoAttributes()
    {
        // Arrange
        var symbolMock = new Mock<INamedTypeSymbol>();
        symbolMock.SetupGet(x => x.Name).Returns("TextLib");
        symbolMock.SetupGet(x => x.IsStatic).Returns(true);
        symbolMock.Setup(x => x.GetMembers()).Returns(ImmutableArray<ISymbol>.Empty);
        symbolMock.Setup(x => x.GetAttributes()).Returns(ImmutableArray<AttributeData>.Empty);

        var dict = new Dictionary<string, ISymbol>
        {
            { "TextLib", symbolMock.Object }
        };

        var context = new SymbolContext(dict);
        var hBlock = new NamespaceHBlock(context);
        var builder = new StringBuilder();
        var expected = $"public static partial class TextLib{Environment.NewLine}{{{Environment.NewLine}";
        var exampleString = "namespace TextLib {";
        var match = hBlock.IdentifierRegex!.Match(exampleString);

        // Act
        var result = hBlock.BeforeRead(exampleString, match, builder);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void BeforeRead_SymbolExists_HasIgnoreGeneratedAttribute()
    {
        // Arrange
        var ignoreGeneratedSymbolMock = new Mock<INamedTypeSymbol>();
        ignoreGeneratedSymbolMock.SetupGet(x => x.Name).Returns("IgnoreGeneratedAttribute");

        var ignoreGeneratedMock = new MockAttributeData(ignoreGeneratedSymbolMock.Object);

        var symbolMock = new Mock<INamedTypeSymbol>();
        symbolMock.SetupGet(x => x.Name).Returns("TextLib");
        symbolMock.SetupGet(x => x.IsStatic).Returns(true);
        symbolMock.Setup(x => x.GetAttributes()).Returns(ImmutableArray.Create<AttributeData>(ignoreGeneratedMock));

        var dict = new Dictionary<string, ISymbol>
        {
            { "TextLib", symbolMock.Object }
        };

        var context = new SymbolContext(dict);
        var hBlock = new NamespaceHBlock(context);
        var builder = new StringBuilder();
        var exampleString = "namespace TextLib {";
        var match = hBlock.IdentifierRegex!.Match(exampleString);

        // Act
        var result = hBlock.BeforeRead(exampleString, match, builder);

        // Assert
        Assert.False(result);
    }
}
