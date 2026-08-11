using ManiaScriptSharp.Generator.Naming;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>
/// Emits regular ManiaScript functions (non-virtual ordinary methods) and the
/// <c>***Label***</c> blocks that virtual / override methods translate to.
/// </summary>
internal sealed class FunctionEmitter
{
    private readonly EmitContext _ctx;
    private readonly StatementEmitter _stmt;
    private readonly ExpressionEmitter _expr;

    public FunctionEmitter(EmitContext ctx, StatementEmitter stmt, ExpressionEmitter expr)
    { _ctx = ctx; _stmt = stmt; _expr = expr; }

    /// <summary>
    /// Pre-pass: register every virtual / override method as a label so call sites are
    /// rewritten to <c>+++Name+++</c> by ExpressionEmitter.
    /// </summary>
    public void CollectLabels()
    {
        foreach (var m in _ctx.Info.Symbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (m.MethodKind != MethodKind.Ordinary) continue;
            if (m.IsVirtual || m.IsOverride)
                _ctx.LabelMethods.Add(m.Name);
        }
    }

    public void Emit()
    {
        // Labels first (per common ManiaScript convention they live at the top of the script).
        foreach (var m in _ctx.Info.Symbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (m.MethodKind != MethodKind.Ordinary) continue;
            if (!m.IsVirtual && !m.IsOverride) continue;
            if (m.Name is "Main" or "Loop" or "Settings" or "UpdateSettings") continue;
            EmitLabel(m);
        }

        foreach (var m in _ctx.Info.Symbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (m.MethodKind != MethodKind.Ordinary) continue;
            if (m.IsVirtual || m.IsOverride) continue;
            if (m.Name is "Main" or "Loop") continue;
            EmitFunction(m);
        }

        // Emit Get/Set functions for each C# property that has a body.
        foreach (var p in _ctx.Info.Symbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (p.HasAttr("ManialinkControlAttribute")) continue;
            if (!IsUserDefinedProperty(p)) continue;
            EmitPropertyAccessors(p);
        }
    }

    /// <summary>
    /// Returns true for properties defined in user source code (non-generated files).
    /// Excludes properties from <c>.g.cs</c> files (API-generated) and compiled assemblies.
    /// Safe to key off source file alone here: this is only ever called for members of
    /// <c>_ctx.Info.Symbol</c> itself (see <see cref="Emit"/> below), never inherited properties.
    /// </summary>
    private static bool IsUserDefinedProperty(IPropertySymbol p)
    {
        var syntaxRef = p.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef?.GetSyntax() is not PropertyDeclarationSyntax) return false;
        if (p.ContainingType.TypeKind == Microsoft.CodeAnalysis.TypeKind.Interface) return false;
        if (p.IsLibContextProperty()) return false;
        var path = syntaxRef.SyntaxTree.FilePath;
        return !path.EndsWith(".g.cs", System.StringComparison.OrdinalIgnoreCase);
    }

    private void EmitPropertyAccessors(IPropertySymbol p)
    {
        var syntaxRef = p.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef?.GetSyntax() is not PropertyDeclarationSyntax decl) return;

        var msType = TypeMapper.Map(p.Type);
        var getName = NameMangler.Getter(p);
        var setName = NameMangler.Setter(p);

        // Getter
        if (p.GetMethod is not null && !p.GetMethod.IsAbstract)
        {
            if (decl.ExpressionBody is { } eb)
            {
                // `=> expr` — whole property is a getter expression body.
                _ctx.W.Line($"{msType} {getName}() {{");
                _ctx.W.Push();
                _ctx.W.Line($"return {_expr.Translate(eb.Expression)};");
                _ctx.W.Pop();
                _ctx.W.Line("}");
                _ctx.W.Line();
            }
            else if (decl.AccessorList is { } al)
            {
                var getter = al.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
                if (getter is not null)
                {
                    _ctx.W.Line($"{msType} {getName}() {{");
                    _ctx.W.Push();
                    if (getter.Body is not null || getter.ExpressionBody is not null)
                        EmitAccessorBody(getter, returnsVoid: false);
                    else
                        _ctx.W.Line($"return {AutoPropBacking(p)};"); // auto-property
                    _ctx.W.Pop();
                    _ctx.W.Line("}");
                    _ctx.W.Line();
                }
            }
        }

        // Setter
        if (p.SetMethod is not null && !p.SetMethod.IsAbstract
            && decl.AccessorList is { } accessorList)
        {
            var setter = accessorList.Accessors.FirstOrDefault(a =>
                a.IsKind(SyntaxKind.SetAccessorDeclaration) || a.IsKind(SyntaxKind.InitAccessorDeclaration));
            if (setter is not null)
            {
                _ctx.W.Line($"Void {setName}({msType} _Value) {{");
                _ctx.W.Push();
                if (setter.Body is not null || setter.ExpressionBody is not null)
                    EmitAccessorBody(setter, returnsVoid: true);
                else
                    _ctx.W.Line($"{AutoPropBacking(p)} = _Value;"); // auto-property
                _ctx.W.Pop();
                _ctx.W.Line("}");
                _ctx.W.Line();
            }
        }
    }

    /// <summary>
    /// Returns the ManiaScript backing variable name for an auto-property.
    /// Mirrors <see cref="NameMangler.Global"/> convention: public → <c>G_Name</c>, others → <c>Name</c>.
    /// </summary>
    private static string AutoPropBacking(IPropertySymbol p)
    {
        var pascal = NameMangler.PascalCase(p.Name);
        return p.DeclaredAccessibility == Accessibility.Public ? "G_" + pascal : pascal;
    }

    private void EmitAccessorBody(AccessorDeclarationSyntax accessor, bool returnsVoid)
    {
        if (accessor.Body is { } body)
        {
            foreach (var s in body.Statements) _stmt.Emit(s);
        }
        else if (accessor.ExpressionBody is { } eb)
        {
            if (returnsVoid) _ctx.W.Line(_expr.Translate(eb.Expression) + ";");
            else _ctx.W.Line($"return {_expr.Translate(eb.Expression)};");
        }
    }

    private void EmitFunction(IMethodSymbol m)
    {
        var ret = TypeMapper.Map(m.ReturnType);
        var name = NameMangler.Method(m);
        var ps = string.Join(", ", m.Parameters.Select(p => $"{TypeMapper.Map(p.Type)} {NameMangler.Parameter(p)}"));
        _ctx.W.Line($"{ret} {name}({ps}) {{");
        _ctx.W.Push();
        EmitBody(m);
        _ctx.W.Pop();
        _ctx.W.Line("}");
        _ctx.W.Line();
    }

    private void EmitLabel(IMethodSymbol m)
    {
        _ctx.W.Line($"***{m.Name}***");
        _ctx.W.Line("***");
        EmitBody(m);
        _ctx.W.Line("***");
        _ctx.W.Line();
    }

    private void EmitBody(IMethodSymbol m)
    {
        var syntaxRef = m.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef?.GetSyntax() is not MethodDeclarationSyntax decl) return;
        if (decl.Body is { } body)
        {
            foreach (var s in body.Statements) _stmt.Emit(s);
        }
        else if (decl.ExpressionBody is { } eb)
        {
            if (m.ReturnsVoid) _ctx.W.Line(_expr.Translate(eb.Expression) + ";");
            else _ctx.W.Line($"return {_expr.Translate(eb.Expression)};");
        }
    }
}
