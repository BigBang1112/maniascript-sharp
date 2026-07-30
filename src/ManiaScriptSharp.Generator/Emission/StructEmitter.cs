using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>Emits <c>#Struct</c> blocks for every C# <c>struct</c> type defined alongside the context.</summary>
internal sealed class StructEmitter
{
    private readonly EmitContext _ctx;
    public StructEmitter(EmitContext ctx) { _ctx = ctx; }

    public void Emit()
    {
        // Walk the same SyntaxTree as the context class to find sibling struct declarations.
        var root = _ctx.Info.Declaration.SyntaxTree.GetRoot();
        var types = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.StructDeclarationSyntax>()
            .Select(n => _ctx.Model.GetDeclaredSymbol(n) as INamedTypeSymbol)
            .Where(s => s is not null)
            .Distinct<INamedTypeSymbol?>(SymbolEqualityComparer.Default)
            .Cast<INamedTypeSymbol>()
            .ToList();

        if (types.Count == 0) return;
        foreach (var t in types) EmitOne(t);
    }

    private void EmitOne(INamedTypeSymbol t)
    {
        _ctx.W.Line($"#Struct {t.Name} {{");
        _ctx.W.Push();
        foreach (var f in t.GetMembers().OfType<IFieldSymbol>())
        {
            if (f.IsStatic || f.IsConst) continue;
            _ctx.W.Line($"{TypeMapper.Map(f.Type)} {Naming.NameMangler.PascalCase(f.Name)};");
        }
        _ctx.W.Pop();
        _ctx.W.Line("}");
        _ctx.W.Line();
    }
}
