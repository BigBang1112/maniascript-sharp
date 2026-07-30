using System.Linq;
using Xunit;

namespace ManiaScriptSharp.ApiGenerator.Tests;

public class HeaderParserTests
{
    [Fact]
    public void Parse_EmptyInput_ReturnsEmptyHeader()
    {
        var parser = new HeaderParser("");
        var result = parser.Parse();
        Assert.Empty(result.Types);
        Assert.Empty(result.TopLevelEnums);
    }

    [Fact]
    public void Parse_SimpleStruct_ExtractsName()
    {
        var input = "struct CFoo : public CNod { };";
        var result = new HeaderParser(input).Parse();
        Assert.Single(result.Types);
        Assert.Equal("CFoo", result.Types[0].Name);
        Assert.Equal("CNod", result.Types[0].Base);
    }

    [Fact]
    public void Parse_Class_ExtractsName()
    {
        var input = "class CBar : public CBaz { };";
        var result = new HeaderParser(input).Parse();
        Assert.Single(result.Types);
        Assert.Equal("CBar", result.Types[0].Name);
        Assert.Equal("CBaz", result.Types[0].Base);
    }

    [Fact]
    public void Parse_StructNoBase_BaseIsNull()
    {
        var input = "struct CSimple { };";
        var result = new HeaderParser(input).Parse();
        Assert.Single(result.Types);
        Assert.Null(result.Types[0].Base);
    }

    [Fact]
    public void Parse_Field_ExtractsMember()
    {
        var input = @"
struct CPlayer : public CNod {
    const Integer Score;
};";
        var result = new HeaderParser(input).Parse();
        Assert.Single(result.Types);
        Assert.Single(result.Types[0].Members);
        var m = result.Types[0].Members[0];
        Assert.Equal(MemberKind.Field, m.Kind);
        Assert.Equal("Score", m.Name);
        Assert.Equal("Integer", m.ReturnType);
    }

    [Fact]
    public void Parse_Method_ExtractsMember()
    {
        var input = @"
struct CMode : public CNod {
    Void PassOn(CSmModeEvent Event);
};";
        var result = new HeaderParser(input).Parse();
        var m = result.Types[0].Members[0];
        Assert.Equal(MemberKind.Method, m.Kind);
        Assert.Equal("PassOn", m.Name);
        Assert.Equal("Void", m.ReturnType);
        Assert.Single(m.Parameters);
        Assert.Equal("CSmModeEvent", m.Parameters[0].Type);
        Assert.Equal("Event", m.Parameters[0].Name);
    }

    [Fact]
    public void Parse_MethodMultipleParams_ExtractsAll()
    {
        var input = @"
struct CFoo : public CNod {
    Integer Add(Integer A,Integer B);
};";
        var result = new HeaderParser(input).Parse();
        var m = result.Types[0].Members[0];
        Assert.Equal(2, m.Parameters.Count);
        Assert.Equal("A", m.Parameters[0].Name);
        Assert.Equal("B", m.Parameters[1].Name);
    }

    [Fact]
    public void Parse_NestedEnum_Extracted()
    {
        var input = @"
struct CMode : public CNod {
    enum EWeapon {
        Laser,
        Rocket,
    };
};";
        var result = new HeaderParser(input).Parse();
        Assert.Single(result.Types[0].NestedEnums);
        var e = result.Types[0].NestedEnums[0];
        Assert.Equal("EWeapon", e.Name);
        Assert.Equal(2, e.Values.Count);
        Assert.Contains("Laser", e.Values);
        Assert.Contains("Rocket", e.Values);
    }

    [Fact]
    public void Parse_TopLevelEnum_Extracted()
    {
        var input = @"
enum EColor {
    Red,
    Green,
    Blue,
};";
        var result = new HeaderParser(input).Parse();
        Assert.Single(result.TopLevelEnums);
        Assert.Equal("EColor", result.TopLevelEnums[0].Name);
        Assert.Equal(3, result.TopLevelEnums[0].Values.Count);
    }

    [Fact]
    public void Parse_DocComment_ExtractsDoc()
    {
        var input = @"
/*! My class description */
struct CDoc : public CNod { };";
        var result = new HeaderParser(input).Parse();
        Assert.Equal("My class description", result.Types[0].Doc);
    }

    [Fact]
    public void Parse_ArrayField_DetectedAsArray()
    {
        var input = @"
struct CFoo : public CNod {
    Integer[] Scores;
};";
        var result = new HeaderParser(input).Parse();
        var m = result.Types[0].Members[0];
        Assert.True(m.IsArray);
        Assert.Equal("Integer", m.ReturnType);
    }

    [Fact]
    public void Parse_TrackmaniaArraySyntax_DetectedAsArray()
    {
        var input = @"
class CFoo : public CNod {
public :
    Array<CSmPlayer* const > Players;
};";
        var result = new HeaderParser(input).Parse();
        var m = result.Types[0].Members[0];
        Assert.True(m.IsArray);
        Assert.Equal("CSmPlayer", m.ReturnType);
    }

    [Fact]
    public void Parse_AssociativeArray_DetectedAsDict()
    {
        var input = @"
class CFoo : public CNod {
public :
    AssociativeArray<Text, Integer> Data;
};";
        var result = new HeaderParser(input).Parse();
        var m = result.Types[0].Members[0];
        Assert.True(m.IsDictionary);
        Assert.Equal("Integer", m.ReturnType);
        Assert.Equal("Text", m.DictKey);
    }

    [Fact]
    public void Parse_Template_Skipped()
    {
        var input = @"
template <typename T>
struct Array {
    T get(Integer Index);
    Integer count;
};

struct CReal : public CNod { };";
        var result = new HeaderParser(input).Parse();
        Assert.Single(result.Types);
        Assert.Equal("CReal", result.Types[0].Name);
    }

    [Fact]
    public void Parse_MultipleTypes_AllExtracted()
    {
        var input = @"
struct CA : public CNod { };
struct CB : public CA { };
struct CC : public CB { };";
        var result = new HeaderParser(input).Parse();
        Assert.Equal(3, result.Types.Count);
    }

    [Fact]
    public void Parse_EnumWithEqualsValue_StripsValue()
    {
        var input = @"
enum ETest {
    A = 0,
    B = 1,
    C = 2,
};";
        var result = new HeaderParser(input).Parse();
        Assert.Equal(new[] { "A", "B", "C" }, result.TopLevelEnums[0].Values);
    }

    [Fact]
    public void Parse_AccessLabels_Ignored()
    {
        var input = @"
class CTest : public CNod {
public :
    Integer X;
private :
    Integer Y;
};";
        var result = new HeaderParser(input).Parse();
        Assert.Equal(2, result.Types[0].Members.Count);
    }

    [Fact]
    public void Parse_Namespace_IsExtractedAsType()
    {
        var input = "namespace MathLib {\n    Integer Abs(Integer Argument1);\n};";
        var result = new HeaderParser(input).Parse();
        Assert.Single(result.Types);
        Assert.Equal("MathLib", result.Types[0].Name);
    }

    [Fact]
    public void Parse_Namespace_IsNamespaceFlagSet()
    {
        var input = "namespace MathLib {\n    Integer Abs(Integer Argument1);\n};";
        var result = new HeaderParser(input).Parse();
        Assert.True(result.Types[0].IsNamespace);
    }

    [Fact]
    public void Parse_Namespace_BaseIsNull()
    {
        var input = "namespace MathLib { Integer Abs(Integer X); };";
        var result = new HeaderParser(input).Parse();
        Assert.Null(result.Types[0].Base);
    }

    [Fact]
    public void Parse_Namespace_ExtractsMethods()
    {
        var input = @"
namespace MathLib {
    Integer Abs(Integer Argument1);
    Real Sin(Real Argument1);
};";
        var result = new HeaderParser(input).Parse();
        var ns = result.Types[0];
        Assert.Equal(2, ns.Members.Count);
        Assert.All(ns.Members, m => Assert.Equal(MemberKind.Method, m.Kind));
    }

    [Fact]
    public void Parse_Namespace_MethodNamesCorrect()
    {
        var input = "namespace MathLib { Integer Abs(Integer X); Real Sin(Real X); };";
        var result = new HeaderParser(input).Parse();
        var ns = result.Types[0];
        Assert.Equal("Abs", ns.Members[0].Name);
        Assert.Equal("Sin", ns.Members[1].Name);
    }

    [Fact]
    public void Parse_Namespace_MethodReturnTypes()
    {
        var input = "namespace MathLib { Integer Abs(Integer X); Real Sin(Real X); };";
        var result = new HeaderParser(input).Parse();
        var ns = result.Types[0];
        Assert.Equal("Integer", ns.Members[0].ReturnType);
        Assert.Equal("Real", ns.Members[1].ReturnType);
    }

    [Fact]
    public void Parse_Namespace_ConstFieldWithInitializer_ExtractsName()
    {
        var input = "namespace MathLib { const Real Pi = 3.14159; };";
        var result = new HeaderParser(input).Parse();
        var ns = result.Types[0];
        Assert.Single(ns.Members);
        Assert.Equal(MemberKind.Field, ns.Members[0].Kind);
        Assert.Equal("Pi", ns.Members[0].Name);
        Assert.Equal("Real", ns.Members[0].ReturnType);
    }

    [Fact]
    public void Parse_Namespace_MultipleConstFields_AllExtracted()
    {
        var input = "namespace MathLib { const Real Pi = 3.14159; const Real Tau = 6.28319; };";
        var result = new HeaderParser(input).Parse();
        var ns = result.Types[0];
        Assert.Equal(2, ns.Members.Count);
        Assert.Equal("Pi", ns.Members[0].Name);
        Assert.Equal("Tau", ns.Members[1].Name);
    }

    [Fact]
    public void Parse_Namespace_ArrayReturnType_DetectedAsArray()
    {
        var input = "namespace TextLib { Text[Void] Split(Text Separators, Text Text_); };";
        var result = new HeaderParser(input).Parse();
        var m = result.Types[0].Members[0];
        Assert.True(m.IsArray);
        Assert.Equal("Text", m.ReturnType);
    }

    [Fact]
    public void Parse_Namespace_ArrayParam_DetectedAsArray()
    {
        var input = "namespace TextLib { Text Join(Text Separator, Text[Void] Texts); };";
        var result = new HeaderParser(input).Parse();
        var m = result.Types[0].Members[0];
        Assert.Single(m.Parameters.Where(p => p.IsArray));
        Assert.Equal("Texts", m.Parameters[1].Name);
        Assert.True(m.Parameters[1].IsArray);
    }

    [Fact]
    public void Parse_Namespace_DocComment_Extracted()
    {
        var input = @"
/*! Standard math. */
namespace MathLib {
    Integer Abs(Integer X);
};";
        var result = new HeaderParser(input).Parse();
        Assert.Equal("Standard math.", result.Types[0].Doc);
    }

    [Fact]
    public void Parse_Namespace_StructsAndNamespacesCoexist()
    {
        var input = @"
struct CFoo : public CNod { };
namespace MathLib { Integer Abs(Integer X); };";
        var result = new HeaderParser(input).Parse();
        Assert.Equal(2, result.Types.Count);
        Assert.False(result.Types[0].IsNamespace);
        Assert.True(result.Types[1].IsNamespace);
    }

    [Fact]
    public void Parse_RegularStruct_IsNamespaceFalse()
    {
        var input = "struct CFoo : public CNod { };";
        var result = new HeaderParser(input).Parse();
        Assert.False(result.Types[0].IsNamespace);
    }

    // ────────── Supported declare modes ──────────

    [Fact]
    public void Parse_DeclaredModes_LocalAndPersistent()
    {
        var input = @"
/*! 
* \brief Some class.
*
* Supported declare modes :
* - Local
* - Persistent
*/
struct CFoo : public CNod { };";
        var result = new HeaderParser(input).Parse();
        Assert.Contains("Local", result.Types[0].DeclaredModes);
        Assert.Contains("Persistent", result.Types[0].DeclaredModes);
    }

    [Fact]
    public void Parse_DeclaredModes_SingleMode()
    {
        var input = @"
/*!
* \brief Some class.
*
* Supported declare modes :
* - Metadata
*/
struct CFoo : public CNod { };";
        var result = new HeaderParser(input).Parse();
        Assert.Contains("Metadata", result.Types[0].DeclaredModes);
        Assert.DoesNotContain("Local", result.Types[0].DeclaredModes);
    }

    [Fact]
    public void Parse_NoDeclaredModes_EmptySet()
    {
        var input = "struct CFoo : public CNod { };";
        var result = new HeaderParser(input).Parse();
        Assert.Empty(result.Types[0].DeclaredModes);
    }

    [Fact]
    public void Parse_DeclaredModes_InlineFormat_LocalPersistent()
    {
        // ManiaPlanet inline format: "Supported declare modes : Local  Persistent"
        var input = @"
/*!
Supported declare modes : Local  Persistent
This is the base Manialink page interface.
*/
struct CFoo : public CNod { };";
        var result = new HeaderParser(input).Parse();
        Assert.Contains("Local", result.Types[0].DeclaredModes);
        Assert.Contains("Persistent", result.Types[0].DeclaredModes);
    }

    [Fact]
    public void Parse_DeclaredModes_InlineFormat_SingleMode()
    {
        var input = @"
/*!
Supported declare modes : Local 
*/
struct CFoo : public CNod { };";
        var result = new HeaderParser(input).Parse();
        Assert.Contains("Local", result.Types[0].DeclaredModes);
        Assert.DoesNotContain("Persistent", result.Types[0].DeclaredModes);
    }
}