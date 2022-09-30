using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace ManiaScriptSharp.DocH.Tests.Unit;

public class SymbolContextTests
{
    [Fact]
    public void Constructor()
    {
        // Arrange
        var exampleDict = ImmutableDictionary<string, ISymbol>.Empty;
        var exampleDict2 = ImmutableDictionary<string, ISymbol>.Empty;

        // Act
        var context = new SymbolContext(exampleDict, exampleDict2);

        // Assert
        Assert.Equal(exampleDict, context.SharedSymbols);
        Assert.Equal(exampleDict2, context.SpecificSymbols);
    }
}
