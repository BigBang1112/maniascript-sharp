using Microsoft.CodeAnalysis;
using Xunit;

namespace ManiaScriptSharp.Generator.Tests;

public class TypeMapperTests
{
    [Fact]
    public void Map_Null_ReturnsVoid()
    {
        Assert.Equal("Void", TypeMapper.Map(null));
    }

    [Theory]
    [InlineData(SpecialType.System_Void, "Void")]
    [InlineData(SpecialType.System_Boolean, "Boolean")]
    [InlineData(SpecialType.System_Int32, "Integer")]
    [InlineData(SpecialType.System_Int64, "Integer")]
    [InlineData(SpecialType.System_Byte, "Integer")]
    [InlineData(SpecialType.System_UInt32, "Integer")]
    [InlineData(SpecialType.System_Single, "Real")]
    [InlineData(SpecialType.System_Double, "Real")]
    [InlineData(SpecialType.System_Decimal, "Real")]
    [InlineData(SpecialType.System_String, "Text")]
    [InlineData(SpecialType.System_Char, "Text")]
    [InlineData(SpecialType.System_Object, "Text")]
    public void Map_SpecialTypes_MapsCorrectly(SpecialType special, string expected)
    {
        var type = SymbolHelper.CreateType(special);
        Assert.Equal(expected, TypeMapper.Map(type));
    }

    [Fact]
    public void Map_ArrayType_AppendsArraySuffix()
    {
        var elemType = SymbolHelper.CreateType(SpecialType.System_Int32);
        var arrType = SymbolHelper.CreateArrayType(elemType);
        Assert.Equal("Integer[]", TypeMapper.Map(arrType));
    }

    [Fact]
    public void Map_CustomTypeName_ReturnsName()
    {
        var type = SymbolHelper.CreateType(SpecialType.None, "CSmPlayer");
        Assert.Equal("CSmPlayer", TypeMapper.Map(type));
    }

    [Fact]
    public void Map_Vector2_MapsToVec2()
    {
        var type = SymbolHelper.CreateType(SpecialType.None, "Vector2");
        Assert.Equal("Vec2", TypeMapper.Map(type));
    }

    [Fact]
    public void Map_Vector3_MapsToVec3()
    {
        var type = SymbolHelper.CreateType(SpecialType.None, "Vector3");
        Assert.Equal("Vec3", TypeMapper.Map(type));
    }

    [Fact]
    public void Map_NestedEnum_KeepsContainingTypeInPath()
    {
        // C# CUILayer.EUILayerType → ManiaScript CUILayer::EUILayerType
        var containing = (INamedTypeSymbol)SymbolHelper.CreateType(SpecialType.None, "CUILayer");
        var type = SymbolHelper.CreateType(SpecialType.None, "EUILayerType", TypeKind.Enum, containing);
        Assert.Equal("CUILayer::EUILayerType", TypeMapper.Map(type));
    }

    [Fact]
    public void Map_TopLevelEnum_HasNoContainingTypePrefix()
    {
        var type = SymbolHelper.CreateType(SpecialType.None, "EWeapon", TypeKind.Enum);
        Assert.Equal("EWeapon", TypeMapper.Map(type));
    }

    [Fact]
    public void Map_EnumNestedInContextClass_UsesLeadingDoubleColonNoContainingName()
    {
        // An enum nested directly in the IContext class has no ManiaScript struct to
        // qualify it with (the class itself IS the script) → `::MyState`, not `MyGamemode::MyState`.
        var iface = SymbolHelper.CreateInterface("IContext");
        var containing = (INamedTypeSymbol)SymbolHelper.CreateType(SpecialType.None, "MyGamemode", allInterfaces: new[] { iface });
        var type = SymbolHelper.CreateType(SpecialType.None, "MyState", TypeKind.Enum, containing);
        Assert.Equal("::MyState", TypeMapper.Map(type));
    }

    [Fact]
    public void Map_EnumNestedInLibClass_UsesLeadingDoubleColonNoContainingName()
    {
        var iface = SymbolHelper.CreateInterface("ILib", isGenericType: true);
        var containing = (INamedTypeSymbol)SymbolHelper.CreateType(SpecialType.None, "MyLib", allInterfaces: new[] { iface });
        var type = SymbolHelper.CreateType(SpecialType.None, "MyEnum", TypeKind.Enum, containing);
        Assert.Equal("::MyEnum", TypeMapper.Map(type));
    }
}
