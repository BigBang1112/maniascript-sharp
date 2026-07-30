using Xunit;

namespace ManiaScriptSharp.Generator.Tests;

/// <summary>
/// Tests that C# properties with accessor bodies are translated into
/// ManiaScript getter/setter functions.
/// </summary>
public class PropertyEmitterTests : EmitterTestBase
{
    // ──────────── Getter ────────────

    [Fact]
    public void Emit_Getter_BlockBody()
    {
        var output = EmitFunctions("int _x; public int Score { get { return _x; } }");
        Assert.Contains("Integer GetScore() {", output);
        Assert.Contains("return X;", output);
    }

    [Fact]
    public void Emit_Getter_ExpressionBodyAccessor()
    {
        // `get => expr`
        var output = EmitFunctions("int _x; public int Score { get => _x; }");
        Assert.Contains("Integer GetScore() {", output);
        Assert.Contains("return X;", output);
    }

    [Fact]
    public void Emit_Getter_ExpressionBodyProperty()
    {
        // `int Score => expr` (whole property is expression-bodied)
        var output = EmitFunctions("int _x; public int Score => _x;");
        Assert.Contains("Integer GetScore() {", output);
        Assert.Contains("return X;", output);
    }

    [Fact]
    public void Emit_Getter_StringType()
    {
        var output = EmitFunctions("string _name = \"\"; public string Name { get { return _name; } }");
        Assert.Contains("Text GetName() {", output);
    }

    [Fact]
    public void Emit_Getter_BoolType()
    {
        var output = EmitFunctions("bool _active; public bool Active { get { return _active; } }");
        Assert.Contains("Boolean GetActive() {", output);
    }

    // ──────────── Setter ────────────

    [Fact]
    public void Emit_Setter_BlockBody_UsesValue()
    {
        var output = EmitFunctions("int _x; public int Score { set { _x = value; } }");
        Assert.Contains("Void SetScore(Integer _Value) {", output);
        Assert.Contains("X = _Value;", output);
    }

    [Fact]
    public void Emit_Setter_ExpressionBody()
    {
        var output = EmitFunctions("int _x; public int Score { set => _x = value; }");
        Assert.Contains("Void SetScore(Integer _Value) {", output);
        Assert.Contains("X = _Value;", output);
    }

    // ──────────── Both getter and setter ────────────

    [Fact]
    public void Emit_GetterAndSetter_BothEmitted()
    {
        var output = EmitFunctions("int _x; public int Score { get { return _x; } set { _x = value; } }");
        Assert.Contains("Integer GetScore() {", output);
        Assert.Contains("return X;", output);
        Assert.Contains("Void SetScore(Integer _Value) {", output);
        Assert.Contains("X = _Value;", output);
    }

    // ──────────── Auto-property: generates getter/setter with backing variable ────────────

    [Fact]
    public void Emit_AutoProperty_GetSet_GeneratesGetterSetterWithBacking()
    {
        var output = EmitFunctions("public int Score { get; set; }");
        Assert.Contains("Integer GetScore() {", output);
        Assert.Contains("return G_Score;", output);
        Assert.Contains("Void SetScore(Integer _Value) {", output);
        Assert.Contains("G_Score = _Value;", output);
    }

    [Fact]
    public void Emit_AutoProperty_GetOnly_GeneratesGetterOnly()
    {
        var output = EmitFunctions("public int Score { get; }");
        Assert.Contains("Integer GetScore() {", output);
        Assert.Contains("return G_Score;", output);
        Assert.DoesNotContain("SetScore", output);
    }

    [Fact]
    public void Emit_AutoProperty_Private_NoBacking_G_Prefix()
    {
        var output = EmitFunctions("private int Score { get; set; }");
        Assert.Contains("Integer Private_GetScore() {", output);
        Assert.Contains("return Score;", output);
        Assert.Contains("Void Private_SetScore(Integer _Value) {", output);
        Assert.Contains("Score = _Value;", output);
    }

    // ──────────── Private accessor uses Private_ prefix ────────────

    [Fact]
    public void Emit_PrivateGetter_HasPrivatePrefix()
    {
        var output = EmitFunctions("int _x; private int Score { get { return _x; } }");
        Assert.Contains("Integer Private_GetScore() {", output);
    }

    // ──────────── Property access at call sites ────────────

    [Fact]
    public void Translate_PropertyRead_BecomeGetterCall()
    {
        // Reading a property `score` in an expression → `GetScore()`
        var output = TranslateExpr("score", "int _x; public int score { get { return _x; } }");
        Assert.Equal("GetScore()", output);
    }

    [Fact]
    public void Translate_PropertyAssign_BecomeSetterCall()
    {
        // Assigning `score = 5` → `SetScore(5)`
        var output = TranslateStmt("score = 5;", "int _x; public int score { get { return _x; } set { _x = value; } }");
        Assert.Equal("SetScore(5);", output);
    }
}
