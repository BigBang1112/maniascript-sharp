using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator;

public static class NamedTypeSymbolExtensions
{
    public static bool IsSubclassOf(this ITypeSymbol typeSymbol, Func<INamedTypeSymbol, bool> predicate)
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