using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.DocH;

public class SymbolContext
{
    public Dictionary<string, ISymbol> Symbols { get; }

    public SymbolContext(Dictionary<string, ISymbol> symbols)
    {
        Symbols = symbols;
    }
}
