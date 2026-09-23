using Xunit;

namespace ManiaScriptSharp.Generator.Tests;

/// <summary>
/// Tests that plain functions (and property accessors) are reordered so every callee is
/// emitted before its caller — ManiaScript has no forward declarations and disallows circular
/// calls between distinct functions.
/// </summary>
public class FunctionEmitterTests : EmitterTestBase
{
    [Fact]
    public void Emit_Function_MutatedParameterGetsLocalCopy()
    {
        var output = EmitFunctions("int Adjust(int score, int step) { score += step; return score; }");

        Assert.Contains("Integer Private_Adjust(Integer __Input_Score, Integer _Step) {\n" +
            "    declare Integer _Score = __Input_Score;\n" +
            "    _Score += _Step;\n" +
            "    return _Score;\n}", output);
        Assert.DoesNotContain("declare Integer _Step", output);
    }

    [Fact]
    public void Emit_Function_ReadOnlyParametersKeepTheirNames()
    {
        var output = EmitFunctions("int Sum(int a, int b) => a + b;");

        Assert.Contains("Integer Private_Sum(Integer _A, Integer _B) {\n    return _A + _B;\n}", output);
        Assert.DoesNotContain("__Input", output);
    }

    [Fact]
    public void Emit_Function_IncrementAndConditionalAssignmentUseLocalCopy()
    {
        var output = EmitFunctions("int Adjust(int score, bool useBonus) { score++; score = useBonus ? 5 : score; return score; }");

        Assert.Contains("Integer Private_Adjust(Integer __Input_Score, Boolean _UseBonus)", output);
        Assert.Contains("declare Integer _Score = __Input_Score;", output);
        Assert.Contains("_Score += 1;", output);
        Assert.Contains("_Score = 5;", output);
        Assert.Contains("return _Score;", output);
    }

    [Fact]
    public void Emit_Function_ExpressionBodyMutationUsesLocalCopy()
    {
        var output = EmitFunctions("void Advance(int count) => count++;");

        Assert.Contains("Void Private_Advance(Integer __Input_Count) {\n" +
            "    declare Integer _Count = __Input_Count;\n" +
            "    _Count += 1;\n}", output);
    }

    [Fact]
    public void Emit_Function_PrefixIncrementUsesLocalCopy()
    {
        var output = EmitFunctions("void Advance(int count) { ++count; }");

        Assert.Contains("Void Private_Advance(Integer __Input_Count) {\n" +
            "    declare Integer _Count = __Input_Count;\n" +
            "    _Count += 1;\n}", output);
    }

    [Fact]
    public void Emit_Function_TupleAssignmentCopiesBothParameters()
    {
        var output = EmitFunctions("void Swap(int a, int b) { (a, b) = (b, a); }");

        Assert.Contains("Void Private_Swap(Integer __Input_A, Integer __Input_B)", output);
        Assert.Contains("declare Integer _A = __Input_A;", output);
        Assert.Contains("declare Integer _B = __Input_B;", output);
    }

    [Fact]
    public void Emit_Function_CollectionParameterMutationUsesLocalCopy()
    {
        var output = EmitFunctions("void Push(List<int> items) { items.Add(1); items[0] = 2; }");

        Assert.Contains("Void Private_Push(Integer[] __Input_Items) {", output);
        Assert.Contains("declare Integer[] _Items = __Input_Items;", output);
        Assert.Contains("_Items.add(1);", output);
        Assert.Contains("_Items[0] = 2;", output);
    }

    [Fact]
    public void Emit_Function_StructParameterFieldMutationUsesLocalCopy()
    {
        var output = EmitFunctions("struct State { public int Count; } int Update(State state) { state.Count += 1; return state.Count; }");

        Assert.Contains("Integer Private_Update(State __Input_State) {", output);
        Assert.Contains("declare State _State = __Input_State;", output);
        Assert.Contains("_State.Count += 1;", output);
        Assert.Contains("return _State.Count;", output);
    }

    [Fact]
    public void Emit_Setter_MutatedValueGetsLocalCopy()
    {
        var output = EmitFunctions("int score; public int Score { set { value++; score = value; } }");

        Assert.Contains("Void SetScore(Integer __Input_Value) {", output);
        Assert.Contains("declare Integer _Value = __Input_Value;", output);
        Assert.Contains("_Value += 1;", output);
        Assert.Contains("G_Score = _Value;", output);
    }

    [Fact]
    public void Emit_Functions_CalleeDeclaredAfterCaller_IsMovedAbove()
    {
        // C# order is Caller then Callee; ManiaScript output must have Callee first.
        // (private methods get a Private_ prefix from NameMangler.)
        var output = EmitFunctions(
            "void Caller() { Callee(); } void Callee() { }");
        Assert.True(
            output.IndexOf("Void Private_Callee()") < output.IndexOf("Void Private_Caller()"),
            output);
    }

    [Fact]
    public void Emit_Functions_AlreadyInDependencyOrder_IsUnchanged()
    {
        var output = EmitFunctions(
            "void Callee() { } void Caller() { Callee(); }");
        Assert.True(
            output.IndexOf("Void Private_Callee()") < output.IndexOf("Void Private_Caller()"),
            output);
    }

    [Fact]
    public void Emit_Functions_TransitiveChain_IsFullyOrdered()
    {
        // A -> B -> C, declared in reverse; expect C, B, A.
        var output = EmitFunctions(
            "void A() { B(); } void B() { C(); } void C() { }");
        var iA = output.IndexOf("Void Private_A()");
        var iB = output.IndexOf("Void Private_B()");
        var iC = output.IndexOf("Void Private_C()");
        Assert.True(iC < iB && iB < iA, output);
    }

    [Fact]
    public void Emit_Functions_SelfRecursion_DoesNotReportCycle()
    {
        var (_, diagnostics) = EmitFunctionsWithDiagnostics("void Foo(int n) { if (n > 0) Foo(n - 1); }");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Emit_Functions_PropertyCallingFunction_IsOrdered()
    {
        // Getter reads a helper function's result — helper must come first.
        var output = EmitFunctions(
            "public int Score { get { return Helper(); } } int Helper() { return 1; }");
        Assert.True(
            output.IndexOf("Integer Helper()") < output.IndexOf("Integer GetScore()"),
            output);
    }

    [Fact]
    public void Emit_Functions_CircularCall_ReportsDiagnostic()
    {
        var (_, diagnostics) = EmitFunctionsWithDiagnostics(
            "void A() { B(); } void B() { A(); }");
        Assert.Contains(diagnostics, d => d.Id == "MSS011");
    }

    [Fact]
    public void Emit_Labels_UseDefinitionBlocksAndScopedAdditiveCalls()
    {
        var output = EmitFunctions(
            "public virtual void AfterStart() { string message = \"Started\"; } void Run() { AfterStart(); }");

        Assert.Contains("***AfterStart***", output);
        Assert.Contains("declare Text Message = \"Started\";", output);
        Assert.Contains("Void Private_Run() {\n    {+++AfterStart+++}\n}", output);
    }

    [Fact]
    public void Emit_Labels_WithParametersOrReturnValues_ReportDiagnostic()
    {
        var (output, diagnostics) = EmitFunctionsWithDiagnostics(
            "public virtual int Compute(int value) => value;");

        Assert.Contains(diagnostics, d => d.Id == "MSS014");
        Assert.DoesNotContain("***Compute***", output);
    }
}
