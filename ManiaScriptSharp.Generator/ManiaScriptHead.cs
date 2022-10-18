using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator;

public class ManiaScriptHead
{
    /// <summary>
    /// Behaves as #RequireContext for official classes and as #Extends for custom classes. Not used for libraries and manialink scripts.
    /// </summary>
    public INamedTypeSymbol? Context { get; init; }
    public ImmutableArray<IPropertySymbol> AdditionalConsts { get; init; }
    public ImmutableArray<INamedTypeSymbol> Structs { get; init; }
    public ImmutableArray<INamedTypeSymbol> Includes { get; init; }
    public ImmutableArray<IFieldSymbol> Consts { get; init; }
    public ImmutableArray<IFieldSymbol> Settings { get; init; }
    public ImmutableArray<ISymbol> Globals { get; init; }
    public ImmutableArray<ISymbol> Bindings { get; init; }
}