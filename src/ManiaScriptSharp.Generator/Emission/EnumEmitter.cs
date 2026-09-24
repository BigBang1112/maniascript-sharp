using ManiaScriptSharp.Generator.Naming;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>Emits user-defined C# enum members as integer <c>#Const</c> directives.</summary>
internal sealed class EnumEmitter
{
    private readonly EmitContext _ctx;
    private readonly LiteralEmitter _literal = new();

    public EnumEmitter(EmitContext ctx) { _ctx = ctx; }

    public void Emit()
    {
        var enums = new List<INamedTypeSymbol>();
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        void Add(INamedTypeSymbol? type)
        {
            if (type is null || !EnumSupport.IsCustomEnum(type) || IsOwnedByAnotherLib(type)) return;
            if (seen.Add(type)) enums.Add(type);
        }

        foreach (var declaration in _ctx.Info.Declaration.DescendantNodesAndSelf().OfType<EnumDeclarationSyntax>())
            Add(_ctx.Model.GetDeclaredSymbol(declaration) as INamedTypeSymbol);

        // Include enums declared in other source files or referenced assemblies when this
        // script uses them. The semantic model resolves aliases and qualified names here.
        foreach (var name in _ctx.Info.Declaration.DescendantNodesAndSelf().OfType<SimpleNameSyntax>())
        {
            var symbol = _ctx.Model.GetSymbolInfo(name).Symbol;
            Add(symbol switch
            {
                INamedTypeSymbol type => type,
                IFieldSymbol field => field.ContainingType,
                IAliasSymbol { Target: INamedTypeSymbol type } => type,
                _ => null,
            });
        }

        var any = false;
        foreach (var type in enums)
        {
            foreach (var member in type.GetMembers().OfType<IFieldSymbol>())
            {
                if (!member.HasConstantValue) continue;
                _ctx.W.Line($"#Const {NameMangler.EnumConst(member)} {_literal.Format(member.ConstantValue)}");
                any = true;
            }
        }

        if (any) _ctx.W.Line();
    }

    private bool IsOwnedByAnotherLib(INamedTypeSymbol type)
    {
        for (var owner = type.ContainingType; owner is not null; owner = owner.ContainingType)
            if (TypeMapper.IsContextOrLibType(owner)
                && !SymbolEqualityComparer.Default.Equals(owner, _ctx.Info.Symbol))
                // A normal script accesses members of an included library through its alias,
                // so its enum constants must remain defined in that library. Manialinks inline
                // their user libraries, which is the sole case where definitions belong here.
                return _ctx.IsManialink || _ctx.TryGetLibraryAlias(owner, out _);
        return false;
    }
}
