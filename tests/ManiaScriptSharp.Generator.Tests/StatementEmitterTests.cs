using Xunit;

namespace ManiaScriptSharp.Generator.Tests;

public class StatementEmitterTests : EmitterTestBase
{
    // ────────── Simple control-flow ──────────

    [Fact]
    public void Emit_Return_WithExpression()
    {
        Assert.Equal("return 42;", TranslateStmt("return 42;"));
    }

    [Fact]
    public void Emit_Return_WithoutExpression()
    {
        Assert.Equal("return;", TranslateStmt("return;"));
    }

    [Fact]
    public void Emit_Break()
    {
        // break is only valid inside a loop/switch; wrap in while to parse correctly
        var output = TranslateStmt("while (true) { break; }");
        Assert.Contains("break;", output);
    }

    [Fact]
    public void Emit_Continue()
    {
        var output = TranslateStmt("while (true) { continue; }");
        Assert.Contains("continue;", output);
    }

    // ────────── Throw statements ──────────

    [Fact]
    public void Emit_Throw_NoMessage_EmitsAssertFalse()
    {
        var output = TranslateStmt("throw new System.Exception();");
        Assert.Equal("assert(False);", output);
    }

    [Fact]
    public void Emit_Throw_WithMessage_EmitsAssertFalseWithMessage()
    {
        var output = TranslateStmt("throw new System.Exception(\"oops\");");
        Assert.Equal("assert(False, \"oops\");", output);
    }

    [Fact]
    public void Emit_Throw_NotImplementedException_EmitsAssertFalse()
    {
        var output = TranslateStmt("throw new System.NotImplementedException();");
        Assert.Equal("assert(False);", output);
    }

    // ────────── Expression statements ──────────

    [Fact]
    public void Emit_ExprStatement_PostfixIncrement()
    {
        Assert.Equal("X += 1;", TranslateStmt("x++;", "int x;"));
    }

    [Fact]
    public void Emit_ExprStatement_PostfixDecrement()
    {
        Assert.Equal("X -= 1;", TranslateStmt("x--;", "int x;"));
    }

    [Fact]
    public void Emit_ExprStatement_Assignment()
    {
        Assert.Equal("X = 5;", TranslateStmt("x = 5;", "int x;"));
    }

    // ────────── Ternary / ??= (no inline conditional in ManiaScript) ──────────

    [Fact]
    public void Emit_LocalDecl_Ternary_RewritesToIfElse()
    {
        var output = TranslateStmt("int y = x > 0 ? 1 : -1;", "int x;");
        Assert.Equal("declare Integer Y;\nif (X > 0) {\n\tY = 1;\n} else {\n\tY = -1;\n}", output);
    }

    [Fact]
    public void Emit_Return_Ternary_RewritesToIfElse()
    {
        var output = TranslateStmt("return x > 0 ? 1 : -1;", "int x;");
        Assert.Equal("if (X > 0) {\n\treturn 1;\n} else {\n\treturn -1;\n}", output);
    }

    [Fact]
    public void Emit_Assignment_Ternary_RewritesToIfElse()
    {
        var output = TranslateStmt("x = x > 0 ? 1 : -1;", "int x;");
        Assert.Equal("if (X > 0) {\n\tX = 1;\n} else {\n\tX = -1;\n}", output);
    }

    [Fact]
    public void Emit_NullCoalescingAssignment_RewritesToIf()
    {
        var output = TranslateStmt("x ??= 1;", "object x;");
        Assert.Equal("if (X == Null) {\n\tX = 1;\n}", output);
    }

    // ────────── While ──────────

    [Fact]
    public void Emit_While_TrueCondition()
    {
        Assert.Equal("while (True) {\n}", TranslateStmt("while (true) { }"));
    }

    [Fact]
    public void Emit_While_WithBody()
    {
        var output = TranslateStmt("while (x > 0) { x--; }", "int x;");
        Assert.StartsWith("while (X > 0) {", output);
        Assert.Contains("X -= 1;", output);
    }

    // ────────── For loops ──────────

    [Fact]
    public void Emit_For_CanonicalLessThan()
    {
        // i < 10 → hi = "10 - 1"
        Assert.Equal("for (I, 0, 10 - 1) {\n}", TranslateStmt("for (int i = 0; i < 10; i++) { }"));
    }

    [Fact]
    public void Emit_For_CanonicalLessThanOrEqual()
    {
        // i <= 9 → hi = "9"
        Assert.Equal("for (I, 0, 9) {\n}", TranslateStmt("for (int i = 0; i <= 9; i++) { }"));
    }

    [Fact]
    public void Emit_For_NonZeroStart()
    {
        Assert.Equal("for (I, 5, 10 - 1) {\n}", TranslateStmt("for (int i = 5; i < 10; i++) { }"));
    }

    [Fact]
    public void Emit_For_VariableNamePascalCased()
    {
        // iterator named `index` → ManiaScript name `Index`
        Assert.Equal("for (Index, 0, 10 - 1) {\n}", TranslateStmt("for (int index = 0; index < 10; index++) { }"));
    }

    [Fact]
    public void Emit_For_CanonicalPreIncrement()
    {
        // ++i is equivalent to i++ for canonical detection
        Assert.Equal("for (I, 0, 10 - 1) {\n}", TranslateStmt("for (int i = 0; i < 10; ++i) { }"));
    }

    [Fact]
    public void Emit_For_CanonicalPlusEqualsOne()
    {
        // i += 1 is equivalent to i++ for canonical detection
        Assert.Equal("for (I, 0, 10 - 1) {\n}", TranslateStmt("for (int i = 0; i < 10; i += 1) { }"));
    }

    [Fact]
    public void Emit_For_FallbackToWhile_DescendingLoop()
    {
        // Descending for loop: non-canonical → while fallback
        var output = TranslateStmt("for (int i = 10; i > 0; i--) { }");
        Assert.StartsWith("declare Integer I = 10;", output);
        Assert.Contains("while (", output);
        Assert.Contains("I -= 1;", output);
    }

    [Fact]
    public void Emit_For_FallbackToWhile_CustomStep()
    {
        // ManiaScript's for() has no step parameter — i += 2 must stay a while loop.
        var output = TranslateStmt("for (int i = 0; i < 10; i += 2) { }");
        Assert.StartsWith("declare Integer I = 0;", output);
        Assert.Contains("while (I < 10) {", output);
        Assert.Contains("I += 2;", output);
    }

    [Fact]
    public void Emit_For_FallbackToWhile_ConditionVariableMismatch()
    {
        // Condition tests a different variable than the one declared/incremented.
        var output = TranslateStmt("for (int i = 0; j < 10; i++) { }", "int j;");
        Assert.StartsWith("declare Integer I = 0;", output);
        Assert.Contains("while (J < 10) {", output);
        Assert.Contains("I += 1;", output);
    }

    [Fact]
    public void Emit_For_FallbackToWhile_ReusedVariable_NoDeclaration()
    {
        // for (i = 0; ...; ...) with `i` declared outside the loop: no fresh `declare`,
        // but the initializer assignment must still be emitted.
        var output = TranslateStmt("for (i = 0; i < 10; i++) { }", "int i;");
        Assert.StartsWith("I = 0;", output);
        Assert.DoesNotContain("declare Integer I", output);
        Assert.Contains("while (I < 10) {", output);
    }

    [Fact]
    public void Emit_For_FallbackToWhile_NonIntegerType()
    {
        // Non-integer loop variables can't use ManiaScript's for() at all.
        var output = TranslateStmt("for (float f = 0f; f < 1f; f += 0.5f) { }");
        Assert.StartsWith("declare Real F = 0", output);
        Assert.Contains("while (F < 1", output);
    }

    [Fact]
    public void Emit_For_Infinite_NoCondition()
    {
        var output = TranslateStmt("for (int i = 0; ; i++) { }");
        Assert.StartsWith("declare Integer I = 0;", output);
        Assert.Contains("while (True) {", output);
    }

    [Fact]
    public void Emit_For_WithBody()
    {
        var output = TranslateStmt("for (int i = 0; i < 3; i++) { return i; }");
        Assert.StartsWith("for (I, 0, 3 - 1) {", output);
        Assert.Contains("return I;", output);
    }

    // ────────── Foreach ──────────

    [Fact]
    public void Emit_Foreach_Simple()
    {
        var output = TranslateStmt("foreach (var item in items) { }", "int[] items;");
        Assert.Equal("foreach (Item in Items) {\n}", output);
    }

    [Fact]
    public void Emit_Foreach_NamePascalCased()
    {
        var output = TranslateStmt("foreach (var myItem in items) { }", "int[] items;");
        Assert.StartsWith("foreach (MyItem in", output);
    }

    [Fact]
    public void Emit_Foreach_TupleDestructuring_KeyValueForm()
    {
        // foreach (var (k, v) in dict) → foreach (K => V in Dict)
        var output = TranslateStmt(
            "foreach (var (k, v) in dict) { }",
            "System.Collections.Generic.Dictionary<string, int> dict;");
        Assert.Equal("foreach (K => V in Dict) {\n}", output);
    }

    [Fact]
    public void Emit_Foreach_IndexMethod_ManualCounter()
    {
        // foreach (var (i, x) in items.Index()) → a manually incremented counter, since
        // .Index() guarantees a 0-based sequential position on any list — unlike a
        // ManiaScript array's native key, which isn't sequential for associative arrays.
        var output = TranslateBodyMs(
            "foreach (var (i, x) in items.Index()) { }",
            "int[] items = [];");
        Assert.StartsWith("declare Integer I = 0;", output);
        Assert.Contains("foreach (X in Items) {", output);
        Assert.Contains("I += 1;", output);
        Assert.DoesNotContain("=>", output);
    }

    // ────────── If ──────────

    [Fact]
    public void Emit_If_Simple()
    {
        var output = TranslateStmt("if (x > 0) { }", "int x;");
        Assert.Equal("if (X > 0) {\n}", output);
    }

    [Fact]
    public void Emit_If_WithElse()
    {
        var output = TranslateStmt("if (x > 0) { return; } else { return; }", "int x;");
        Assert.Contains("} else {", output);
    }

    [Fact]
    public void Emit_If_ElseIf_Chained()
    {
        var output = TranslateStmt(
            "if (x == 1) { } else if (x == 2) { } else { }",
            "int x;");
        Assert.Contains("} else if (X == 2) {", output);
    }

    [Fact]
    public void Emit_If_TryGetValue_Simple()
    {
        var output = TranslateStmt(
            "if (dict.TryGetValue(key, out var value)) { }",
            "System.Collections.Generic.Dictionary<string, int> dict; string key;");
        Assert.Equal("if (Dict.existskey(Key)) {\n\tdeclare Integer Value = Dict[Key];\n}", output);
    }

    [Fact]
    public void Emit_If_TryGetValue_WithElse()
    {
        var output = TranslateStmt(
            "if (dict.TryGetValue(key, out var value)) { } else { }",
            "System.Collections.Generic.Dictionary<string, int> dict; string key;");
        Assert.Contains("if (Dict.existskey(Key)) {", output);
        Assert.Contains("} else {", output);
    }

    [Fact]
    public void Emit_If_TryGetValue_Negated()
    {
        var output = TranslateStmt(
            "if (!dict.TryGetValue(key, out var value)) { return; }",
            "System.Collections.Generic.Dictionary<string, int> dict; string key;");
        Assert.Equal(
            "declare Integer Value;\nif (!Dict.existskey(Key)) {\n\treturn;\n} else {\n\tValue = Dict[Key];\n}",
            output);
    }

    // ────────── Local declarations ──────────

    [Fact]
    public void Emit_LocalDecl_Var_WithInitialiser()
    {
        // var x = 5; → declare X = 5;
        var output = TranslateStmt("var x = 5;");
        Assert.Equal("declare X = 5;", output);
    }

    [Fact]
    public void Emit_LocalDecl_ExplicitType_Mapped()
    {
        // int x = 5; → declare Integer X = 5;
        var output = TranslateStmt("int x = 5;");
        Assert.Equal("declare Integer X = 5;", output);
    }

    [Fact]
    public void Emit_LocalDecl_EmptyStructCreation_NoInitialiser()
    {
        // new CustomStruct() → declaration with no value
        var output = TranslateStmt("var s = new System.Text.StringBuilder();");
        // Empty object creation strips the initialiser but keeps the type name.
        Assert.Equal("declare StringBuilder S;", output);
    }

    [Fact]
    public void Emit_LocalDecl_NullLiteral_ForIdentType_BecomesNullId()
    {
        // Ident? x = null; → declare Ident X = NullId; (Ident's default value)
        var output = TranslateStmt("Ident? x = null;", "public struct Ident {}");
        Assert.Equal("declare Ident X = NullId;", output);
    }

    [Fact]
    public void Emit_LocalDecl_DefaultLiteral_ForIdentType_BecomesNullId()
    {
        var output = TranslateStmt("Ident? x = default;", "public struct Ident {}");
        Assert.Equal("declare Ident X = NullId;", output);
    }

    // ────────── Label calls ──────────

    [Fact]
    public void Emit_LabelCall_NoSemicolon()
    {
        // Virtual method calls become +++Name+++  (no trailing semicolon)
        var output = TranslateStmtWithLabel("MyLoop", "MyLoop();");
        Assert.Equal("+++MyLoop+++", output);
    }

    // ────────── Block ──────────

    [Fact]
    public void Emit_Block_EmitsWithBraces()
    {
        // Emit (not EmitInline) on a block emits it with surrounding braces.
        var output = TranslateStmt("{ return 1; }");
        Assert.Equal("{\n\treturn 1;\n}", output);
    }

    // ────────── Persistent / Local / Metadata declare ──────────

    [Fact]
    public void Emit_PersistentFor_EmitsDeclare()
    {
        // Persistent<int>.For(provider, out var myVar) → declare persistent Integer Persistent_MyVar for Provider;
        var members = "IPersistentProvider provider = null!;";
        var output = TranslateStmtMs("Persistent<int>.For(provider, out var myVar);", members);
        Assert.Equal("declare persistent Integer Persistent_MyVar for Provider;", output);
    }

    [Fact]
    public void Emit_LocalFor_EmitsDeclare()
    {
        // Local<int>.For(provider, out var myVar) → declare Integer MyVar for Provider;
        var members = "ILocalProvider provider = null!;";
        var output = TranslateStmtMs("Local<int>.For(provider, out var myVar);", members);
        Assert.Equal("declare Integer MyVar for Provider;", output);
    }

    [Fact]
    public void Emit_MetadataFor_EmitsDeclare()
    {
        // Metadata<int>.For(provider, out var myVar) → declare metadata Integer Metadata_MyVar for Provider;
        var members = "IMetadataProvider provider = null!;";
        var output = TranslateStmtMs("Metadata<int>.For(provider, out var myVar);", members);
        Assert.Equal("declare metadata Integer Metadata_MyVar for Provider;", output);
    }

    [Fact]
    public void Emit_PersistentFor_StringType()
    {
        var members = "IPersistentProvider provider = null!;";
        var output = TranslateStmtMs("Persistent<string>.For(provider, out var myLogin);", members);
        Assert.Equal("declare persistent Text Persistent_MyLogin for Provider;", output);
    }

    [Fact]
    public void Emit_PersistentFor_PascalCasesVariableName()
    {
        var members = "IPersistentProvider provider = null!;";
        var output = TranslateStmtMs("Persistent<int>.For(provider, out var score_total);", members);
        Assert.Equal("declare persistent Integer Persistent_Score_total for Provider;", output);
    }

    [Fact]
    public void Emit_NetwriteFor_EmitsDeclare()
    {
        // net_ prefix on variable name is stripped; Net_ is added by generator
        var output = TranslateStmtMs("Netwrite<int>.For(provider, out var net_Score);", "INetwriteProvider provider = null!;");
        Assert.Equal("declare netwrite Integer Net_Score for Provider;", output);
    }

    [Fact]
    public void Emit_NetreadFor_EmitsDeclare()
    {
        var output = TranslateStmtMs("Netread<int>.For(provider, out var net_Score);", "INetreadProvider provider = null!;");
        Assert.Equal("declare netread Integer Net_Score for Provider;", output);
    }

    [Fact]
    public void Emit_NetwriteFor_VariableNameWithoutNetPrefix()
    {
        // No net_ on variable name — Net_ is still prepended
        var output = TranslateStmtMs("Netwrite<string>.For(provider, out var score);", "INetwriteProvider provider = null!;");
        Assert.Equal("declare netwrite Text Net_Score for Provider;", output);
    }

    // ────────── Declare-for variable usage keeps prefix ──────────

    [Fact]
    public void Emit_PersistentFor_UsageKeepsPrefix()
    {
        const string members = "IPersistentProvider provider = null!;";
        var output = TranslateBodyMs(
            "Persistent<int>.For(provider, out var myVar); myVar.Value = 42;",
            members);
        Assert.Contains("declare persistent Integer Persistent_MyVar for Provider;", output);
        Assert.Contains("Persistent_MyVar = 42;", output);
    }

    [Fact]
    public void Emit_LocalFor_UsageKeepsPrefix()
    {
        const string members = "ILocalProvider provider = null!;";
        var output = TranslateBodyMs(
            "Local<int>.For(provider, out var myVar); _ = myVar.Value;",
            members);
        Assert.Contains("declare Integer MyVar for Provider;", output);
        Assert.Contains("MyVar", output);
    }

    [Fact]
    public void Emit_NetwriteFor_UsageKeepsPrefix()
    {
        const string members = "INetwriteProvider provider = null!;";
        var output = TranslateBodyMs(
            "Netwrite<int>.For(provider, out var score); score.Value = 5;",
            members);
        Assert.Contains("declare netwrite Integer Net_Score for Provider;", output);
        Assert.Contains("Net_Score = 5;", output);
    }
}
