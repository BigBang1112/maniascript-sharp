using Xunit;

namespace ManiaScriptSharp.Generator.Tests;

/// <summary>
/// Tests that <c>OnChange(value, oldValue => { ... })</c> is translated into a static
/// backing global plus an <c>if (Value != OldValue) { ...; OldValue = Value; }</c> block, since
/// ManiaScript has no equivalent runtime change-tracking mechanism.
/// </summary>
public class OnChangeEmitterTests : EmitterTestBase
{
    // A structurally-matching stand-in for CNod.OnChange — detection is by name + the
    // ChangeCallback<T> parameter type, not by declaring type, so this is enough to test with.
    private const string OnChangeStub = "void OnChange<T>(T value, ChangeCallback<T> callback) {} int _score;";

    [Fact]
    public void Emit_OnChange_TranslatesToIfBlock_WithRemappedOldParam()
    {
        var output = TranslateStmtMs(
            "OnChange(_score, oldScore => { Log(oldScore); Log(_score); });",
            OnChangeStub + " public void Log(int x) {}");

        Assert.Equal(
            "if (Score != OldScore) {\n    Log(OldScore);\n    Log(Score);\n    OldScore = Score;\n}",
            output);
    }

    [Fact]
    public void Emit_OnChange_ExpressionBodiedCallback_TranslatesSingleStatement()
    {
        var output = TranslateStmtMs(
            "OnChange(_score, oldScore => Log(oldScore));",
            OnChangeStub + " public void Log(int x) {}");

        Assert.Equal(
            "if (Score != OldScore) {\n    Log(OldScore);\n    OldScore = Score;\n}",
            output);
    }

    [Fact]
    public void Emit_OnChange_ParenthesizedCallbackParam_Works()
    {
        var output = TranslateStmtMs(
            "OnChange(_score, (int oldScore) => { });",
            OnChangeStub);

        Assert.StartsWith("if (Score != OldScore) {", output);
    }

    [Fact]
    public void Collect_OnChange_DeclaresBackingGlobal()
    {
        var output = EmitGlobalsWithOnChange(
            $"{OnChangeStub} void Loop() {{ OnChange(_score, oldScore => {{ }}); }}");

        Assert.Contains("declare Integer OldScore;", output);
    }

    [Fact]
    public void Emit_OnChange_MethodGroupValue_ReportsUnsupported()
    {
        // The value must be a direct field/property reference — a method reference can't
        // name a backing global.
        var (output, diagnostics) = TranslateStmtWithDiagnosticsMs(
            "OnChange(GetScore, oldScore => { });",
            OnChangeStub + " int GetScore() => _score;");

        Assert.Single(diagnostics);
        Assert.Equal("", output); // unsupported shape — nothing emitted for the statement
    }

    [Fact]
    public void Emit_OnChange_ComplexValueExpression_ReportsUnsupported()
    {
        // Same reasoning — an arbitrary expression (not a plain field/property) can't be
        // used to derive a backing global name.
        var (_, diagnostics) = TranslateStmtWithDiagnosticsMs(
            "OnChange(_score + 1, oldScore => { });",
            OnChangeStub);

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Emit_UnrelatedInvocation_NamedOnChangeButWrongShape_IsNotConsumed()
    {
        // A method that happens to be named OnChange but doesn't take a ChangeCallback<T>
        // second parameter isn't our special form at all — must translate normally.
        var output = TranslateStmt("OnChange(1, 2);", "public void OnChange(int a, int b) {}");
        Assert.Equal("OnChange(1, 2);", output);
    }
}
