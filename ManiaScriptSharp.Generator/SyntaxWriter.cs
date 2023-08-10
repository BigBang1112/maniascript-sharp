using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator;

public abstract class SyntaxWriter
{
    public abstract ManiaScriptBodyBuilder BodyBuilder { get; }
    
    public abstract void Write();

    protected ISymbol? GetSymbol(SyntaxNode syntax)
    {
        return BodyBuilder.SemanticModel.GetSymbolInfo(syntax).Symbol;
    }
}
