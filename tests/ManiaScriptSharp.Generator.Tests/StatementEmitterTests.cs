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
    public void Emit_Return_AssignmentReportsErrorAndIsNotEmitted()
    {
        var (output, diagnostics) = TranslateStmtWithDiagnostics("return x = 5;", "int x;");

        Assert.Empty(output);
        Assert.Contains(diagnostics, d => d.Id == "MSS017"
            && d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public void Emit_Break()
    {
        // break is only valid inside a loop/switch; wrap in while to parse correctly
        var output = TranslateStmt("while (true) { break; }");
        Assert.Contains("break;", output);
    }

    [Fact]
    public void Emit_ContinueInConditionalWhile_ReportsWarning()
    {
        var (output, diagnostics) = TranslateStmtWithDiagnostics("while (isReady) { continue; }", "bool isReady;");
        Assert.Contains("continue;", output);
        Assert.Contains(diagnostics, d => d.Id == "MSS020"
            && d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Emit_ContinueInTrueWhile_DoesNotReportWarning()
    {
        var (_, diagnostics) = TranslateStmtWithDiagnostics("while (true) { continue; }");

        Assert.DoesNotContain(diagnostics, d => d.Id == "MSS020");
    }

    [Fact]
    public void Emit_ContinueInConditionalWhile_VersionTwo_DoesNotReportWarning()
    {
        var (_, diagnostics) = TranslateStmtWithDiagnostics(
            "while (isReady) { continue; }",
            "bool isReady;",
            BuildSettings.Version2);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MSS020");
    }

    [Fact]
    public void Emit_ContinueInForeachInsideWhile_DoesNotReportWarning()
    {
        var (_, diagnostics) = TranslateStmtWithDiagnostics("while (true) { foreach (var item in items) { continue; } }", "int[] items = []; ");

        Assert.DoesNotContain(diagnostics, d => d.Id == "MSS020");
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
        Assert.Equal("G_X += 1;", TranslateStmt("x++;", "int x;"));
    }

    [Fact]
    public void Emit_ExprStatement_PostfixDecrement()
    {
        Assert.Equal("G_X -= 1;", TranslateStmt("x--;", "int x;"));
    }

    [Fact]
    public void Emit_ExprStatement_Assignment()
    {
        Assert.Equal("G_X = 5;", TranslateStmt("x = 5;", "int x;"));
    }

    [Fact]
    public void Emit_ListAddRange_AsForeach()
    {
        var output = TranslateStmt(
            "target.AddRange(source);",
            "System.Collections.Generic.List<int> target; System.Collections.Generic.List<int> source;");

        Assert.Equal(
            "foreach (AddRangeItem in G_Source) {\n" +
            "    G_Target.add(AddRangeItem);\n" +
            "}",
            output);
    }

    [Fact]
    public void Emit_ZeroArgumentLambda_AsAlias()
    {
        var output = TranslateBodyMs(
            "var item = () => items[i]; Console.WriteLine(\"Item added at \" + item().Position); previousItems.Add(item().Position);",
            "class CItem { public int Position; } CItem[] items; int i; System.Collections.Generic.List<int> previousItems;");

        Assert.Equal(
            "declare CItem Item <=> G_Items[G_I];\n" +
            "log(\"Item added at \" ^ Item.Position);\n" +
            "G_PreviousItems.add(Item.Position);",
            output);
    }

    [Fact]
    public void Emit_ExprStatement_ChainedAssignmentReportsErrorAndIsNotEmitted()
    {
        var (output, diagnostics) = TranslateStmtWithDiagnostics("x = y = 1;", "int x; int y;");

        Assert.Empty(output);
        Assert.Contains(diagnostics, d => d.Id == "MSS018"
            && d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public void Emit_LocalDecl_AssignmentInitializerReportsErrorAndIsNotEmitted()
    {
        var (output, diagnostics) = TranslateStmtWithDiagnostics("var x = y = [];", "int[] y;");

        Assert.Empty(output);
        Assert.Contains(diagnostics, d => d.Id == "MSS018"
            && d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public void Emit_LocalDecl_DictionaryInitializerRemainsSupported()
    {
        var (output, diagnostics) = TranslateStmtWithDiagnostics(
            "var scores = new Dictionary<string, int> { [\"alpha\"] = 10, [\"beta\"] = 20 };");

        Assert.Equal("declare Scores = [\"alpha\" => 10, \"beta\" => 20];", output);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Emit_AssignmentInConditionReportsErrorAndIsNotEmitted()
    {
        var (output, diagnostics) = TranslateStmtWithDiagnostics("if (isReady = true) { }", "bool isReady;");

        Assert.Empty(output);
        Assert.Contains(diagnostics, d => d.Id == "MSS018"
            && d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public void Emit_ForInitializer_ChainedAssignmentReportsErrorAndIsNotEmitted()
    {
        var (output, diagnostics) = TranslateStmtWithDiagnostics(
            "for (x = y = 0; x < 1; x++) { }", "int x; int y;");

        Assert.Empty(output);
        Assert.Contains(diagnostics, d => d.Id == "MSS018"
            && d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    // ────────── Ternary / ??= (no inline conditional in ManiaScript) ──────────

    [Fact]
    public void Emit_LocalDecl_Ternary_RewritesToIfElse()
    {
        var output = TranslateStmt("int y = x > 0 ? 1 : -1;", "int x;");
        Assert.Equal("declare Integer Y;\nif (G_X > 0) {\n    Y = 1;\n} else {\n    Y = -1;\n}", output);
    }

    [Fact]
    public void Emit_Return_Ternary_RewritesToIfElse()
    {
        var output = TranslateStmt("return x > 0 ? 1 : -1;", "int x;");
        Assert.Equal("if (G_X > 0) {\n    return 1;\n} else {\n    return -1;\n}", output);
    }

    [Fact]
    public void Emit_Assignment_Ternary_RewritesToIfElse()
    {
        var output = TranslateStmt("x = x > 0 ? 1 : -1;", "int x;");
        Assert.Equal("if (G_X > 0) {\n    G_X = 1;\n} else {\n    G_X = -1;\n}", output);
    }

    [Fact]
    public void Emit_NullCoalescingAssignment_RewritesToIf()
    {
        var output = TranslateStmt("x ??= 1;", "object x;");
        Assert.Equal("if (G_X == Null) {\n    G_X = 1;\n}", output);
    }

    // ────────── Switch expressions (no switch expression in ManiaScript) ──────────

    [Fact]
    public void Emit_LocalDecl_SwitchExpression_ConstantArms_RewritesToSwitchStatement()
    {
        // All-constant arms (plus a trailing discard) can be expressed as a real switch statement.
        var output = TranslateStmt(
            "var y = x switch { \"a\" => 1, \"b\" => 2, _ => 0 };", "string x;");
        Assert.Equal(
            "declare Integer Y;\n" +
            "switch (G_X) {\n" +
            "    case \"a\": {\n        Y = 1;\n    }\n" +
            "    case \"b\": {\n        Y = 2;\n    }\n" +
            "    default: {\n        Y = 0;\n    }\n" +
            "}",
            output);
    }

    [Fact]
    public void Emit_Return_SwitchExpression_ConstantArms_RewritesToSwitchStatement()
    {
        var output = TranslateStmt("return x switch { 1 => 10, 2 => 20, _ => 0 };", "int x;");
        Assert.Equal(
            "switch (G_X) {\n" +
            "    case 1: {\n        return 10;\n    }\n" +
            "    case 2: {\n        return 20;\n    }\n" +
            "    default: {\n        return 0;\n    }\n" +
            "}",
            output);
    }

    [Fact]
    public void Emit_SwitchExpression_RelationalPattern_FallsBackToIfElse()
    {
        // A relational pattern has no case-label equivalent, so the whole expression falls
        // back to nested if/else instead of a switch statement.
        var output = TranslateStmt("return x switch { > 0 => 1, _ => 0 };", "int x;");
        Assert.Equal("if (G_X > 0) {\n    return 1;\n} else {\n    return 0;\n}", output);
    }

    [Fact]
    public void Emit_SwitchExpression_WhenClause_FallsBackToIfElse()
    {
        // A `when` clause has no case-label equivalent even on an otherwise-constant arm.
        var output = TranslateStmt("return x switch { 1 when x > 0 => 10, _ => 0 };", "int x;");
        Assert.Equal("if ((G_X == 1) && (G_X > 0)) {\n    return 10;\n} else {\n    return 0;\n}", output);
    }

    [Fact]
    public void Emit_SwitchExpression_VarPattern_AliasesBoundVariable()
    {
        // `var n` is aliased directly to the subject expression (no physical declare needed).
        var output = TranslateStmt("return x switch { var n when n > 0 => n, _ => 0 };", "int x;");
        Assert.Equal("if (G_X > 0) {\n    return G_X;\n} else {\n    return 0;\n}", output);
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
        Assert.StartsWith("while (G_X > 0) {", output);
        Assert.Contains("G_X -= 1;", output);
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
    public void Emit_For_DescendingLoop_FallsBackToWhile()
    {
        Assert.Equal(
            "declare Integer I = 10;\n" +
            "while (I > 0) {\n" +
            "    I -= 1;\n" +
            "}",
            TranslateStmt("for (int i = 10; i > 0; i--) { }"));
    }

    [Fact]
    public void Emit_For_CustomStep_FallsBackToWhile()
    {
        Assert.Equal(
            "declare Integer I = 0;\n" +
            "while (I < 10) {\n" +
            "    I += 2;\n" +
            "}",
            TranslateStmt("for (int i = 0; i < 10; i += 2) { }"));
    }

    [Fact]
    public void Emit_For_CustomPositiveStep_VersionTwo_UsesNativeFor()
    {
        Assert.Equal(
            "for (I, 0, 10 - 1, 2) {\n}",
            TranslateStmt("for (int i = 0; i < 10; i += 2) { }", settings: BuildSettings.Version2));
    }

    [Fact]
    public void Emit_For_NegativeStep_VersionTwo_UsesAscendingRangeBounds()
    {
        Assert.Equal(
            "for (I, 0 + 1, 10, -1) {\n}",
            TranslateStmt("for (int i = 10; i > 0; i--) { }", settings: BuildSettings.Version2));
    }

    [Fact]
    public void Emit_For_CustomNegativeStep_VersionTwo_UsesNativeFor()
    {
        Assert.Equal(
            "for (I, 0 + 1, 10, -(2)) {\n}",
            TranslateStmt("for (int i = 10; i > 0; i -= 2) { }", settings: BuildSettings.Version2));
    }

    [Fact]
    public void Emit_For_SteppedLoop_ContinueStillExecutesIncrement()
    {
        var (output, diagnostics) = TranslateStmtWithDiagnostics(
            "for (int i = 6; i >= 0; i -= 2) { if (i == 2) continue; }");

        Assert.Equal(
            "declare Integer I = 6;\n" +
            "while (I >= 0) {\n" +
            "    if (I == 2) {\n" +
            "        I -= 2;\n" +
            "        continue;\n" +
            "    }\n" +
            "    I -= 2;\n" +
            "}",
            output);
        Assert.DoesNotContain(diagnostics, d => d.Id == "MSS020");
    }

    [Fact]
    public void Emit_For_FallbackToWhile_ConditionVariableMismatch()
    {
        // Condition tests a different variable than the one declared/incremented.
        var output = TranslateStmt("for (int i = 0; j < 10; i++) { }", "int j;");
        Assert.StartsWith("declare Integer I = 0;", output);
        Assert.Contains("while (G_J < 10) {", output);
        Assert.Contains("I += 1;", output);
    }

    [Fact]
    public void Emit_For_FallbackToWhile_ReusedVariable_NoDeclaration()
    {
        // for (i = 0; ...; ...) with `i` declared outside the loop: no fresh `declare`,
        // but the initializer assignment must still be emitted.
        var output = TranslateStmt("for (i = 0; i < 10; i++) { }", "int i;");
        Assert.StartsWith("G_I = 0;", output);
        Assert.DoesNotContain("declare Integer G_I", output);
        Assert.Contains("while (G_I < 10) {", output);
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
        Assert.Equal("foreach (Item in G_Items) {\n}", output);
    }

    [Fact]
    public void Emit_Foreach_NamePascalCased()
    {
        var output = TranslateStmt("foreach (var myItem in items) { }", "int[] items;");
        Assert.StartsWith("foreach (MyItem in", output);
    }

    [Fact]
    public void Emit_Foreach_Reverse_VersionTwo_UsesReverseSuffix()
    {
        var output = TranslateStmt(
            "foreach (var item in items.Reverse()) { }",
            "int[] items;",
            BuildSettings.Version2);

        Assert.Equal("foreach (Item in G_Items reverse) {\n}", output);
    }

    [Fact]
    public void Emit_Foreach_TupleDestructuring_KeyValueForm()
    {
        // foreach (var (k, v) in dict) → foreach (K => V in Dict)
        var output = TranslateStmt(
            "foreach (var (k, v) in dict) { }",
            "System.Collections.Generic.Dictionary<string, int> dict;");
        Assert.Equal("foreach (K => V in G_Dict) {\n}", output);
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
        Assert.Contains("foreach (X in G_Items) {", output);
        Assert.Contains("I += 1;", output);
        Assert.DoesNotContain("=>", output);
    }

    [Fact]
    public void Emit_Foreach_DictionaryPair_UsesKeyValueForm_AndRemapsMembers()
    {
        // foreach (var pair in dict) { ... pair.Key / pair.Value ... } → foreach (PairKey => PairValue in Dict)
        var output = TranslateStmt(
            "foreach (var pair in dict) { if (pair.Value == 1) { } var k = pair.Key; }",
            "System.Collections.Generic.Dictionary<string, int> dict;");
        Assert.StartsWith("foreach (PairKey => PairValue in G_Dict) {", output);
        Assert.Contains("if (PairValue == 1) {", output);
        Assert.Contains("declare K = PairKey;", output);
    }

    // ────────── Tuple deconstruction assignment ──────────

    [Fact]
    public void Emit_TupleAssignment_Swap_UsesTemporaries()
    {
        // (a, b) = (b, a): ManiaScript has no tuple type, so every RHS element is evaluated
        // into a temp first (preserving simultaneous-assignment semantics), then assigned back.
        var output = TranslateStmt("(a, b) = (b, a);", "int a; int b;");
        Assert.Equal(
            "declare Integer TupleTmp1 = G_B;\n" +
            "declare Integer TupleTmp2 = G_A;\n" +
            "G_A = TupleTmp1;\n" +
            "G_B = TupleTmp2;",
            output);
    }

    [Fact]
    public void Emit_TupleAssignment_MismatchedArity_ReportsUnsupported()
    {
        var (_, diagnostics) = TranslateStmtWithDiagnostics("(a, b) = (1, 2, 3);", "int a; int b;");
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Emit_TupleAssignment_NonTupleRight_ReportsUnsupported()
    {
        var (_, diagnostics) = TranslateStmtWithDiagnostics("(a, b) = GetPair();", "int a; int b; (int, int) GetPair() => (1, 2);");
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void Emit_Foreach_DictionaryKeys_UsesKeyValueForm_WithPlaceholderValue()
    {
        var output = TranslateStmt(
            "foreach (var name in dict.Keys) { }",
            "System.Collections.Generic.Dictionary<string, int> dict;");
        Assert.Equal("foreach (Name => NameValue in G_Dict) {\n}", output);
    }

    [Fact]
    public void Emit_Foreach_DictionaryValues_UsesKeyValueForm_WithPlaceholderKey()
    {
        var output = TranslateStmt(
            "foreach (var score in dict.Values) { }",
            "System.Collections.Generic.Dictionary<string, int> dict;");
        Assert.Equal("foreach (ScoreKey => Score in G_Dict) {\n}", output);
    }

    [Fact]
    public void Emit_Foreach_DictionaryPair_ComplexUsage_ReportsDiagnosticAndFallsBack()
    {
        // Passing the whole KeyValuePair around can't be expressed via Key => Value —
        // report the construct as unsupported and fall back to the plain single-variable form.
        var (output, diagnostics) = TranslateStmtWithDiagnostics(
            "foreach (var pair in dict) { Log(pair); }",
            "System.Collections.Generic.Dictionary<string, int> dict; void Log(object o) {}");
        Assert.StartsWith("foreach (Pair in G_Dict) {", output);
        Assert.Single(diagnostics);
    }

    // ────────── If ──────────

    [Fact]
    public void Emit_If_Simple()
    {
        var output = TranslateStmt("if (x > 0) { }", "int x;");
        Assert.Equal("if (G_X > 0) {\n}", output);
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
        Assert.Contains("} else if (G_X == 2) {", output);
    }

    [Fact]
    public void Emit_If_TryGetValue_Simple()
    {
        var output = TranslateStmt(
            "if (dict.TryGetValue(key, out var value)) { }",
            "System.Collections.Generic.Dictionary<string, int> dict; string key;");
        Assert.Equal("if (G_Dict.existskey(G_Key)) {\n    declare Integer Value = G_Dict[G_Key];\n}", output);
    }

    [Fact]
    public void Emit_If_TryGetValue_WithElse()
    {
        var output = TranslateStmt(
            "if (dict.TryGetValue(key, out var value)) { } else { }",
            "System.Collections.Generic.Dictionary<string, int> dict; string key;");
        Assert.Contains("if (G_Dict.existskey(G_Key)) {", output);
        Assert.Contains("} else {", output);
    }

    [Fact]
    public void Emit_If_TryGetValue_Negated()
    {
        var output = TranslateStmt(
            "if (!dict.TryGetValue(key, out var value)) { return; }",
            "System.Collections.Generic.Dictionary<string, int> dict; string key;");
        Assert.Equal(
            "declare Integer Value;\nif (!G_Dict.existskey(G_Key)) {\n    return;\n} else {\n    Value = G_Dict[G_Key];\n}",
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
    public void Emit_LabelCall_UsesScopedBlock()
    {
        // Virtual method calls become a scoped +++Name+++ insertion (no trailing semicolon).
        var output = TranslateStmtWithLabel("MyLoop", "MyLoop();");
        Assert.Equal("{+++MyLoop+++}", output);
    }

    // ────────── Block ──────────

    [Fact]
    public void Emit_Block_EmitsWithBraces()
    {
        // Emit (not EmitInline) on a block emits it with surrounding braces.
        var output = TranslateStmt("{ return 1; }");
        Assert.Equal("{\n    return 1;\n}", output);
    }

    // ────────── Persistent / Local / Metadata declare ──────────

    [Fact]
    public void Emit_PersistentFor_EmitsDeclare()
    {
        // Persistent<int>.For(provider, out var myVar) → declare persistent Integer Persistent_MyVar for Provider;
        var members = "IPersistentProvider provider = null!;";
        var output = TranslateStmtMs("Persistent<int>.For(provider, out var myVar);", members);
        Assert.Equal("declare persistent Integer Persistent_MyVar for G_Provider;", output);
    }

    [Fact]
    public void Emit_LocalFor_EmitsDeclare()
    {
        // Local<int>.For(provider, out var myVar) → declare Integer MyVar for Provider;
        var members = "ILocalProvider provider = null!;";
        var output = TranslateStmtMs("Local<int>.For(provider, out var myVar);", members);
        Assert.Equal("declare Integer MyVar for G_Provider;", output);
    }

    [Fact]
    public void Emit_MetadataFor_EmitsDeclare()
    {
        // Metadata<int>.For(provider, out var myVar) → declare metadata Integer Metadata_MyVar for Provider;
        var members = "IMetadataProvider provider = null!;";
        var output = TranslateStmtMs("Metadata<int>.For(provider, out var myVar);", members);
        Assert.Equal("declare metadata Integer Metadata_MyVar for G_Provider;", output);
    }

    [Fact]
    public void Emit_PersistentFor_StringType()
    {
        var members = "IPersistentProvider provider = null!;";
        var output = TranslateStmtMs("Persistent<string>.For(provider, out var myLogin);", members);
        Assert.Equal("declare persistent Text Persistent_MyLogin for G_Provider;", output);
    }

    [Fact]
    public void Emit_PersistentFor_PascalCasesVariableName()
    {
        var members = "IPersistentProvider provider = null!;";
        var output = TranslateStmtMs("Persistent<int>.For(provider, out var score_total);", members);
        Assert.Equal("declare persistent Integer Persistent_Score_total for G_Provider;", output);
    }

    [Fact]
    public void Emit_NetwriteFor_EmitsDeclare()
    {
        // net_ prefix on variable name is stripped; Net_ is added by generator
        var output = TranslateStmtMs("Netwrite<int>.For(provider, out var net_Score);", "INetwriteProvider provider = null!;");
        Assert.Equal("declare netwrite Integer Net_Score for G_Provider;", output);
    }

    [Fact]
    public void Emit_NetreadFor_EmitsDeclare()
    {
        var output = TranslateStmtMs("Netread<int>.For(provider, out var net_Score);", "INetreadProvider provider = null!;");
        Assert.Equal("declare netread Integer Net_Score for G_Provider;", output);
    }

    [Fact]
    public void Emit_NetwriteFor_VariableNameWithoutNetPrefix()
    {
        // No net_ on variable name — Net_ is still prepended
        var output = TranslateStmtMs("Netwrite<string>.For(provider, out var score);", "INetwriteProvider provider = null!;");
        Assert.Equal("declare netwrite Text Net_Score for G_Provider;", output);
    }

    // ────────── Declare-for with explicit name (`as` alias form) ──────────

    [Fact]
    public void Emit_LocalFor_ExplicitName_EmitsAsAlias()
    {
        // Local<int>.For(provider, out var myVar, name: "MyLib_MyVar") → declare Integer MyLib_MyVar as MyVar for Provider;
        var members = "ILocalProvider provider = null!;";
        var output = TranslateStmtMs("Local<int>.For(provider, out var myVar, name: \"MyLib_MyVar\");", members);
        Assert.Equal("declare Integer MyLib_MyVar as MyVar for G_Provider;", output);
    }

    [Fact]
    public void Emit_LocalFor_ExplicitNamePositional_EmitsAsAlias()
    {
        // The merged `name` parameter can also be passed positionally.
        var members = "ILocalProvider provider = null!;";
        var output = TranslateStmtMs("Local<int>.For(provider, out var myVar, \"MyLib_MyVar\");", members);
        Assert.Equal("declare Integer MyLib_MyVar as MyVar for G_Provider;", output);
    }

    [Fact]
    public void Emit_LocalFor_EmptyName_FallsBackToDefault()
    {
        // An explicitly empty name is skipped (the auto-filled caller expression is not an
        // object-side name) — falls back to the plain declare-for form.
        var members = "ILocalProvider provider = null!;";
        var output = TranslateStmtMs("Local<int>.For(provider, out var myVar, name: \"\");", members);
        Assert.Equal("declare Integer MyVar for G_Provider;", output);
    }

    [Fact]
    public void Emit_PersistentFor_ExplicitName_EmitsAsAlias()
    {
        var members = "IPersistentProvider provider = null!;";
        var output = TranslateStmtMs("Persistent<int>.For(provider, out var myVar, name: \"MyLib_MyVar\");", members);
        Assert.Equal("declare persistent Integer MyLib_MyVar as MyVar for G_Provider;", output);
    }

    [Fact]
    public void Emit_MetadataFor_ExplicitName_EmitsAsAlias()
    {
        var members = "IMetadataProvider provider = null!;";
        var output = TranslateStmtMs("Metadata<int>.For(provider, out var myVar, name: \"MyLib_MyVar\");", members);
        Assert.Equal("declare metadata Integer MyLib_MyVar as MyVar for G_Provider;", output);
    }

    [Fact]
    public void Emit_NetwriteFor_ExplicitName_EmitsAsAlias()
    {
        // The explicit name is used as-is (no Net_ prefix injection) —
        // mirroring Nadeo's `declare netwrite Boolean Net_Lobby_Ready as ReadyForPlayer for Player;`.
        var output = TranslateStmtMs("Netwrite<int>.For(provider, out var readyForPlayer, name: \"Net_Lobby_Ready\");", "INetwriteProvider provider = null!;");
        Assert.Equal("declare netwrite Integer Net_Lobby_Ready as ReadyForPlayer for G_Provider;", output);
    }

    [Fact]
    public void Emit_NetreadFor_ExplicitName_EmitsAsAlias()
    {
        var output = TranslateStmtMs("Netread<int>.For(provider, out var readyForPlayer, name: \"Net_Lobby_Ready\");", "INetreadProvider provider = null!;");
        Assert.Equal("declare netread Integer Net_Lobby_Ready as ReadyForPlayer for G_Provider;", output);
    }

    [Fact]
    public void Emit_LocalFor_ExplicitName_UsageUsesAlias()
    {
        // After the alias declare, the rest of the script references the alias name.
        const string members = "ILocalProvider provider = null!;";
        var output = TranslateBodyMs(
            "Local<int>.For(provider, out var myVar, name: \"MyLib_MyVar\"); myVar.Value = 42;",
            members);
        Assert.Contains("declare Integer MyLib_MyVar as MyVar for G_Provider;", output);
        Assert.Contains("MyVar = 42;", output);
    }

    [Fact]
    public void Emit_NetwriteFor_ExplicitName_UsageUsesAlias()
    {
        const string members = "INetwriteProvider provider = null!;";
        var output = TranslateBodyMs(
            "Netwrite<bool>.For(provider, out var readyForPlayer, name: \"Net_Lobby_Ready\"); readyForPlayer.Value = true;",
            members);
        Assert.Contains("declare netwrite Boolean Net_Lobby_Ready as ReadyForPlayer for G_Provider;", output);
        Assert.Contains("ReadyForPlayer = True;", output);
    }

    // ────────── Declare-for variable usage keeps prefix ──────────

    [Fact]
    public void Emit_PersistentFor_UsageKeepsPrefix()
    {
        const string members = "IPersistentProvider provider = null!;";
        var output = TranslateBodyMs(
            "Persistent<int>.For(provider, out var myVar); myVar.Value = 42;",
            members);
        Assert.Contains("declare persistent Integer Persistent_MyVar for G_Provider;", output);
        Assert.Contains("Persistent_MyVar = 42;", output);
    }

    [Fact]
    public void Emit_LocalFor_UsageKeepsPrefix()
    {
        const string members = "ILocalProvider provider = null!;";
        var output = TranslateBodyMs(
            "Local<int>.For(provider, out var myVar); _ = myVar.Value;",
            members);
        Assert.Contains("declare Integer MyVar for G_Provider;", output);
        Assert.Contains("MyVar", output);
    }

    [Fact]
    public void Emit_NetwriteFor_UsageKeepsPrefix()
    {
        const string members = "INetwriteProvider provider = null!;";
        var output = TranslateBodyMs(
            "Netwrite<int>.For(provider, out var score); score.Value = 5;",
            members);
        Assert.Contains("declare netwrite Integer Net_Score for G_Provider;", output);
        Assert.Contains("Net_Score = 5;", output);
    }
}
