using ManiaScriptSharp.DocH.Blocks;
using ManiaScriptSharp.DocH.Inlines;
using ManiaScriptSharp.DocH.Tests.Mocks;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;

namespace ManiaScriptSharp.DocH.Tests.Unit.Blocks;

public class EnumHBlockTests
{
    [Fact]
    public void End_ReturnsCorrect()
    {
        // Arrange
        var hBlock = new MockMajorHBlock();

        // Act
        var actual = hBlock.End;

        // Assert
        Assert.Equal(expected: "};", actual);
    }

    [Fact]
    public void BeforeRead_MatchNull_Throws()
    {
        // Arrange
        var hBlock = new EnumHBlock();
        var builder = new StringBuilder();

        // Act & Assert
        Assert.Throws<Exception>(() => hBlock.BeforeRead("enum TestEnum {", match: null, builder));
    }

    [Fact]
    public void BeforeRead_StartsCode()
    {
        // Arrange
        var hBlock = new EnumHBlock();
        var builder = new StringBuilder();
        var expected = $"\tpublic enum TestEnum{Environment.NewLine}\t{{{Environment.NewLine}";
        var exampleString = "enum TestEnum {";
        var match = hBlock.IdentifierRegex!.Match(exampleString);

        // Act
        var result = hBlock.BeforeRead(exampleString, match, builder);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, actual: builder.ToString());
    }
    
    [Fact]
    public void BeforeRead_SkipsGenWhenSpecificSymbolAvailable()
    {
        // Arrange
        var symbolMock = new Mock<INamedTypeSymbol>();
        symbolMock.SetupGet(x => x.Name).Returns("TestEnum");

        var dict = ImmutableDictionary.CreateBuilder<string, ISymbol>();
        dict.Add("TestEnum", symbolMock.Object);

        var context = new SymbolContext(ImmutableDictionary<string, ISymbol>.Empty, dict.ToImmutable());
        var hBlock = new EnumHBlock(context);

        var builder = new StringBuilder();
        var exampleString = "enum TestEnum {";
        var match = hBlock.IdentifierRegex!.Match(exampleString);

        // Act
        var result = hBlock.BeforeRead(exampleString, match, builder);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ReadLine_CanProcessEnumValue()
    {
        // Arrange
        var hBlock = new EnumHBlock();
        var expected = $"\t\tOne,{Environment.NewLine}";
        var exampleString = @"One,";
        using var exampleStream = new MemoryStream(Encoding.UTF8.GetBytes(exampleString));
        using var reader = new StreamReader(exampleStream);
        var builder = new StringBuilder();

        // Act
        hBlock.ReadLine(reader.ReadLine()!, reader, builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void ReadLine_CanProcessEnumValue_ForbiddenChars()
    {
        // Arrange
        var hBlock = new EnumHBlock();
        var expected = $"\t\t[ActualName(\"(One)\")] _One_,{Environment.NewLine}";
        var exampleString = @"(One),";
        using var exampleStream = new MemoryStream(Encoding.UTF8.GetBytes(exampleString));
        using var reader = new StreamReader(exampleStream);
        var builder = new StringBuilder();

        // Act
        hBlock.ReadLine(reader.ReadLine()!, reader, builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void AfterRead_AppendsEndCurlyBracket()
    {
        // Arrange
        var hBlock = new EnumHBlock();
        var builder = new StringBuilder();

        // Act
        hBlock.AfterRead(builder);

        // Assert
        Assert.Equal($"\t}}{Environment.NewLine}{Environment.NewLine}", builder.ToString());
    }
}
