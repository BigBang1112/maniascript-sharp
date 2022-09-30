using ManiaScriptSharp.DocH.Inlines;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;

namespace ManiaScriptSharp.DocH.Tests.Unit.Inlines;

public class ConstHInlineTests
{
    [Fact]
    public void AppendValueAsCorrectCSharpString_Float()
    {
        // Arrange
        var builder = new StringBuilder();

        // Act
        ConstHInline.AppendValueAsCorrectCSharpString(builder, "float", "3.14");

        // Assert
        Assert.Equal("3.14f", actual: builder.ToString());
    }

    [Fact]
    public void AppendValueAsCorrectCSharpString_AnythingElse()
    {
        // Arrange
        var builder = new StringBuilder();

        // Act & Assert
        Assert.Throws<Exception>(() => ConstHInline.AppendValueAsCorrectCSharpString(builder, "int", "5"));
    }

    [Fact]
    public void Read_ReadsConstWithDifferentTypeName()
    {
        // Arrange
        var hInline = new ConstHInline();
        var builder = new StringBuilder();
        var match = hInline.IdentifierRegex.Match("    const Real Tau = 6.28319;");
        var expected = $"\tpublic const float Tau = 6.28319f;{Environment.NewLine}";

        // Act
        hInline.Read(match, builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void Read_ReadsConstWithSameTypeName()
    {
        // Arrange
        var hInline = new ConstHInline();
        var builder = new StringBuilder();
        var match = hInline.IdentifierRegex.Match("    const Real float = 6.28319;");
        var expected = $"\t[ActualName(\"float\")] public const float floatC = 6.28319f;{Environment.NewLine}";

        // Act
        hInline.Read(match, builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void Read_ReadsProperty_NameCollision()
    {
        // Arrange
        var symbolMock = new Mock<ISymbol>();
        symbolMock.SetupGet(x => x.Name).Returns("Tau");

        var dict = ImmutableDictionary.CreateBuilder<string, ISymbol>();
        dict.Add("Tau", symbolMock.Object);

        var context = new SymbolContext(ImmutableDictionary<string, ISymbol>.Empty, dict.ToImmutable());
        var hInline = new ConstHInline(context);
        var builder = new StringBuilder();
        var match = hInline.IdentifierRegex.Match("\tconst Real Tau = 6.28319;");
        var expected = $"\tpublic const float Tau = 6.28319f;{Environment.NewLine}";

        // Act
        hInline.Read(match, builder);

        // Assert
        Assert.Equal(expected: 0, actual: builder.Length);
    }
}
