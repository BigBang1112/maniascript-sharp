using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator;

internal static class EnumSupport
{
    public static string? NativeName(INamedTypeSymbol type)
    {
        var attributes = type.GetAttributes();
        if (attributes.IsDefaultOrEmpty) return null;
        return attributes.FirstOrDefault(a =>
            a.AttributeClass?.ToDisplayString() == "ManiaScriptSharp.ManiaScriptNameAttribute")
            ?.ConstructorArguments.FirstOrDefault().Value as string;
    }

    /// <summary>API enums already exist in ManiaScript; other C# enums need integer constants.</summary>
    public static bool IsCustomEnum(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType) return false;

        // A renamed official API enum is still native even when represented by source
        // syntax in a generator test or another consuming compilation.
        if (NativeName(enumType) is not null)
            return false;

        if (!enumType.DeclaringSyntaxReferences.IsDefaultOrEmpty
            && enumType.DeclaringSyntaxReferences.Any(reference =>
                !reference.SyntaxTree.FilePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)))
            return true;

        var assemblyName = enumType.ContainingAssembly?.Name;
        return !string.IsNullOrEmpty(assemblyName)
            && assemblyName is not
                ("ManiaScriptSharp.Trackmania" or "ManiaScriptSharp.ManiaPlanet" or "ManiaScriptSharp.ManiaPlanet3");
    }
}
