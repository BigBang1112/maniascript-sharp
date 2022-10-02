using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace ManiaScriptSharp.DocH.Tests.Mocks;

internal class MockAttributeData : AttributeData
{
    protected override INamedTypeSymbol? CommonAttributeClass { get; }
    protected override IMethodSymbol? CommonAttributeConstructor => throw new NotImplementedException();
    protected override SyntaxReference? CommonApplicationSyntaxReference => throw new NotImplementedException();
    protected override ImmutableArray<TypedConstant> CommonConstructorArguments { get; } = ImmutableArray<TypedConstant>.Empty;
    protected override ImmutableArray<KeyValuePair<string, TypedConstant>> CommonNamedArguments => throw new NotImplementedException();

    public MockAttributeData(INamedTypeSymbol attributeClass)
    {
        CommonAttributeClass = attributeClass;
    }
}
