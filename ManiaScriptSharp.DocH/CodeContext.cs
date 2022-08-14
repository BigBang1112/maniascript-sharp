using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.DocH;

public class CodeContext
{
    public Dictionary<string, INamedTypeSymbol> Types { get; }

    public CodeContext(Dictionary<string, INamedTypeSymbol> types)
    {
        Types = types;
    }
}
