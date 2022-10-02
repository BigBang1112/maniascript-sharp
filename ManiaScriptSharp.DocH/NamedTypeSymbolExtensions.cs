using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.DocH;

public static class NamedTypeSymbolExtensions
{
    public static bool IsSubclassOf(this INamedTypeSymbol typeSymbol, Func<INamedTypeSymbol, bool> predicate)
    {
        while (typeSymbol.BaseType is not null)
        {
            if (predicate(typeSymbol.BaseType))
            {
                return true;
            }

            typeSymbol = typeSymbol.BaseType;
        }
            
        return false;
    }
}