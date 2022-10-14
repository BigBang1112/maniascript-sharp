using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator;

public class FunctionIdentifier : Function
{
    public IMethodSymbol Method { get; }
    
    public FunctionIdentifier(IMethodSymbol method)
    {
        Method = method;
    }
}