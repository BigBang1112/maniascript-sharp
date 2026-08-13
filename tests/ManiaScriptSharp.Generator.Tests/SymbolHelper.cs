using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using NSubstitute;

namespace ManiaScriptSharp.Generator.Tests;

/// <summary>
/// Minimal NSubstitute helpers for creating mock Roslyn symbols
/// needed by NameMangler and TypeMapper tests.
/// </summary>
internal static class SymbolHelper
{
    public static IFieldSymbol CreateField(
        string name,
        bool isConst = false,
        Accessibility accessibility = Accessibility.Private)
    {
        var field = Substitute.For<IFieldSymbol>();
        field.Name.Returns(name);
        field.IsConst.Returns(isConst);
        field.DeclaredAccessibility.Returns(accessibility);
        return field;
    }

    public static IParameterSymbol CreateParameter(string name)
    {
        var param = Substitute.For<IParameterSymbol>();
        param.Name.Returns(name);
        return param;
    }

    public static IMethodSymbol CreateMethod(string name, Accessibility accessibility)
    {
        var method = Substitute.For<IMethodSymbol>();
        method.Name.Returns(name);
        method.DeclaredAccessibility.Returns(accessibility);
        return method;
    }

    public static ITypeSymbol CreateType(
        SpecialType specialType,
        string name = "",
        TypeKind typeKind = TypeKind.Class,
        INamedTypeSymbol? containingType = null,
        IEnumerable<INamedTypeSymbol>? allInterfaces = null)
    {
        var type = Substitute.For<INamedTypeSymbol>();
        type.SpecialType.Returns(specialType);
        type.Name.Returns(name);
        type.IsGenericType.Returns(false);
        type.TypeKind.Returns(typeKind);
        type.ContainingType.Returns(containingType);
        type.AllInterfaces.Returns(allInterfaces?.ToImmutableArray() ?? ImmutableArray<INamedTypeSymbol>.Empty);
        return type;
    }

    /// <summary>Mocks an interface symbol (e.g. <c>IContext</c>) as seen via <c>AllInterfaces</c>.</summary>
    public static INamedTypeSymbol CreateInterface(string name, string containingNamespace = "ManiaScriptSharp", bool isGenericType = false)
    {
        var iface = Substitute.For<INamedTypeSymbol>();
        iface.Name.Returns(name);
        iface.IsGenericType.Returns(isGenericType);
        var ns = Substitute.For<INamespaceSymbol>();
        ns.ToDisplayString().Returns(containingNamespace);
        iface.ContainingNamespace.Returns(ns);
        return iface;
    }

    public static IArrayTypeSymbol CreateArrayType(ITypeSymbol elementType)
    {
        var arr = Substitute.For<IArrayTypeSymbol>();
        arr.ElementType.Returns(elementType);
        return arr;
    }
}
