using Xunit;

namespace ManiaScriptSharp.ApiGenerator.Tests;

public class ScriptApiEmitterTests
{
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
