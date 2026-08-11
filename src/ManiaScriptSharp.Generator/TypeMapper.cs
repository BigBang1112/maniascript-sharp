using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator;

/// <summary>
/// Maps C# types to ManiaScript types. Mirrors the table in ManiaScriptSharp.md.
/// </summary>
internal static class TypeMapper
{
    public static string Map(ITypeSymbol? type)
    {
        if (type is null) return "Void";

        if (type is IArrayTypeSymbol arr)
            return Map(arr.ElementType) + "[]";

        if (type is INamedTypeSymbol named && named.IsGenericType)
        {
            switch (named.ConstructedFrom.ToDisplayString())
            {
                case "System.Nullable<T>":
                    return Map(named.TypeArguments[0]);
                case "System.Collections.Generic.List<T>":
                case "System.Collections.Generic.IList<T>":
                case "System.Collections.Generic.IReadOnlyList<T>":
                case "System.Collections.Generic.ICollection<T>":
                case "System.Collections.Generic.IEnumerable<T>":
                case "System.Collections.Immutable.ImmutableArray<T>":
                    return Map(named.TypeArguments[0]) + "[]";
                case "System.Collections.Generic.Dictionary<TKey, TValue>":
                case "System.Collections.Generic.IDictionary<TKey, TValue>":
                case "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>":
                    return Map(named.TypeArguments[1]) + "[" + Map(named.TypeArguments[0]) + "]";
            }
        }

        return type.SpecialType switch
        {
            SpecialType.System_Void => "Void",
            SpecialType.System_Boolean => "Boolean",
            SpecialType.System_Byte or SpecialType.System_SByte or
            SpecialType.System_Int16 or SpecialType.System_UInt16 or
            SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Int64 or SpecialType.System_UInt64 => "Integer",
            SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal => "Real",
            SpecialType.System_String or SpecialType.System_Char or SpecialType.System_Object => "Text",
            _ => MapNamed(type),
        };
    }

    private static string MapNamed(ITypeSymbol type)
    {
        // Nested enums (e.g. CUILayer.EUILayerType) keep their containing type in the
        // path, mirroring the ManiaScript header (`CUILayer::EUILayerType`). An enum
        // nested directly inside the context/lib class itself has no ManiaScript
        // counterpart for its "container" (the class IS the script), so it's a
        // script-local enum referenced with a leading `::` instead (e.g. `::MyState`).
        if (type.TypeKind == TypeKind.Enum && type.ContainingType is { } containing)
            return IsContextOrLibType(containing) ? $"::{type.Name}" : $"{containing.Name}::{type.Name}";

        return type.Name switch
        {
            "Vector2" => "Vec2",
            "Vector3" => "Vec3",
            _ => type.Name,
        };
    }

    /// <summary>Returns <see langword="true"/> when <paramref name="type"/> implements <c>IContext</c> or <c>ILib&lt;T&gt;</c>.</summary>
    private static bool IsContextOrLibType(INamedTypeSymbol type)
    {
        foreach (var i in type.AllInterfaces)
        {
            if (i.ContainingNamespace?.ToDisplayString() != "ManiaScriptSharp") continue;
            if (i.Name == "IContext") return true;
            if (i.Name == "ILib" && i.IsGenericType) return true;
        }
        return false;
    }
}
