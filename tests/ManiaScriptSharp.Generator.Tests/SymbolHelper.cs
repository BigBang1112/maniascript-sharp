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

    public static ITypeSymbol CreateType(SpecialType specialType, string name = "")
    {
        var type = Substitute.For<INamedTypeSymbol>();
        type.SpecialType.Returns(specialType);
        type.Name.Returns(name);
        type.IsGenericType.Returns(false);
        return type;
    }

    public static IArrayTypeSymbol CreateArrayType(ITypeSymbol elementType)
    {
        var arr = Substitute.For<IArrayTypeSymbol>();
        arr.ElementType.Returns(elementType);
        return arr;
    }
}
