using ManiaScriptSharp.DocH.Inlines;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;

namespace ManiaScriptSharp.DocH.Tests.Unit.Inlines;

public class MethodHInlineTests
{
    [Fact]
    public void Read_ReadsMethod_Void()
    {
        // Arrange
        var hInline = new MethodHInline(isStatic: false);
        var builder = new StringBuilder();
        var match = hInline.IdentifierRegex.Match("    Void EnableMenuNavigation(Boolean EnableInputs,Boolean WithAutoFocus,Boolean WithManualScroll,CMlControl AutoBackControl,Integer InputPriority);");
        var expected = $"\tpublic void EnableMenuNavigation(bool EnableInputs, bool WithAutoFocus, bool WithManualScroll, CMlControl AutoBackControl, int InputPriority) {{ }}{Environment.NewLine}";

        // Act
        hInline.Read(match, builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void Read_ReadsMethod_Static_Void()
    {
        // Arrange
        var hInline = new MethodHInline(isStatic: true);
        var builder = new StringBuilder();
        var match = hInline.IdentifierRegex.Match("    Void EnableMenuNavigation(Boolean EnableInputs,Boolean WithAutoFocus,Boolean WithManualScroll,CMlControl AutoBackControl,Integer InputPriority);");
        var expected = $"\tpublic static void EnableMenuNavigation(bool EnableInputs, bool WithAutoFocus, bool WithManualScroll, CMlControl AutoBackControl, int InputPriority) {{ }}{Environment.NewLine}";

        // Act
        hInline.Read(match, builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void Read_ReadsMethod_Bool()
    {
        // Arrange
        var hInline = new MethodHInline(isStatic: false);
        var builder = new StringBuilder();
        var match = hInline.IdentifierRegex.Match("    Boolean EnableMenuNavigation(Boolean EnableInputs,Boolean WithAutoFocus,Boolean WithManualScroll,CMlControl AutoBackControl,Integer InputPriority);");
        var expected = $"\tpublic bool EnableMenuNavigation(bool EnableInputs, bool WithAutoFocus, bool WithManualScroll, CMlControl AutoBackControl, int InputPriority) {{ return default; }}{Environment.NewLine}";

        // Act
        hInline.Read(match, builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void Read_ReadsMethod_TypeOwnerParameter()
    {
        // Arrange
        var hInline = new MethodHInline(isStatic: false);
        var builder = new StringBuilder();
        var match = hInline.IdentifierRegex.Match("\t\t\tCTaskResult UbisoftClub_Launch(Ident UserId,CUserV2Manager::EUbisoftClubFlow UbisoftClubFlow,Text RewardId);");
        var expected = $"\tpublic CTaskResult UbisoftClub_Launch(Ident UserId, CUserV2Manager.EUbisoftClubFlow UbisoftClubFlow, string RewardId) {{ return default; }}{Environment.NewLine}";

        // Act
        hInline.Read(match, builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void Read_ReadsMethod_TypeOwner()
    {
        // Arrange
        var hInline = new MethodHInline(isStatic: false);
        var builder = new StringBuilder();
        var match = hInline.IdentifierRegex.Match("\t\t\tCEditorMesh::ELayerType Layers_GetLayerTypeFromIndex(Integer LayerIndex);");
        var expected = $"\tpublic CEditorMesh.ELayerType Layers_GetLayerTypeFromIndex(int LayerIndex) {{ return default; }}{Environment.NewLine}";

        // Act
        hInline.Read(match, builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void Read_ReadsMethod_WithMultipleSameNameParams()
    {
        // Arrange
        var hInline = new MethodHInline(isStatic: false);
        var builder = new StringBuilder();
        var match = hInline.IdentifierRegex.Match("\t\t\tVoid Solo_SetNewRecord(CScore PlayerScore,CMode::EMedal PlayerScore);");
        var expected = $"\tpublic void Solo_SetNewRecord(CScore PlayerScore, [ActualName(\"PlayerScore\")] CMode.EMedal PlayerScore2) {{ }}{Environment.NewLine}";

        // Act
        hInline.Read(match, builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Theory]
    [InlineData("Void", "ItemList_Begin")]
    [InlineData("Void", "ActionList_Begin")]
    public void Read_CommentsOutCertainMethods(string returnType, string name)
    {
        // Arrange
        var hInline = new MethodHInline(isStatic: false);
        var builder = new StringBuilder();
        var match = hInline.IdentifierRegex.Match($"\t\t\t{returnType} {name}();");
        var expected = $"\t// public {HInline.GetTypeBindOrDefault(returnType)} {name}() {{ }}{Environment.NewLine}";

        // Act
        hInline.Read(match, builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }
    
    [Fact]
    public void Read_ReadsMethod_PartialMethod()
    {
        // Arrange
        var symbolMock = new Mock<IMethodSymbol>();
        symbolMock.SetupGet(x => x.Name).Returns("EnableMenuNavigation");

        var dict = ImmutableDictionary.CreateBuilder<string, ISymbol>();
        dict.Add("EnableMenuNavigation", symbolMock.Object);

        var context = new SymbolContext(ImmutableDictionary<string, ISymbol>.Empty, dict.ToImmutable());
        var hInline = new MethodHInline(context, isStatic: false);
        var builder = new StringBuilder();
        var match = hInline.IdentifierRegex.Match("    Void EnableMenuNavigation(Boolean EnableInputs,Boolean WithAutoFocus,Boolean WithManualScroll,CMlControl AutoBackControl,Integer InputPriority);");
        var expected = $"\tpublic partial void EnableMenuNavigation(bool EnableInputs, bool WithAutoFocus, bool WithManualScroll, CMlControl AutoBackControl, int InputPriority);{Environment.NewLine}";

        // Act
        hInline.Read(match, builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void Read_ReadsMethod_NameCollision()
    {
        // Arrange
        var symbolMock = new Mock<IPropertySymbol>();
        symbolMock.SetupGet(x => x.Name).Returns("EnableMenuNavigation");

        var dict = ImmutableDictionary.CreateBuilder<string, ISymbol>();
        dict.Add("EnableMenuNavigation", symbolMock.Object);

        var context = new SymbolContext(ImmutableDictionary<string, ISymbol>.Empty, dict.ToImmutable());
        var hInline = new MethodHInline(context, isStatic: false);
        var builder = new StringBuilder();
        var match = hInline.IdentifierRegex.Match("    Void EnableMenuNavigation(Boolean EnableInputs,Boolean WithAutoFocus,Boolean WithManualScroll,CMlControl AutoBackControl,Integer InputPriority);");

        // Act & Assert
        Assert.Throws<Exception>(() => hInline.Read(match, builder));
    }
}
