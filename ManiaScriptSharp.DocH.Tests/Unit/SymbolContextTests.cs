using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.DocH.Tests.Unit;

public class SymbolContextTests
{
    [Fact]
    public void Constructor()
    {
        // Arrange
        var exampleDict = new Dictionary<string, ISymbol>();

        // Act
        var context = new SymbolContext(exampleDict);

        // Assert
        Assert.Equal(exampleDict, context.Symbols);
    }
}
