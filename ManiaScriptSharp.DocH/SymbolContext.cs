using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace ManiaScriptSharp.DocH;

public class SymbolContext
{
    public ImmutableDictionary<string, ISymbol> SharedSymbols { get; }
    public ImmutableDictionary<string, ISymbol> SpecificSymbols { get; }

    public SymbolContext(ImmutableDictionary<string, ISymbol> sharedSymbols, ImmutableDictionary<string, ISymbol> specificSymbols)
    {
        SharedSymbols = sharedSymbols;
        SpecificSymbols = specificSymbols;
    }
}
