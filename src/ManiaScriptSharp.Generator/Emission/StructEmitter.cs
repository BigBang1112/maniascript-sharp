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
            .Where(IsDeclaredHere)
            .ToList();

        if (types.Count == 0) return;
        var localTypes = new HashSet<INamedTypeSymbol>(types, SymbolEqualityComparer.Default);
        var dependencies = new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
        var emitted = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var type in types)
        {
            var required = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
            {
                if (field.IsStatic || field.IsConst) continue;
                foreach (var dependency in FieldStructTypes(field.Type))
                    if (localTypes.Contains(dependency)
                        && !SymbolEqualityComparer.Default.Equals(type, dependency))
                        required.Add(dependency);
            }
            dependencies.Add(type, required);
        }

        while (emitted.Count < types.Count)
        {
            // Choose the first declaration whose field types are ready, retaining source
            // order whenever dependencies do not require a move.
            var next = types.FirstOrDefault(type => !emitted.Contains(type)
                && dependencies[type].All(emitted.Contains));
            // A cycle cannot be ordered; still emit each definition exactly once.
            next ??= types.First(type => !emitted.Contains(type));
            EmitOne(next);
            emitted.Add(next);
        }
    }

    private bool IsDeclaredHere(INamedTypeSymbol type)
    {
        // A nested struct owned by another context/lib is imported through its include
        // alias by DirectivesEmitter, not redeclared in this script.
        return !(type.ContainingType is { } owner
            && !SymbolEqualityComparer.Default.Equals(owner, _ctx.Info.Symbol)
            && TypeMapper.IsContextOrLibType(owner));
    }

    private static IEnumerable<INamedTypeSymbol> FieldStructTypes(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
        {
            foreach (var element in FieldStructTypes(array.ElementType)) yield return element;
        }
        else if (type is INamedTypeSymbol named)
        {
            if (named.TypeKind == TypeKind.Struct) yield return named;
            foreach (var argument in named.TypeArguments)
                foreach (var element in FieldStructTypes(argument)) yield return element;
        }
    }

    private void EmitOne(INamedTypeSymbol t)
    {
        _ctx.W.Line($"#Struct {t.Name} {{");
        _ctx.W.Push();
        foreach (var f in t.GetMembers().OfType<IFieldSymbol>())
        {
            if (f.IsStatic || f.IsConst) continue;
            _ctx.W.Line($"{_ctx.MapType(f.Type)} {Naming.NameMangler.StructField(f)};");
        }
        _ctx.W.Pop();
        _ctx.W.Line("}");
        _ctx.W.Line();
    }
}
