using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator;

internal static class EnumSupport
{
    /// <summary>API enums already exist in ManiaScript; other C# enums need integer constants.</summary>
    public static bool IsCustomEnum(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType) return false;

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
