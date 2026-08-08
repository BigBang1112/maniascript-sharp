using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>
/// Pre-pass that scans every ordinary method body for <c>OnChange(...)</c> calls and registers
/// their backing globals on <see cref="EmitContext.OnChangeGlobals"/>, so <see cref="GlobalEmitter"/>
/// can declare them before any call site is translated.
/// </summary>
internal sealed class OnChangeCollector
{
    private readonly EmitContext _ctx;
    public OnChangeCollector(EmitContext ctx) { _ctx = ctx; }

    public void Collect()
    {
        foreach (var m in _ctx.Info.Symbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (m.MethodKind != MethodKind.Ordinary) continue;
            if (m.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax decl) continue;
            if (decl.Body is null) continue;

            foreach (var inv in decl.Body.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (OnChangeSupport.TryMatch(_ctx.Model, inv, out var match))
                    _ctx.OnChangeGlobals[match.BackingName] = match.ValueType;
            }
        }
    }
}
