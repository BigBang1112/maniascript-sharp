using System;
using System.Linq;
using Xunit;

namespace ManiaScriptSharp.Generator.Tests;

public class ScriptEmitterTests : EmitterTestBase
{
    [Fact]
    public void Emit_IncludesAndExtends_OmitProjectRootNamespace()
    {
        const string code = """
            using ManiaScriptSharp;

            namespace MyProject.Libs { public class LayerLib : ILib { } }
            namespace MyProject.Modes
            {
                public class BaseMode : IContext
                {
                    public void Main() { }
                    public void Loop() { }
                }

                public class DerivedMode : BaseMode
                {
                    public MyProject.Libs.LayerLib Layers = new();
                }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "DerivedMode", rootNamespace: "MyProject");

        Assert.Empty(diagnostics);
        Assert.Contains("#Extends \"Modes/BaseMode.Script.txt\"", output);
        Assert.Contains("#Include \"Libs/LayerLib.Script.txt\" as Layers", output);
    }

    [Theory]
    [InlineData("MyProject.Modes", "MyProject.Modes", "Modes/LayerLib.Script.txt")]
    [InlineData("MyProject.Modes", "MyProject.Modes.Helpers", "Modes/Helpers/LayerLib.Script.txt")]
    [InlineData("MyProject.Modes.Nested", "MyProject.Libs", "Libs/LayerLib.Script.txt")]
    [InlineData("MyProject.Modes", "MyProject", "LayerLib.Script.txt")]
    public void Emit_LibFieldIncludesNamespacePathEvenWhenUnused(string scriptNamespace, string libNamespace,
        string expectedPath)
    {
        var code = $$"""
            using ManiaScriptSharp;

            namespace {{libNamespace}} { public class LayerLib : ILib { } }
            namespace {{scriptNamespace}}
            {
                public class MyMode : IContext
                {
                    private {{libNamespace}}.LayerLib layers;
                    public void Main() { }
                    public void Loop() { }
                }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "MyMode", rootNamespace: "MyProject");

        Assert.Empty(diagnostics);
        Assert.Contains($"#Include \"{expectedPath}\" as Layers", output);
    }

    [Fact]
    public void Emit_LibFieldInitializedInConstructor_EmitsOnlyInclude()
    {
        const string code = """
            using ManiaScriptSharp;

            public class LayerLib : ILib { }

            public class MyMode : IContext
            {
                private readonly LayerLib layers;

                public MyMode()
                {
                    layers = new();
                }

                public void Main() { }
                public void Loop() { }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "MyMode");

        Assert.Empty(diagnostics);
        Assert.Contains("#Include \"LayerLib.Script.txt\" as Layers", output);
        Assert.DoesNotContain("Layers =", output);
        Assert.DoesNotContain("MyMode()", output);
    }

    [Fact]
    public void Emit_LibFieldInitializedInline_EmitsOnlyInclude()
    {
        const string code = """
            using ManiaScriptSharp;

            public class LayerLib : ILib { }

            public class MyMode : IContext
            {
                private readonly LayerLib layers = new();

                public void Main() { }
                public void Loop() { }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "MyMode");

        Assert.Empty(diagnostics);
        Assert.Contains("#Include \"LayerLib.Script.txt\" as Layers", output);
        Assert.DoesNotContain("Layers =", output);
    }

    [Fact]
    public void Emit_MathAndMathFCalls_IncludeMathLibOnceWithoutField()
    {
        const string code = """
            using System;
            using ManiaScriptSharp;

            public class MyMode : IContext
            {
                public void Main()
                {
                    var absolute = Math.Abs(-5);
                    var sine = MathF.Sin(1f);
                }
                public void Loop() { }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "MyMode");

        Assert.Empty(diagnostics);
        Assert.Equal(1, output.Split("#Include \"MathLib\" as MathLib").Length - 1);
        Assert.Contains("MathLib::Abs(-5)", output);
        Assert.Contains("MathLib::Sin(1.)", output);
    }

    [Fact]
    public void Emit_MathCallWithMathLibField_UsesCanonicalAliasOnce()
    {
        const string code = """
            using System;
            using ManiaScriptSharp;

            namespace ManiaScriptSharp
            {
                public class MathLib : ILib
                {
                    public int Abs(int value) => value;
                }
            }

            public class MyMode : IContext
            {
                private MathLib math = new();

                public void Main()
                {
                    var first = Math.Abs(-5);
                    var second = math.Abs(-2);
                }
                public void Loop() { }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "MyMode");

        Assert.Empty(diagnostics);
        Assert.Equal(1, output.Split("#Include \"MathLib\" as MathLib").Length - 1);
        Assert.Contains("MathLib::Abs(-5)", output);
        Assert.Contains("MathLib::Abs(-2)", output);
    }

    [Fact]
    public void Emit_ManialinkMathCallsInHostAndInlinedLib_IncludeMathLibOnce()
    {
        const string code = """
            using System;
            using ManiaScriptSharp;

            public class HelperLib : ILib
            {
                public int Absolute(int value) => Math.Abs(value);
            }

            public class MyMode : IContext
            {
                private HelperLib helper = new();

                public void Main()
                {
                    var first = Math.Abs(-5);
                    var second = helper.Absolute(-2);
                }
                public void Loop() { }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "MyMode", isManialink: true);

        Assert.Empty(diagnostics);
        Assert.Equal(1, output.Split("#Include \"MathLib\" as MathLib").Length - 1);
        Assert.Contains("MathLib::Abs(-5)", output);
        Assert.Contains("MathLib::Abs(_Value)", output);
    }

    [Theory]
    [InlineData("var value = Math.PI;", "MathLib::PI()")]
    [InlineData("var value = (float)1;", "MathLib::ToReal(1)")]
    [InlineData("var value = Convert.ToInt32(1.5f);", "MathLib::NearestInteger(1.5)")]
    public void Emit_MathConstantAndNumericConversions_IncludeMathLibWithoutField(string statement, string expected)
    {
        var code = $$"""
            using System;
            using ManiaScriptSharp;

            public class MyMode : IContext
            {
                public void Main()
                {
                    {{statement}}
                }
                public void Loop() { }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "MyMode");

        Assert.Empty(diagnostics);
        Assert.Equal(1, output.Split("#Include \"MathLib\" as MathLib").Length - 1);
        Assert.Contains(expected, output);
    }

    [Fact]
    public void Emit_WithoutMathUsage_DoesNotIncludeMathLib()
    {
        const string code = """
            using System;
            using ManiaScriptSharp;

            public class MyMode : IContext
            {
                private const double Pi = Math.PI;
                public void Main() { var value = 1; }
                public void Loop() { }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "MyMode");

        Assert.Empty(diagnostics);
        Assert.DoesNotContain("#Include \"MathLib\"", output);
        Assert.Contains("#Const C_Pi", output);
    }

    [Fact]
    public void Emit_StringOperations_IncludeTextLibOnceWithoutField()
    {
        const string code = """
            using ManiaScriptSharp;

            public class MyMode : IContext
            {
                public void Main()
                {
                    string text = "hello";
                    var upper = text.ToUpper();
                    var length = text.Length;
                    var number = int.Parse("5");
                }
                public void Loop() { }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "MyMode");

        Assert.Empty(diagnostics);
        Assert.Equal(1, output.Split("#Include \"TextLib\" as TextLib").Length - 1);
        Assert.Contains("TextLib::ToUpperCase", output);
        Assert.Contains("TextLib::Length", output);
        Assert.Contains("TextLib::ToInteger(\"5\")", output);
    }

    [Fact]
    public void Emit_StringCallWithTextLibField_UsesCanonicalAliasOnce()
    {
        const string code = """
            using ManiaScriptSharp;

            namespace ManiaScriptSharp
            {
                public class TextLib : ILib
                {
                    public string ToUpperCase(string value) => value;
                }
            }

            public class MyMode : IContext
            {
                private TextLib text = new();

                public void Main()
                {
                    var first = "hello".ToUpper();
                    var second = text.ToUpperCase("world");
                }
                public void Loop() { }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "MyMode");

        Assert.Empty(diagnostics);
        Assert.Equal(1, output.Split("#Include \"TextLib\" as TextLib").Length - 1);
        Assert.Contains("TextLib::ToUpperCase(\"hello\")", output);
        Assert.Contains("TextLib::ToUpperCase(\"world\")", output);
    }

    [Fact]
    public void Emit_ManialinkStringCallsInHostAndInlinedLib_IncludeTextLibOnce()
    {
        const string code = """
            using ManiaScriptSharp;

            public class HelperLib : ILib
            {
                public string Upper(string value) => value.ToUpper();
            }

            public class MyMode : IContext
            {
                private HelperLib helper = new();

                public void Main()
                {
                    var first = "hello".ToUpper();
                    var second = helper.Upper("world");
                }
                public void Loop() { }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "MyMode", isManialink: true);

        Assert.Empty(diagnostics);
        Assert.Equal(1, output.Split("#Include \"TextLib\" as TextLib").Length - 1);
        Assert.Contains("TextLib::ToUpperCase(\"hello\")", output);
        Assert.Contains("TextLib::ToUpperCase(_Value)", output);
    }

    [Theory]
    [InlineData("var value = (int)\"5\";", "TextLib::ToInteger(\"5\")")]
    [InlineData("var value = (string)5;", "TextLib::ToText(5)")]
    [InlineData("var value = Convert.ToInt32(\"5\");", "TextLib::ToInteger(\"5\")")]
    [InlineData("var value = Convert.ToString(5);", "TextLib::ToText(5)")]
    public void Emit_TextConversions_IncludeTextLibWithoutField(string statement, string expected)
    {
        var code = $$"""
            using System;
            using ManiaScriptSharp;

            public class MyMode : IContext
            {
                public void Main()
                {
                    {{statement}}
                }
                public void Loop() { }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "MyMode");

        Assert.Empty(diagnostics);
        Assert.Equal(1, output.Split("#Include \"TextLib\" as TextLib").Length - 1);
        Assert.Contains(expected, output);
    }

    [Fact]
    public void Emit_StringOperationsWithoutTextLibCalls_DoNotIncludeTextLib()
    {
        const string code = """
            using ManiaScriptSharp;

            public class MyMode : IContext
            {
                public void Main()
                {
                    var empty = string.Empty;
                    var isEmpty = string.IsNullOrEmpty(empty);
                    var joined = string.Concat("a", "b");
                }
                public void Loop() { }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "MyMode");

        Assert.Empty(diagnostics);
        Assert.DoesNotContain("#Include \"TextLib\"", output);
        Assert.DoesNotContain("TextLib::", output);
    }

    [Fact]
    public void Emit_UserEnums_AsIntegerConstants()
    {
        const string code = """
            using ManiaScriptSharp;

            public enum Phase { Idle, Running = 5, Done, Alias = Running }

            public class EnumContext : IContext
            {
                private enum Choice : byte { No = 2, Yes }
                private Phase phase = Phase.Running;
                private Choice choice = Choice.Yes;

                public void Main()
                {
                    Phase next = Phase.Done;
                    if (next == Phase.Alias) choice = Choice.No;
                }

                public void Loop() { }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "EnumContext");

        Assert.Empty(diagnostics);
        Assert.Contains("#Const C_Choice_No 2", output);
        Assert.Contains("#Const C_Choice_Yes 3", output);
        Assert.Contains("#Const C_Phase_Idle 0", output);
        Assert.Contains("#Const C_Phase_Running 5", output);
        Assert.Contains("#Const C_Phase_Done 6", output);
        Assert.Contains("#Const C_Phase_Alias 5", output);
        Assert.Contains("declare Integer G_Phase", output);
        Assert.Contains("declare Integer G_Choice", output);
        Assert.Contains("Integer Next = C_Phase_Done", output);
        Assert.Contains("Next == C_Phase_Alias", output);
        Assert.Contains("G_Choice = C_Choice_No", output);
    }

    [Fact]
    public void Emit_UserEnum_SwitchCasesUseConstants()
    {
        const string code = """
            using ManiaScriptSharp;

            public enum Status { Ready, Started = 4 }

            public class SwitchContext : IContext
            {
                private Status status;
                public void Main()
                {
                    switch (status)
                    {
                        case Status.Ready: status = Status.Started; break;
                        default: break;
                    }
                }
                public void Loop() { }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "SwitchContext");

        Assert.Empty(diagnostics);
        Assert.Contains("#Const C_Status_Ready 0", output);
        Assert.Contains("#Const C_Status_Started 4", output);
        Assert.Contains("case C_Status_Ready:", output);
        Assert.Contains("G_Status = C_Status_Started", output);
    }

    [Fact]
    public void Emit_UserEnums_OmitNamespaceFromConstants()
    {
        const string code = """
            using ManiaScriptSharp;

            namespace First { public enum FirstState { Ready = 1 } }
            namespace Second { public enum SecondState { Ready = 2 } }

            public class NamespacedEnums : IContext
            {
                private First.FirstState first;
                private Second.SecondState second;
                public void Main()
                {
                    first = First.FirstState.Ready;
                    second = Second.SecondState.Ready;
                }
                public void Loop() { }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "NamespacedEnums");

        Assert.Empty(diagnostics);
        Assert.Contains("#Const C_FirstState_Ready 1", output);
        Assert.Contains("#Const C_SecondState_Ready 2", output);
        Assert.Contains("G_First = C_FirstState_Ready", output);
        Assert.Contains("G_Second = C_SecondState_Ready", output);
    }

    [Fact]
    public void Emit_LibEnum_UsesIncludeAliasFromConsumer()
    {
        const string code = """
            using ManiaScriptSharp;

            public class Palette : ILib<object>
            {
                public object Context => null!;
                public enum Tone { Light = 1, Dark = 2 }
            }

            public class LibEnumContext : IContext
            {
                private Palette colors = new();
                private int tone;
                public void Main() { tone = (int)Palette.Tone.Dark; }
                public void Loop() { }
            }
            """;

        var (libOutput, libDiagnostics) = EmitScript(code, "Palette");
        var (scriptOutput, scriptDiagnostics) = EmitScript(code, "LibEnumContext");

        Assert.Empty(libDiagnostics);
        Assert.Empty(scriptDiagnostics);
        Assert.Contains("#Const C_Tone_Light 1", libOutput);
        Assert.Contains("#Const C_Tone_Dark 2", libOutput);
        Assert.Contains("#Include \"Palette.Script.txt\" as Colors", scriptOutput);
        Assert.Contains("Colors::C_Tone_Dark", scriptOutput);
        Assert.DoesNotContain("#Const C_Tone_Dark", scriptOutput);
    }

    [Fact]
    public void Emit_Manialink_InlinesLibEnumConstants()
    {
        const string code = """
            using ManiaScriptSharp;

            public class Palette : ILib<object>
            {
                public object Context => null!;
                public enum Tone { Light = 1, Dark = 2 }
            }

            public class LibEnumManialink : IContext
            {
                public Palette palette = new();
                private int tone;
                public void Main() { tone = (int)Palette.Tone.Dark; }
                public void Loop() { }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "LibEnumManialink", isManialink: true);

        Assert.Empty(diagnostics);
        Assert.Contains("#Const C_Tone_Dark 2", output);
        Assert.Contains("G_Tone = C_Tone_Dark", output);
        Assert.DoesNotContain("Palette::C_Tone_Dark", output);
    }

    [Fact]
    public void Emit_FieldsAlwaysUseGPrefixAndPublicFieldsWarn()
    {
        const string code = """
            using ManiaScriptSharp;

            public class FieldVisibility : IContext
            {
                private int privateState;
                protected int protectedState;
                protected internal int sharedState;
                public int publicState;
                [ManialinkControl(IgnoreValidation = true)]
                public object Control;
                public int Exposed { get; set; }
                private int camelCase { get; set; }
                private int _case { get; set; }

                public void Main() { }
                public void Loop() { }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "FieldVisibility");

        Assert.Contains("declare Integer G_PrivateState;", output);
        Assert.Contains("declare Integer G_ProtectedState;", output);
        Assert.Contains("declare Integer G_SharedState;", output);
        Assert.Contains("declare Integer G_PublicState;", output);
        Assert.Contains("declare Text G_Control;", output);
        Assert.Contains("declare Integer G_Exposed;", output);
        Assert.Contains("declare Integer G_CamelCase;", output);
        Assert.Contains("declare Integer G_Case;", output);
        Assert.Contains("Integer GetExposed() {", output);
        Assert.Contains(diagnostics, d => d.Id == "MSS016" && d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
        Assert.DoesNotContain(diagnostics, d => d.Id == "MSS016" && d.GetMessage().Contains("Control"));
        Assert.DoesNotContain(diagnostics, d => d.Id == "MSS016" && d.GetMessage().Contains("Exposed"));
    }

    [Fact]
    public void Emit_StructFieldNaming_UsesConfiguredCasing()
    {
        const string code = """
            using ManiaScriptSharp;

            [StructFieldNaming(StructFieldNameStyle.CamelCase)]
            public struct CamelState { public int TotalScore; }

            [StructFieldNaming(StructFieldNameStyle.SnakeCase)]
            public struct SnakeState { public int PlayerURL; }

            [StructFieldNaming(StructFieldNameStyle.SnakeCase)]
            public struct JsonNamedState
            {
                [System.Text.Json.Serialization.JsonPropertyName("score")]
                public int TotalScore;
            }

            public class StructFieldNamingContext : IContext
            {
                public void Main() { }
                public void Loop() { }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "StructFieldNamingContext");

        Assert.Empty(diagnostics);
        Assert.Contains("#Struct CamelState {\n  Integer totalScore;\n}", output);
        Assert.Contains("#Struct SnakeState {\n  Integer player_url;\n}", output);
        Assert.Contains("#Struct JsonNamedState {\n  Integer score;\n}", output);
    }

    [Fact]
    public void Emit_Structs_DeclareFieldTypesBeforeTheirUsers()
    {
        const string code = """
            using System.Collections.Generic;
            using ManiaScriptSharp;

            public struct Parent
            {
                public Child Child;
                public List<Leaf> Leaves;
            }

            public struct Unrelated { public int Value; }
            public struct Child { public Leaf Leaf; }
            public struct Leaf { public int Value; }

            public class StructOrderContext : IContext
            {
                public void Main() { }
                public void Loop() { }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "StructOrderContext");

        Assert.Empty(diagnostics);
        Assert.True(output.IndexOf("#Struct Unrelated {", StringComparison.Ordinal)
            < output.IndexOf("#Struct Leaf {", StringComparison.Ordinal));
        Assert.True(output.IndexOf("#Struct Leaf {", StringComparison.Ordinal)
            < output.IndexOf("#Struct Child {", StringComparison.Ordinal));
        Assert.True(output.IndexOf("#Struct Child {", StringComparison.Ordinal)
            < output.IndexOf("#Struct Parent {", StringComparison.Ordinal));
        Assert.Contains("Leaf[] Leaves;", output);
    }

    [Fact]
    public void Emit_HiddenSetting_OverridesDisplayAndTranslation()
    {
        const string code = """
            using ManiaScriptSharp;

            public class Settings
            {
                [Setting(As = "Visible name", Translated = true, Hidden = true)]
                public const int InternalValue = 25;
            }
            """;

        var (output, diagnostics) = EmitScript(code, "Settings");

        Assert.Empty(diagnostics);
        Assert.Contains("#Setting S_InternalValue 25 as \"<hidden>\"", output);
        Assert.DoesNotContain("Visible name", output);
        Assert.DoesNotContain("_(\"<hidden>\")", output);
    }

    [Fact]
    public void Emit_CommandAttributes_EmitTypedCommandDirectives()
    {
        const string code = """
            using ManiaScriptSharp;

            [Command("Command_SetPause", typeof(bool), As = "Pause the game")]
            [Command("Command_SetRound", typeof(int), Translated = false)]
            public class Commands
            {
            }
            """;

        var (output, diagnostics) = EmitScript(code, "Commands");

        Assert.Empty(diagnostics);
        Assert.Contains("#Command Command_SetPause (Boolean) as _(\"Pause the game\")", output);
        Assert.Contains("#Command Command_SetRound (Integer) as \"Command_SetRound\"", output);
    }

    [Fact]
    public void Emit_LabelOverride_OmitsBaseCallBecauseTheLabelIsAlreadyAssembled()
    {
        const string code = """
            using ManiaScriptSharp;

            public class BaseMode : IContext
            {
                public virtual void AfterStart() { }
                public void Main() { }
                public void Loop() { }
            }

            public class ExtendedMode : BaseMode
            {
                public override void AfterStart()
                {
                    base.AfterStart();
                    int updateCount = 1;
                }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "ExtendedMode");

        Assert.Empty(diagnostics);
        Assert.Contains("***AfterStart***", output);
        Assert.Contains("declare Integer UpdateCount = 1;", output);
        Assert.DoesNotContain("AfterStart();", output);
    }

    [Fact]
    public void Emit_LabelOverride_BaseCallAfterAnotherStatement_ReportsDiagnostic()
    {
        const string code = """
            using ManiaScriptSharp;

            public class BaseMode : IContext
            {
                public virtual void AfterStart() { }
                public void Main() { }
                public void Loop() { }
            }

            public class ExtendedMode : BaseMode
            {
                public override void AfterStart()
                {
                    int updateCount = 1;
                    base.AfterStart();
                }
            }
            """;

        var (_, diagnostics) = EmitScript(code, "ExtendedMode");

        Assert.Contains(diagnostics, d => d.Id == "MSS015");
    }

    [Fact]
    public void Emit_Lib_DeclaresFieldsAutoPropertiesConstantsAndStructs()
    {
        const string code = """
            using ManiaScriptSharp;

            public struct CounterState
            {
                public int Value;
            }

            public class CounterLib : ILib<object>
            {
                public object Context => null!;
                public int Counter;
                private string _state;
                public int Score { get; set; }
                public const int Limit = 5;

                public int Increment()
                {
                    Counter++;
                    return Counter;
                }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "CounterLib");

        Assert.Contains(diagnostics, d => d.Id == "MSS016" && d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
        Assert.Contains("#Struct CounterState {", output);
        Assert.Contains("#Const C_Limit 5", output);
        Assert.Contains("declare Integer G_Counter;", output);
        Assert.Contains("declare Text G_State;", output);
        Assert.Contains("declare Integer G_Score;", output);
        Assert.Contains("G_Counter += 1;", output);
        Assert.Contains("return G_Counter;", output);
        Assert.Equal(1, output.Split("declare Integer G_Score;").Length - 1);
        Assert.DoesNotContain("G_Context", output);
        Assert.DoesNotContain("#RequireContext", output);
    }

    [Fact]
    public void Emit_Lib_CanInheritItsContextWithoutEmittingDirectives()
    {
        const string code = """
            using ManiaScriptSharp;

            public class CMap
            {
                public string AuthorNickName => "";
            }

            public class MapDetails : CMap, ILib
            {
                public string GetAuthor() => AuthorNickName;
            }
            """;

        var (output, diagnostics) = EmitScript(code, "MapDetails");

        Assert.Empty(diagnostics);
        Assert.Contains("Text GetAuthor() {", output);
        Assert.Contains("return AuthorNickName;", output);
        Assert.DoesNotContain("#RequireContext", output);
        Assert.DoesNotContain("#Extends", output);
        Assert.DoesNotContain("main()", output);
    }

    [Fact]
    public void Emit_Lib_LoopMethod_IsEmittedAsARegularFunction()
    {
        const string code = """
            using ManiaScriptSharp;

            public class CounterLib : ILib
            {
                public void Loop()
                {
                    ManiaScript.Log("tick");
                }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "CounterLib");

        Assert.Empty(diagnostics);
        Assert.Contains("Void Loop() {", output);
        Assert.Contains("log(\"tick\");", output);
    }

    [Fact]
    public void Emit_Lib_MainMethod_IsEmittedAsARegularFunction()
    {
        const string code = """
            using ManiaScriptSharp;

            public class CounterLib : ILib
            {
                public void Main()
                {
                    ManiaScript.Log("start");
                }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "CounterLib");

        Assert.Empty(diagnostics);
        Assert.Contains("Void Main() {", output);
        Assert.Contains("log(\"start\");", output);
    }

    [Fact]
    public void Emit_Lib_FieldInitializerReportsDiagnosticAndEmitsBareDeclare()
    {
        const string code = """
            using ManiaScriptSharp;

            public class CounterLib : ILib<object>
            {
                public object Context => null!;
                public int Counter = 3;
            }
            """;

        var (output, diagnostics) = EmitScript(code, "CounterLib");

        Assert.Contains("declare Integer G_Counter;", output);
        Assert.DoesNotContain("G_Counter = 3", output);
        Assert.Contains(diagnostics, d => d.Id == "MSS012");
        Assert.Contains(diagnostics, d => d.Id == "MSS016");
    }

    [Fact]
    public void Emit_Lib_AllowedFieldInitializersAreNotEmittedInline()
    {
        const string code = """
            using System.Collections.Generic;
            using ManiaScriptSharp;

            public class StateLib : ILib<object>
            {
                public object Context => null!;
                private readonly Dictionary<string, Ident> ByName = new();
                private readonly IList<Ident> Events = [];
                public string Empty = "";
                private string EmptyFromStatic = string.Empty;
                private string NonEmpty = "value";
                public int Number = 3;
            }
            """;

        var (output, diagnostics) = EmitScript(code, "StateLib");

        Assert.Contains("declare Ident[Text] G_ByName;", output);
        Assert.Contains("declare Ident[] G_Events;", output);
        Assert.Contains("declare Text G_Empty;", output);
        Assert.Contains("declare Text G_EmptyFromStatic;", output);
        Assert.Contains("declare Text G_NonEmpty;", output);
        Assert.Contains("declare Integer G_Number;", output);
        Assert.DoesNotContain("G_ByName = []", output);
        Assert.DoesNotContain("G_Events = []", output);
        Assert.DoesNotContain("G_Empty = \"\"", output);
        Assert.Equal(2, diagnostics.Count(d => d.Id == "MSS012"));
        Assert.Equal(2, diagnostics.Count(d => d.Id == "MSS016"));
    }

    [Fact]
    public void Emit_ContextFieldInitializers_AreDeferredFromBareGlobalDeclarations()
    {
        const string code = """
            using System.Collections.Generic;
            using ManiaScriptSharp;

            public class StateContext : IContext
            {
                private readonly Dictionary<string, Ident> ByName = new();
                private string Empty = "";
                private string Banner { get; set; } = "";
            }
            """;

        var (output, diagnostics) = EmitScript(code, "StateContext");

        Assert.Empty(diagnostics);
        Assert.Contains("declare Ident[Text] G_ByName;", output);
        Assert.Contains("declare Text G_Empty;", output);
        Assert.Contains("declare Text G_Banner;", output);
        Assert.DoesNotContain("declare Ident[Text] G_ByName = [];", output);
        Assert.DoesNotContain("declare Text G_Empty = \"\";", output);
        Assert.Contains("main() {\n  G_ByName = [];\n  G_Empty = \"\";\n  G_Banner = \"\";\n}", output);
    }

    [Fact]
    public void Emit_ContextDictionaryInitializer_RemainsSupported()
    {
        const string code = """
            using System.Collections.Generic;
            using ManiaScriptSharp;

            public class StateContext : IContext
            {
                private readonly Dictionary<string, int> Scores = new()
                {
                    ["alpha"] = 10,
                    ["beta"] = 20,
                };
            }
            """;

        var (output, diagnostics) = EmitScript(code, "StateContext");

        Assert.Empty(diagnostics);
        Assert.Contains("declare Integer[Text] G_Scores;", output);
        Assert.Contains("G_Scores = [\"alpha\" => 10, \"beta\" => 20];", output);
    }

    [Fact]
    public void Emit_Consumer_UsesQualifiedNestedLibraryStructWithoutAlias()
    {
        const string code = """
            using ManiaScriptSharp;

            public class StateLib : ILib<object>
            {
                public object Context => null!;
                public struct Snapshot
                {
                    public int Count;
                }

                public Snapshot GetSnapshot() => new();
            }

            public class Host
            {
                public StateLib Lib = null!;
                public StateLib.Snapshot Current;
                public System.Collections.Generic.List<StateLib.Snapshot> History;
                public StateLib.Snapshot Echo(StateLib.Snapshot snapshot) => snapshot;
                public void Main()
                {
                    Current = Lib.GetSnapshot();
                    StateLib.Snapshot next = new() { Count = 1 };
                }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "Host");

        Assert.Contains(diagnostics, d => d.Id == "MSS016" && d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
        Assert.DoesNotContain("#Struct Lib::Snapshot as Snapshot", output);
        Assert.Contains("declare Lib::Snapshot G_Current;", output);
        Assert.Contains("declare Lib::Snapshot[] G_History;", output);
        Assert.Contains("Lib::Snapshot Echo(", output);
        Assert.Contains("declare Lib::Snapshot Next = Lib::Snapshot { Count = 1 };", output);
        Assert.DoesNotContain("#Struct Snapshot {", output);
    }

    [Fact]
    public void Emit_Consumer_ImportsNestedLibraryStructWithExplicitUsingAlias()
    {
        const string code = """
            using ManiaScriptSharp;
            using LocalSnapshot = StateLib.Snapshot;

            public class StateLib : ILib
            {
                public struct Snapshot { public int Count; }
                public Snapshot GetSnapshot() => new();
            }

            public class Host
            {
                private StateLib lib = new();
                private LocalSnapshot current;

                public LocalSnapshot GetCurrent() => current;
                public void Main()
                {
                    current = new LocalSnapshot { Count = 1 };
                }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "Host");

        Assert.Empty(diagnostics);
        Assert.Contains("#Include \"StateLib.Script.txt\" as Lib", output);
        Assert.Contains("#Struct Lib::Snapshot as LocalSnapshot", output);
        Assert.Contains("declare LocalSnapshot G_Current;", output);
        Assert.Contains("LocalSnapshot GetCurrent()", output);
        Assert.Contains("LocalSnapshot { Count = 1 }", output);
    }

    [Fact]
    public void Emit_Consumer_LibFieldAccessReportsDiagnostic()
    {
        const string code = """
            using ManiaScriptSharp;

            public class CounterLib : ILib<object>
            {
                public object Context => null!;
                public int Counter;
            }

            public class Host
            {
                public CounterLib Lib = null!;
                public int Read() => Lib.Counter;
                public void Write() => Lib.Counter = 1;
            }
            """;

        var (output, diagnostics) = EmitScript(code, "Host");

        Assert.DoesNotContain("Lib::G_Counter", output);
        Assert.Equal(2, diagnostics.Count(d => d.Id == "MSS013"));
    }

    [Fact]
    public void Emit_Consumer_LibConstAndPropertyUseExposedNames()
    {
        const string code = """
            using ManiaScriptSharp;

            public class CounterLib : ILib<object>
            {
                public object Context => null!;
                public const int Limit = 5;
                [Setting]
                public const int InitialScore = 3;
                public int Score { get; set; }
                public static int GetLimit() => Limit;
            }

            public class Host
            {
                public CounterLib Included = null!;
                public int ReadLimit() => CounterLib.Limit;
                public int ReadSetting() => CounterLib.InitialScore;
                public int ReadStaticMethod() => CounterLib.GetLimit();
                public int ReadScore() => Included.Score;
                public void WriteScore() => Included.Score = 7;
            }
            """;

        var (output, diagnostics) = EmitScript(code, "Host");

        Assert.Empty(diagnostics);
        Assert.Contains("#Include \"CounterLib.Script.txt\" as Included", output);
        Assert.DoesNotContain("#Const C_Limit", output);
        Assert.DoesNotContain("#Setting S_InitialScore", output);
        Assert.Contains("return Included::C_Limit;", output);
        Assert.Contains("return Included::S_InitialScore;", output);
        Assert.Contains("return Included::GetLimit();", output);
        Assert.Contains("return Included::GetScore();", output);
        Assert.Contains("Included::SetScore(7);", output);
    }

    [Fact]
    public void Translate_GeneratedLibConstPreservesOriginalName()
    {
        var output = TranslateExprFromGeneratedSource(
            "Result = GeneratedLib.Version",
            "public string Result; public class GeneratedLib : ManiaScriptSharp.ILib<object> { public object Context => null!; public const string Version = \"1\"; }");

        Assert.Equal("G_Result = GeneratedLib::Version", output);
    }

    [Fact]
    public void Emit_Manialink_InlinesLibGlobalsAndUsesBareMangledName()
    {
        const string code = """
            using ManiaScriptSharp;

            public class CounterLib : ILib<object>
            {
                public object Context => null!;
                public int Counter;
                public int Score { get; set; }

                public void Increment()
                {
                    Counter++;
                }
            }

            public class Host
            {
                public CounterLib Lib = null!;
                public void Main()
                {
                    Lib.Increment();
                    Lib.Score = 9;
                }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "Host", isManialink: true);

        Assert.Empty(diagnostics);
        Assert.Contains("// Inlined lib: CounterLib", output);
        Assert.Contains("declare Integer G_Counter;", output);
        Assert.Contains("G_Counter += 1;", output);
        Assert.Contains("Increment();", output);
        Assert.Contains("SetScore(9);", output);
        Assert.DoesNotContain("Lib::Increment", output);
        Assert.DoesNotContain("Lib::SetScore", output);
    }
}
