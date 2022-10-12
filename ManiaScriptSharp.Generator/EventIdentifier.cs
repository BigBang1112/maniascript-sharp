using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator;

public class EventIdentifier : EventFunction
{
    public IMethodSymbol Method { get; }
    
    public EventIdentifier(IMethodSymbol method)
    {
        Method = method;
    }
}