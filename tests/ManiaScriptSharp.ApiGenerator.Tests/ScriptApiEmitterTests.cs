using Xunit;

namespace ManiaScriptSharp.ApiGenerator.Tests;

public class ScriptApiEmitterTests
{
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
