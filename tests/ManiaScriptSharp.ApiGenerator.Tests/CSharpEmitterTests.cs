using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ManiaScriptSharp.ApiGenerator.Tests;

public class CSharpEmitterTests
{
    private static ParsedHeader ParseHeader(string input) => new HeaderParser(input).Parse();

    private static Dictionary<string, string> EmitAll(string input, string ns = "Test",
        ApiGeneratorSettings? settings = null,
        HashSet<(string TypeName, string MethodName)>? userImplemented = null)
    {
        var parsed = ParseHeader(input);
        var emitter = new CSharpEmitter(ns, "test.h", parsed, settings, userImplemented);
        return emitter.Emit(parsed).ToDictionary(x => x.FileName, x => x.Source);
    }

    private static Dictionary<string, string> EmitInstance(string input, string ns = "Test") =>
        EmitAll(input, ns, ApiGeneratorSettings.ForTesting(namespaceLibsStatic: false));

    [Fact]
    public void Emit_SimpleType_GeneratesClassFile()
    {
        var files = EmitAll("struct CFoo : public CNod { };");
        Assert.True(files.ContainsKey("CFoo.g.cs"));
        Assert.Contains("public partial class CFoo", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_TypeWithBase_EmitsInheritance()
    {
        var files = EmitAll("struct CChild : public CParent { };");
        Assert.Contains(": CParent", files["CChild.g.cs"]);
    }

    [Fact]
    public void Emit_CNodBase_EmitsInheritance()
    {
        var files = EmitAll("struct CFoo : public CNod { };");
        Assert.Contains(": CNod", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_Field_GeneratesProperty()
    {
        var input = @"struct CFoo : public CNod { Integer Score; };";
        var files = EmitAll(input);
        Assert.Contains("public int Score { get; set; }", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_ConstField_NoSetter()
    {
        var input = @"struct CFoo : public CNod { const Integer Now; };";
        var files = EmitAll(input);
        Assert.Contains("public int Now { get; }", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_TrackmaniaConstPointerField_NoSetter()
    {
        var input = @"
class CFoo : public CNod {
public :
    CUser * const  LocalUser;
};";
        var files = EmitAll(input);
        Assert.Contains("public CUser LocalUser { get; }", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_Method_GeneratesMethodStub()
    {
        var input = @"struct CFoo : public CNod { Void DoWork(Integer X); };";
        var files = EmitAll(input);
        Assert.Contains("public void DoWork(int x)", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_MethodWithReturnType_ReturnsDefault()
    {
        var input = @"struct CFoo : public CNod { Integer GetValue(); };";
        var files = EmitAll(input);
        Assert.Contains("=> default!", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_TypeMapping_IntegerToInt()
    {
        var input = @"struct CFoo : public CNod { Integer X; };";
        var files = EmitAll(input);
        Assert.Contains("public int X", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_TypeMapping_RealToFloat()
    {
        var input = @"struct CFoo : public CNod { Real Speed; };";
        var files = EmitAll(input);
        Assert.Contains("public float Speed", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_TypeMapping_BooleanToBool()
    {
        var input = @"struct CFoo : public CNod { Boolean Active; };";
        var files = EmitAll(input);
        Assert.Contains("public bool Active", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_TypeMapping_TextToString()
    {
        var input = @"struct CFoo : public CNod { Text Name; };";
        var files = EmitAll(input);
        Assert.Contains("public string Name", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_NestedEnum_GeneratedInsideClass()
    {
        var input = @"
struct CFoo : public CNod {
    enum EColor { Red, Green, Blue, };
};";
        var files = EmitAll(input);
        Assert.Contains("public enum EColor", files["CFoo.g.cs"]);
        Assert.Contains("Red,", files["CFoo.g.cs"]);
        Assert.Contains("Green,", files["CFoo.g.cs"]);
        Assert.Contains("Blue,", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_TopLevelEnum_SeparateFile()
    {
        var input = @"enum ETest { A, B, };";
        var files = EmitAll(input);
        Assert.True(files.ContainsKey("ETest.g.cs"));
        Assert.Contains("public enum ETest", files["ETest.g.cs"]);
    }

    [Fact]
    public void Emit_ArrayField_GeneratesArrayProperty()
    {
        var input = @"struct CFoo : public CNod { Integer[] Scores; };";
        var files = EmitAll(input);
        Assert.Contains("public int[] Scores", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_UnknownType_GeneratesStub()
    {
        var input = @"struct CFoo : public CNod { CUnknown Ref; };";
        var files = EmitAll(input);
        Assert.True(files.ContainsKey("CUnknown.stub.g.cs"));
        Assert.Contains("public partial class CUnknown", files["CUnknown.stub.g.cs"]);
    }

    [Fact]
    public void Emit_QualifiedTypeInDefinedClass_NoStub()
    {
        var input = @"
struct CMode : public CNod {
    enum EWeapon { Laser, Rocket, };
    CMode::EWeapon CurrentWeapon;
};";
        var files = EmitAll(input);
        Assert.False(files.ContainsKey("EWeapon.stub.g.cs"));
    }

    [Fact]
    public void Emit_NestedEnumUsedUnqualified_NoStub()
    {
        var input = @"
struct CBlockModel : public CNod {
    enum EWayPointType { Start, Finish, Checkpoint, };
    const EWayPointType WaypointType;
};";
        var files = EmitAll(input);
        Assert.False(files.ContainsKey("EWayPointType.stub.g.cs"));
    }

    [Fact]
    public void Emit_Primitives_AlwaysGenerated()
    {
        var files = EmitAll("struct CFoo : public CNod { };");
        Assert.True(files.ContainsKey("__Primitives.g.cs"));
        Assert.Contains("Vec2", files["__Primitives.g.cs"]);
        Assert.Contains("Vec3", files["__Primitives.g.cs"]);
        Assert.Contains("Int2", files["__Primitives.g.cs"]);
        Assert.Contains("Int3", files["__Primitives.g.cs"]);
        Assert.Contains("Ident", files["__Primitives.g.cs"]);
    }

    [Fact]
    public void Emit_DocComment_IncludedAsSummary()
    {
        var input = @"
/*! My description */
struct CFoo : public CNod { };";
        var files = EmitAll(input);
        Assert.Contains("/// <summary>My description</summary>", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_ReservedWord_EscapedWithAt()
    {
        var input = @"struct CFoo : public CNod { Integer @event; };";
        var files = EmitAll(input);
        // The field name 'event' is a C# keyword and should be escaped
        Assert.Contains("@event", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_InheritedFieldOverlap_EmitsNew()
    {
        var input = @"
struct CBase : public CNod {
    Integer Score;
};
struct CChild : public CBase {
    Integer Score;
};";
        var files = EmitAll(input);
        Assert.Contains("public new int Score", files["CChild.g.cs"]);
    }

    [Fact]
    public void Emit_InheritedEnumOverlap_EmitsNew()
    {
        var input = @"
class CManiaAppEvent : public CNod {
public :
    enum EType { KeyPress, LayerCustomEvent, };
    CManiaAppEvent::EType Type;
};
class CMapEditorPluginEvent : public CManiaAppEvent {
public :
    enum Type { LayerCustomEvent, CursorChange, };
    CMapEditorPluginEvent::Type Type;
};";
        var files = EmitAll(input);
        // The nested enum 'Type' on CMapEditorPluginEvent hides base's 'Type' field
        Assert.Contains("public new enum Type", files["CMapEditorPluginEvent.g.cs"]);
    }

    [Fact]
    public void Emit_Namespace_UsesProvided()
    {
        var files = EmitAll("struct CFoo : public CNod { };", ns: "MyNamespace");
        Assert.Contains("namespace MyNamespace;", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_DuplicateType_KeepsMostComplete()
    {
        var input = @"
struct CFoo : public CNod { };
struct CFoo : public CNod { Integer X; Integer Y; };";
        var files = EmitAll(input);
        // Should keep the second (more complete) definition
        Assert.Contains("X", files["CFoo.g.cs"]);
        Assert.Contains("Y", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_DictField_GeneratesDictionaryProperty()
    {
        var input = @"
class CFoo : public CNod {
public :
    AssociativeArray<Text, Integer> Data;
};";
        var files = EmitAll(input);
        Assert.Contains("Dictionary<string, int>", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_Namespace_GeneratesStaticClass()
    {
        var input = "namespace MathLib { Integer Abs(Integer X); };";
        var files = EmitAll(input);
        Assert.True(files.ContainsKey("MathLib.g.cs"));
        Assert.Contains("public static partial class MathLib", files["MathLib.g.cs"]);
    }

    [Fact]
    public void Emit_Namespace_MethodIsStatic()
    {
        var input = "namespace MathLib { Integer Abs(Integer X); };";
        var files = EmitAll(input);
        Assert.Contains("public static int Abs", files["MathLib.g.cs"]);
    }

    [Fact]
    public void Emit_Namespace_MultipleMethodsAllStatic()
    {
        var input = "namespace MathLib { Integer Abs(Integer X); Real Sin(Real X); };";
        var files = EmitAll(input);
        var src = files["MathLib.g.cs"];
        Assert.Contains("public static int Abs", src);
        Assert.Contains("public static float Sin", src);
    }

    [Fact]
    public void Emit_Namespace_NoInheritanceClause()
    {
        var input = "namespace MathLib { Integer Abs(Integer X); };";
        var files = EmitAll(input);
        Assert.DoesNotContain(" : ", files["MathLib.g.cs"].Split('\n')
            .First(l => l.Contains("class MathLib")));
    }

    [Fact]
    public void Emit_Namespace_ConstFieldIsStaticProperty()
    {
        var input = "namespace MathLib { const Real Pi = 3.14159; };";
        var files = EmitAll(input);
        Assert.Contains("public static float Pi { get; }", files["MathLib.g.cs"]);
    }

    [Fact]
    public void Emit_Namespace_ArrayReturnType_UsesArray()
    {
        var input = "namespace TextLib { Text[Void] Split(Text Separators, Text Text_); };";
        var files = EmitAll(input);
        Assert.Contains("public static string[] Split", files["TextLib.g.cs"]);
    }

    [Fact]
    public void Emit_Namespace_VoidReturnMethod_EmitsEmptyBody()
    {
        var input = "namespace MathLib { Void DoNothing(); };";
        var files = EmitAll(input);
        Assert.Contains("public static void DoNothing() { }", files["MathLib.g.cs"]);
    }

    [Fact]
    public void Emit_Namespace_DocComment_IncludedAsSummary()
    {
        var input = @"
/*! Standard math operations. */
namespace MathLib {
    Integer Abs(Integer X);
};";
        var files = EmitAll(input);
        Assert.Contains("/// <summary>Standard math operations.</summary>", files["MathLib.g.cs"]);
    }

    // ── Instance (non-static) namespace lib mode ─────────────────────────────

    [Fact]
    public void Emit_Namespace_InstanceMode_GeneratesPartialClass()
    {
        var input = "namespace MathLib { Integer Abs(Integer X); };";
        var files = EmitInstance(input);
        Assert.Contains("public sealed partial class MathLib", files["MathLib.g.cs"]);
    }

    [Fact]
    public void Emit_Namespace_InstanceMode_NoStaticKeyword()
    {
        var input = "namespace MathLib { Integer Abs(Integer X); };";
        var files = EmitInstance(input);
        Assert.DoesNotContain("static partial class", files["MathLib.g.cs"]);
    }

    [Fact]
    public void Emit_Namespace_InstanceMode_MethodIsNotStatic()
    {
        var input = "namespace MathLib { Integer Abs(Integer X); };";
        var files = EmitInstance(input);
        Assert.Contains("public int Abs", files["MathLib.g.cs"]);
        Assert.DoesNotContain("public static int Abs", files["MathLib.g.cs"]);
    }

    [Fact]
    public void Emit_Namespace_InstanceMode_MultipleMethodsNotStatic()
    {
        var input = "namespace MathLib { Integer Abs(Integer X); Real Sin(Real X); };";
        var files = EmitInstance(input);
        var src = files["MathLib.g.cs"];
        Assert.DoesNotContain("static", src.Split('\n').Where(l => l.Contains("int Abs") || l.Contains("float Sin")).First());
    }

    [Fact]
    public void Emit_Namespace_InstanceMode_VoidMethodIsNotVirtual()
    {
        var input = "namespace MathLib { Void DoNothing(); };";
        var files = EmitInstance(input);
        Assert.Contains("public void DoNothing()", files["MathLib.g.cs"]);
        Assert.DoesNotContain("virtual", files["MathLib.g.cs"]);
    }

    [Fact]
    public void Emit_Namespace_InstanceMode_FieldNotStatic()
    {
        var input = "namespace MathLib { const Real Pi = 3.14159; };";
        var files = EmitInstance(input);
        var src = files["MathLib.g.cs"];
        Assert.Contains("public float Pi", src);
        Assert.DoesNotContain("public static float Pi", src);
    }

    [Fact]
    public void Emit_Namespace_InstanceMode_ImplementsILib()
    {
        var input = "namespace MathLib { Integer Abs(Integer X); };";
        var files = EmitInstance(input);
        var classLine = files["MathLib.g.cs"].Split('\n')
            .First(l => l.Contains("class MathLib"));
        Assert.Contains(": ILib", classLine);
    }

    [Fact]
    public void Emit_Namespace_InstanceMode_RegularTypesUnaffected()
    {
        var input = @"
struct CFoo : public CNod { Integer X; };
namespace MathLib { Integer Abs(Integer X); };";
        var files = EmitInstance(input);
        // Regular class stays non-static, no sealed
        Assert.Contains("public int X { get; set; }", files["CFoo.g.cs"]);
        Assert.DoesNotContain("sealed", files["CFoo.g.cs"]);
    }

    // ── Default (static) mode — explicit setting ──────────────────────────────

    [Fact]
    public void Emit_Namespace_StaticMode_ExplicitSetting_SameAsDefault()
    {
        var input = "namespace MathLib { Integer Abs(Integer X); };";
        var filesDefault = EmitAll(input);
        var filesExplicit = EmitAll(input, settings: ApiGeneratorSettings.ForTesting(namespaceLibsStatic: true));
        Assert.Equal(filesDefault["MathLib.g.cs"], filesExplicit["MathLib.g.cs"]);
    }

    // ── Parameter name standardization ───────────────────────────────────────

    [Fact]
    public void Emit_Method_StandardizeParamNames_Default_CamelCase()
    {
        var input = @"struct CFoo : public CNod { Void DoWork(Integer _Value); };";
        var files = EmitAll(input);
        Assert.Contains("public void DoWork(int value)", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_Method_StandardizeParamNames_Disabled_KeepsOriginal()
    {
        var input = @"struct CFoo : public CNod { Void DoWork(Integer _Value); };";
        var files = EmitAll(input, settings: ApiGeneratorSettings.ForTesting(standardizeParamNames: false));
        Assert.Contains("public void DoWork(int _Value)", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_Method_StandardizeParamNames_Disabled_PascalCaseKept()
    {
        var input = @"struct CFoo : public CNod { Integer Add(Integer Argument1, Integer Argument2); };";
        var files = EmitAll(input, settings: ApiGeneratorSettings.ForTesting(standardizeParamNames: false));
        Assert.Contains("int Add(int Argument1, int Argument2)", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_Method_StandardizeParamNames_Enabled_StripLeadingUnderscore()
    {
        var input = @"struct CFoo : public CNod { Void Set(Integer _X, Integer _Y); };";
        var files = EmitAll(input);
        Assert.Contains("public void Set(int x, int y)", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_Namespace_StandardizeParamNames_Disabled_KeepsOriginal()
    {
        var input = "namespace MathLib { Integer Abs(Integer _Argument1); };";
        var files = EmitAll(input, settings: ApiGeneratorSettings.ForTesting(standardizeParamNames: false));
        Assert.Contains("int Abs(int _Argument1)", files["MathLib.g.cs"]);
    }

    // ── User partial method implementations ──────────────────────────────────

    [Fact]
    public void Emit_Method_UserImplemented_EmitsPartialDeclaration()
    {
        var input = @"struct CFoo : public CNod { Integer GetValue(); };";
        var userImpl = new HashSet<(string, string)> { ("CFoo", "GetValue") };
        var files = EmitAll(input, userImplemented: userImpl);
        Assert.Contains("public partial int GetValue()", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_Method_UserImplemented_NoStubBody()
    {
        var input = @"struct CFoo : public CNod { Integer GetValue(); };";
        var userImpl = new HashSet<(string, string)> { ("CFoo", "GetValue") };
        var files = EmitAll(input, userImplemented: userImpl);
        var src = files["CFoo.g.cs"];
        Assert.DoesNotContain("=> default!;", src);
        Assert.DoesNotContain("{ }", src);
    }

    [Fact]
    public void Emit_Method_UserImplemented_EndsWithSemicolon()
    {
        var input = @"struct CFoo : public CNod { Integer GetValue(); };";
        var userImpl = new HashSet<(string, string)> { ("CFoo", "GetValue") };
        var files = EmitAll(input, userImplemented: userImpl);
        Assert.Contains("GetValue();", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_Method_NotUserImplemented_RemainsStub()
    {
        var input = @"struct CFoo : public CNod { Integer GetValue(); };";
        var files = EmitAll(input);
        Assert.Contains("=> default!;", files["CFoo.g.cs"]);
        Assert.DoesNotContain("partial int GetValue", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_Method_PartialAndNonPartial_MixedInSameClass()
    {
        var input = @"struct CFoo : public CNod { Integer GetA(); Integer GetB(); };";
        var userImpl = new HashSet<(string, string)> { ("CFoo", "GetA") };
        var files = EmitAll(input, userImplemented: userImpl);
        var src = files["CFoo.g.cs"];
        Assert.Contains("public partial int GetA()", src);
        Assert.Contains("GetA();", src);
        Assert.Contains("public int GetB() => default!;", src);
    }

    [Fact]
    public void Emit_Method_UserImplemented_VoidMethod_PartialDeclaration()
    {
        var input = @"struct CFoo : public CNod { Void DoWork(); };";
        var userImpl = new HashSet<(string, string)> { ("CFoo", "DoWork") };
        var files = EmitAll(input, userImplemented: userImpl);
        Assert.Contains("public partial void DoWork();", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_Namespace_UserImplemented_StaticPartialDeclaration()
    {
        var input = "namespace MathLib { Integer Abs(Integer x); };";
        var userImpl = new HashSet<(string, string)> { ("MathLib", "Abs") };
        var files = EmitAll(input, userImplemented: userImpl);
        Assert.Contains("public static partial int Abs(", files["MathLib.g.cs"]);
        Assert.DoesNotContain("=> default!;", files["MathLib.g.cs"]);
    }

    // ────────── Declare-mode interfaces ──────────

    [Fact]
    public void Emit_DeclaredModes_LocalPersistent_AddsInterfaces()
    {
        var input = @"
/*!
* \brief Manialink class.
*
* Supported declare modes :
* - Local
* - Persistent
*/
struct CFoo : public CNod { };";
        var files = EmitAll(input);
        Assert.Contains("ILocalProvider", files["CFoo.g.cs"]);
        Assert.Contains("IPersistentProvider", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_DeclaredModes_Local_AddsExplicitLocalImplementation()
    {
        var input = @"
/*!
* \brief Some class.
*
* Supported declare modes :
* - Local
*/
struct CFoo : public CNod { };";
        var files = EmitAll(input);
        Assert.Contains("ILocalProvider.Local { get; } = [];", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_DeclaredModes_Persistent_AddsExplicitPersistentImplementation()
    {
        var input = @"
/*!
* \brief Some class.
*
* Supported declare modes :
* - Persistent
*/
struct CFoo : public CNod { };";
        var files = EmitAll(input);
        Assert.Contains("IPersistentProvider.Persistent { get; } = [];", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_DeclaredModes_Metadata_AddsExplicitMetadataImplementation()
    {
        var input = @"
/*!
* \brief Some class.
*
* Supported declare modes :
* - Metadata
*/
struct CFoo : public CNod { };";
        var files = EmitAll(input);
        Assert.Contains("IMetadataProvider", files["CFoo.g.cs"]);
        Assert.Contains("IMetadataProvider.Metadata { get; } = [];", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_NoDeclaredModes_NoProviderInterfaces()
    {
        var files = EmitAll("struct CFoo : public CNod { };");
        Assert.DoesNotContain("ILocalProvider", files["CFoo.g.cs"]);
        Assert.DoesNotContain("IPersistentProvider", files["CFoo.g.cs"]);
        Assert.DoesNotContain("IMetadataProvider", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_DeclaredModes_NetworkReadWrite_AddsMarkerInterfaces()
    {
        var input = @"
/*!
* \brief Some class.
*
* Supported declare modes :
* - NetworkRead
* - NetworkWrite
*/
struct CFoo : public CNod { };";
        var files = EmitAll(input);
        Assert.Contains("INetreadProvider", files["CFoo.g.cs"]);
        Assert.Contains("INetwriteProvider", files["CFoo.g.cs"]);
        // Single shared INetworkProvider.NetworkData implementation.
        Assert.Contains("INetworkProvider.NetworkData", files["CFoo.g.cs"]);
        Assert.DoesNotContain("INetreadProvider.NetworkData", files["CFoo.g.cs"]);
        Assert.DoesNotContain("INetwriteProvider.NetworkData", files["CFoo.g.cs"]);
    }

    [Fact]
    public void Emit_DeclaredModes_NetworkRead_InlineFormat()
    {
        // ManiaPlanet inline format with NetworkRead and NetworkWrite
        var input = @"
/*!
Supported declare modes : Local  NetworkRead  NetworkWrite 
*/
struct CFoo : public CNod { };";
        var files = EmitAll(input);
        Assert.Contains("ILocalProvider", files["CFoo.g.cs"]);
        Assert.Contains("INetreadProvider", files["CFoo.g.cs"]);
        Assert.Contains("INetwriteProvider", files["CFoo.g.cs"]);
        Assert.Contains("INetworkProvider.NetworkData", files["CFoo.g.cs"]);
        // Local's explicit impl still emitted separately.
        Assert.Contains("ILocalProvider.Local", files["CFoo.g.cs"]);
    }
}
