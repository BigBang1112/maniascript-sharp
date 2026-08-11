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
}
