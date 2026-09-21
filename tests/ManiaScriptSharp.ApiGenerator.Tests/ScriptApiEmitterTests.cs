using Xunit;

namespace ManiaScriptSharp.ApiGenerator.Tests;

public class ScriptApiEmitterTests
{
    [Fact]
    public void Emit_ScriptSummary_IsNotDuplicatedOnVersionConstant()
    {
        var script = new ScriptParser("/** Library summary */\n#Const Version \"2026-09-21\"").Parse();

        var source = new ScriptApiEmitter(
            "Test", "test.Script.txt", "TestScript", script, ApiGeneratorSettings.ForTesting()).Emit();

        Assert.Equal("Library summary", script.ScriptDoc?.Summary);
        Assert.Null(script.Consts[0].Doc);
        Assert.Contains("/// <summary>Library summary</summary>", source);
        Assert.Contains("public static partial class TestScript", source);
        Assert.DoesNotContain("    /// <summary>Library summary</summary>\n    public const string Version", source);
        Assert.Contains("public const string Version = \"2026-09-21\";", source);
    }

    [Fact]
    public void Emit_InitialLineComment_IsTheScriptSummary_NotVersionOrMethodDocumentation()
    {
        var script = new ScriptParser("// Library summary\n#Const Version \"2026-09-21\"\n/** Version method */\nText GetScriptVersion() { return Version; }").Parse();

        var source = new ScriptApiEmitter(
            "Test", "test.Script.txt", "TestScript", script, ApiGeneratorSettings.ForTesting()).Emit();

        Assert.Equal("Library summary", script.ScriptDoc?.Summary);
        Assert.Null(script.Consts[0].Doc);
        Assert.Equal("Version method", script.Functions[0].Doc?.Summary);
        Assert.Contains("/// <summary>Library summary</summary>", source);
        Assert.Contains("    /// <summary>Version method</summary>", source);
    }

    [Theory]
    [InlineData("/// Leading documentation\n#Const C_Count 3", "Leading documentation")]
    [InlineData("#Const C_Count 3 ///< Inline documentation", "Inline documentation")]
    public void Emit_ConstDocumentation_UsesTheCorrectCommentAssociation(string scriptText, string expectedDocumentation)
    {
        var script = new ScriptParser(scriptText).Parse();

        var source = new ScriptApiEmitter(
            "Test", "test.Script.txt", "TestScript", script, ApiGeneratorSettings.ForTesting()).Emit();

        Assert.Contains($"/// <summary>{expectedDocumentation}</summary>", source);
        Assert.Contains("public const int C_Count = 3;", source);
    }

    [Fact]
    public void Emit_MethodDocumentation_UsesTheGeneratedParameterName()
    {
        var script = new ParsedScript
        {
            Functions =
            [
                new ScriptFunction
                {
                    ReturnType = "Void",
                    Name = "SetTag",
                    Parameters = [new ScriptParameter { Type = "Text", Name = "_Tag" }],
                    Doc = new ScriptDocComment
                    {
                        Params = [("Tag", "The tag to set")],
                    },
                },
            ],
        };

        var source = new ScriptApiEmitter(
            "Test", "test.Script.txt", "TestScript", script,
            ApiGeneratorSettings.ForTesting(standardizeParamNames: false)).Emit();

        Assert.Contains("public static void SetTag(string _Tag)", source);
        Assert.Contains("<param name=\"_Tag\">The tag to set</param>", source);
        Assert.DoesNotContain("<param name=\"Tag\">", source);
    }

    [Fact]
    public void Emit_Arrays_UsesListsForMethodsAndStructFields()
    {
        var script = new ParsedScript
        {
            Functions =
            [
                new ScriptFunction
                {
                    ReturnType = "Text[]",
                    Name = "GetNames",
                    Parameters = [new ScriptParameter { Type = "Integer[]", Name = "Ids" }],
                },
            ],
            Structs =
            [
                new ScriptStruct
                {
                    Name = "SResult",
                    Fields = [new ScriptStructField { Type = "Boolean[]", Name = "Flags" }],
                },
            ],
        };

        var source = new ScriptApiEmitter(
            "Test", "test.Script.txt", "TestScript", script, ApiGeneratorSettings.ForTesting()).Emit();

        Assert.Contains("public global::System.Collections.Generic.IList<bool> Flags;", source);
        Assert.Contains("public static global::System.Collections.Generic.IList<string> GetNames(global::System.Collections.Generic.IList<int> ids)", source);
    }
}
