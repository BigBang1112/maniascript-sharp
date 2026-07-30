using ManiaScriptSharp.Generator.Naming;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>
/// Emits top-level <c>declare</c> globals (incl. <c>netwrite</c>/<c>netread</c>/<c>persistent</c>/<c>for</c>)
/// and registers any public-field initialisers into the deferred main()-init list.
/// Also collects <c>[ManialinkControl]</c> fields for later wiring.
/// </summary>
internal sealed class GlobalEmitter
{
    private readonly EmitContext _ctx;
    private readonly ExpressionEmitter _expr;

    public GlobalEmitter(EmitContext ctx, ExpressionEmitter expr) { _ctx = ctx; _expr = expr; }

    public void Emit()
    {
        var any = false;
        foreach (var f in _ctx.Info.Symbol.GetMembers().OfType<IFieldSymbol>())
        {
            if (f.IsConst) continue;
            if (f.HasAttr("SettingAttribute")) continue;
            if (IsLibField(f)) continue;
            if (EmitOne(f)) any = true;
        }
        // Properties with [ManialinkControl] (required properties pattern).
        foreach (var p in _ctx.Info.Symbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (p.HasAttr("ManialinkControlAttribute") && EmitManialinkProperty(p))
                any = true;
        }
        // Backing globals for user-defined auto-properties (e.g. `public int Score { get; set; }`).
        foreach (var p in _ctx.Info.Symbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (p.HasAttr("ManialinkControlAttribute")) continue;
            if (!IsUserDefinedAutoProperty(p)) continue;
            var msType = TypeMapper.Map(p.Type);
            var backing = p.DeclaredAccessibility == Accessibility.Public
                ? "G_" + NameMangler.PascalCase(p.Name)
                : NameMangler.PascalCase(p.Name);
            _ctx.W.Line($"declare {msType} {backing};");
            any = true;
        }
        if (any) _ctx.W.Line();
    }

    private bool EmitOne(IFieldSymbol f)
    {
        // ManialinkControl — declare bare global, defer Page.GetFirstChild wiring to main().
        if (f.HasAttr("ManialinkControlAttribute"))
        {
            var attr = f.GetAttr("ManialinkControlAttribute")!;
            var xmlId = attr.Ctor<string>(0) ?? NameMangler.PascalCase(f.Name);
            var type = TypeMapper.Map(f.Type);
            var ignoreValidation = attr.Named<bool>("IgnoreValidation");
            var loc = f.Locations.FirstOrDefault();
            _ctx.W.Line($"declare {type} {NameMangler.PascalCase(f.Name)};");
            _ctx.ManialinkBindings.Add(new ManialinkBinding(NameMangler.PascalCase(f.Name), xmlId, type, ignoreValidation, loc));
            return true;
        }

        var name = ResolveGlobalName(f);
        var msType = TypeMapper.Map(f.Type);

        var initSyntax = TryGetInitializerSyntax(f);
        if (initSyntax is not null && f.DeclaredAccessibility == Accessibility.Public)
        {
            // Public field initialisers move into main() per spec.
            _ctx.DeferredInits.Add(new DeferredInit(name, initSyntax));
            _ctx.W.Line($"declare {msType} {name};");
        }
        else if (initSyntax is not null)
        {
            _ctx.W.Line($"declare {msType} {name} = {_expr.Translate(initSyntax)};");
        }
        else
        {
            _ctx.W.Line($"declare {msType} {name};");
        }
        return true;
    }

    private bool EmitManialinkProperty(IPropertySymbol p)
    {
        var attr = p.GetAttr("ManialinkControlAttribute")!;
        var xmlId = attr.Ctor<string>(0) ?? NameMangler.PascalCase(p.Name);
        var type = TypeMapper.Map(p.Type);
        var ignoreValidation = attr.Named<bool>("IgnoreValidation");
        var loc = p.Locations.FirstOrDefault();
        _ctx.W.Line($"declare {type} {NameMangler.PascalCase(p.Name)};");
        _ctx.ManialinkBindings.Add(new ManialinkBinding(NameMangler.PascalCase(p.Name), xmlId, type, ignoreValidation, loc));
        return true;
    }

    private static string ResolveGlobalName(IFieldSymbol f) => NameMangler.Global(f);

    private static bool IsLibField(IFieldSymbol f) => f.IsLibImplementation();

    /// <summary>
    /// Returns true for user-defined auto-properties (declared in non-generated source, no accessor bodies).
    /// These need a backing <c>declare</c> global emitted.
    /// </summary>
    private static bool IsUserDefinedAutoProperty(IPropertySymbol p)
    {
        var syntaxRef = p.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef?.GetSyntax() is not PropertyDeclarationSyntax decl) return false;
        // Exclude API-generated properties (their source lives in .g.cs files).
        var path = syntaxRef.SyntaxTree.FilePath;
        if (path.EndsWith(".g.cs", System.StringComparison.OrdinalIgnoreCase)) return false;
        if (decl.ExpressionBody is not null) return false; // has explicit body → FunctionEmitter handles it
        if (decl.AccessorList is null) return false;
        // Auto-property: all accessors are bodyless (no Block, no ExpressionBody).
        return decl.AccessorList.Accessors.All(a => a.Body is null && a.ExpressionBody is null);
    }

    private static ExpressionSyntax? TryGetInitializerSyntax(IFieldSymbol f)
    {
        if (f.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is VariableDeclaratorSyntax v && v.Initializer is not null)
            return v.Initializer.Value;
        return null;
    }
}
