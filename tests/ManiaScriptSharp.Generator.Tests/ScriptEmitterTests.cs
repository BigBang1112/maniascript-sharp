using System.Linq;
using Xunit;

namespace ManiaScriptSharp.Generator.Tests;

public class ScriptEmitterTests : EmitterTestBase
{
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
        Assert.Contains("declare Integer G_Exposed;", output);
        Assert.Contains("declare Integer G_CamelCase;", output);
        Assert.Contains("declare Integer G_Case;", output);
        Assert.Contains("Integer GetExposed() {", output);
        Assert.Contains(diagnostics, d => d.Id == "MSS016" && d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
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
        Assert.Contains("#Struct CamelState {\n    Integer totalScore;\n}", output);
        Assert.Contains("#Struct SnakeState {\n    Integer player_url;\n}", output);
        Assert.Contains("#Struct JsonNamedState {\n    Integer score;\n}", output);
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
    public void Emit_Lib_FieldInitializersAreNeverEmittedInline()
    {
        const string code = """
            using System.Collections.Generic;
            using ManiaScriptSharp;

            public class StateLib : ILib<object>
            {
                public object Context => null!;
                private readonly Dictionary<string, Ident> ByName = new();
                public string Empty = "";
                public int Number = 3;
            }
            """;

        var (output, diagnostics) = EmitScript(code, "StateLib");

        Assert.Contains("declare Ident[Text] G_ByName;", output);
        Assert.Contains("declare Text G_Empty;", output);
        Assert.Contains("declare Integer G_Number;", output);
        Assert.DoesNotContain("G_ByName = []", output);
        Assert.DoesNotContain("G_Empty = \"\"", output);
        Assert.Equal(3, diagnostics.Count(d => d.Id == "MSS012"));
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
            }
            """;

        var (output, diagnostics) = EmitScript(code, "StateContext");

        Assert.Empty(diagnostics);
        Assert.Contains("declare Ident[Text] G_ByName;", output);
        Assert.Contains("declare Text G_Empty;", output);
        Assert.DoesNotContain("declare Ident[Text] G_ByName = [];", output);
        Assert.DoesNotContain("declare Text G_Empty = \"\";", output);
        Assert.Contains("main() {\n    G_ByName = [];\n    G_Empty = \"\";\n}", output);
    }

    [Fact]
    public void Emit_Consumer_ImportsNestedLibraryStruct()
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
                public void Main()
                {
                    Current = Lib.GetSnapshot();
                }
            }
            """;

        var (output, diagnostics) = EmitScript(code, "Host");

        Assert.Contains(diagnostics, d => d.Id == "MSS016" && d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
        Assert.Contains("#Struct Lib::Snapshot as Snapshot", output);
        Assert.Contains("declare Snapshot G_Current;", output);
        Assert.DoesNotContain("#Struct Snapshot {", output);
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
            }

            public class Host
            {
                public CounterLib Lib = null!;
                public int ReadLimit() => CounterLib.Limit;
                public int ReadSetting() => CounterLib.InitialScore;
                public int ReadScore() => Lib.Score;
                public void WriteScore() => Lib.Score = 7;
            }
            """;

        var (output, diagnostics) = EmitScript(code, "Host");

        Assert.Empty(diagnostics);
        Assert.Contains("return CounterLib::C_Limit;", output);
        Assert.Contains("return CounterLib::S_InitialScore;", output);
        Assert.Contains("return Lib::GetScore();", output);
        Assert.Contains("Lib::SetScore(7);", output);
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
