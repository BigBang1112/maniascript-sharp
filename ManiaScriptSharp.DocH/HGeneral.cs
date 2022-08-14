using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.DocH;

public abstract class HGeneral
{
    protected internal SymbolContext? Context { get; }
    protected internal ISymbol? ManualSymbol { get; protected set; }

    public HGeneral(SymbolContext? context)
    {
        Context = context;
    }
}
