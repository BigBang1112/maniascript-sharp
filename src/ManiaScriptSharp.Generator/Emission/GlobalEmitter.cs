using ManiaScriptSharp.Generator.Naming;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>
/// Emits top-level <c>declare</c> globals (incl. <c>netwrite</c>/<c>netread</c>/<c>persistent</c>/<c>for</c>)
/// and registers field initialisers into the deferred main()-init list.
/// Also collects <c>[ManialinkControl]</c> fields for later wiring.
/// </summary>
internal sealed class GlobalEmitter
{
    private readonly EmitContext _ctx;
    public GlobalEmitter(EmitContext ctx) { _ctx = ctx; }

    public void Emit()
    {
        var any = false;
        foreach (var f in _ctx.Info.Symbol.GetMembers().OfType<IFieldSymbol>())
        {
            // Auto-property backing fields are compiler-generated. The property pass below
            // emits their intended ManiaScript backing declaration with a stable name.
            if (f.IsImplicitlyDeclared) continue;
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
            if (p.IsLibContextProperty()) continue;
            if (!IsUserDefinedAutoProperty(p)) continue;
            if (EmitAutoProperty(p)) any = true;
        }
        // Backing globals for OnChange(value, oldValue => { ... }) call sites (collected up-front).
        foreach (var kvp in _ctx.OnChangeGlobals)
        {
            _ctx.W.Line($"declare {TypeMapper.Map(kvp.Value)} {kvp.Key};");
            any = true;
        }
        if (any) _ctx.W.Line();
    }

    private bool EmitOne(IFieldSymbol f)
    {
        if (f.DeclaredAccessibility == Accessibility.Public && !f.HasAttr("ManialinkControlAttribute"))
        {
            _ctx.Report(Diagnostics.PublicField, f.Locations.FirstOrDefault(), f.Name);
        }

        // ManialinkControl — declare bare global, defer Page.GetFirstChild wiring to main().
        if (f.HasAttr("ManialinkControlAttribute"))
        {
            var attr = f.GetAttr("ManialinkControlAttribute")!;
            var xmlId = attr.Ctor<string>(0) ?? NameMangler.PascalCase(f.Name);
            var type = TypeMapper.Map(f.Type);
            var ignoreValidation = attr.Named<bool>("IgnoreValidation");
            var loc = f.Locations.FirstOrDefault();
            var controlName = ResolveGlobalName(f);
            _ctx.W.Line($"declare {type} {controlName};");
            _ctx.ManialinkBindings.Add(new ManialinkBinding(controlName, xmlId, type, ignoreValidation, loc));
            return true;
        }

        var name = ResolveGlobalName(f);
        var msType = TypeMapper.Map(f.Type);

        var initSyntax = TryGetInitializerSyntax(f);
        var assignmentInInitializer = initSyntax?.DescendantNodesAndSelf()
            .OfType<AssignmentExpressionSyntax>()
            .FirstOrDefault(assignment => !AssignmentSyntax.IsInitializerEntry(assignment));
        if (assignmentInInitializer is not null)
        {
            _ctx.Report(Diagnostics.NestedAssignment, assignmentInInitializer.GetLocation());
            _ctx.W.Line($"declare {msType} {name};");
        }
        else if (initSyntax is not null && _ctx.IsLib)
        {
            // Libraries have no main(), so C# initializers are never emitted into ManiaScript.
            // Empty text/collections are accepted as harmless defaults; other values are errors.
            if (!CanUseInitializerInLib(f.Type, initSyntax))
                _ctx.Report(Diagnostics.LibFieldInitializer, f.Locations.FirstOrDefault(),
                    $"{_ctx.Info.Symbol.Name}.{f.Name}");
            _ctx.W.Line($"declare {msType} {name};");
        }
        else if (initSyntax is not null)
        {
            // Global declarations must be bare; initialize context fields from main().
            _ctx.DeferredInits.Add(new DeferredInit(name, initSyntax));
            _ctx.W.Line($"declare {msType} {name};");
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

    private bool EmitAutoProperty(IPropertySymbol p)
    {
        var name = NameMangler.Global(p);
        var msType = TypeMapper.Map(p.Type);
        var initSyntax = TryGetInitializerSyntax(p);
        var assignmentInInitializer = initSyntax?.DescendantNodesAndSelf()
            .OfType<AssignmentExpressionSyntax>()
            .FirstOrDefault(assignment => !AssignmentSyntax.IsInitializerEntry(assignment));

        if (assignmentInInitializer is not null)
        {
            _ctx.Report(Diagnostics.NestedAssignment, assignmentInInitializer.GetLocation());
            _ctx.W.Line($"declare {msType} {name};");
        }
        else if (initSyntax is not null && _ctx.IsLib)
        {
            if (!CanUseInitializerInLib(p.Type, initSyntax))
                _ctx.Report(Diagnostics.LibFieldInitializer, p.Locations.FirstOrDefault(),
                    $"{_ctx.Info.Symbol.Name}.{p.Name}");
            _ctx.W.Line($"declare {msType} {name};");
        }
        else if (initSyntax is not null)
        {
            // Context globals cannot have declaration initializers; run these at the top of main().
            _ctx.DeferredInits.Add(new DeferredInit(name, initSyntax));
            _ctx.W.Line($"declare {msType} {name};");
        }
        else
        {
            _ctx.W.Line($"declare {msType} {name};");
        }

        return true;
    }

    private static string ResolveGlobalName(IFieldSymbol f) => NameMangler.Global(f);

    private static bool IsLibField(IFieldSymbol f) => f.IsLibImplementation();

    /// <summary>
    /// Empty text, lists, and maps are permitted C# defaults in a library, but libraries have
    /// no <c>main()</c>, so none of their initializer expressions are emitted to ManiaScript.
    /// </summary>
    private bool CanUseInitializerInLib(ITypeSymbol type, ExpressionSyntax initializer)
    {
        if ((TypeMapper.Map(type).EndsWith("[]", System.StringComparison.Ordinal)
             || ExpressionEmitter.IsDictionaryType(type as INamedTypeSymbol))
            && IsEmptyCollectionInitializer(initializer))
            return true;

        return type.SpecialType == SpecialType.System_String
            && IsEmptyTextInitializer(initializer);
    }

    private bool IsEmptyTextInitializer(ExpressionSyntax initializer)
    {
        if (initializer is LiteralExpressionSyntax { Token.ValueText.Length: 0 }) return true;

        return _ctx.Model.GetSymbolInfo(initializer).Symbol is IFieldSymbol
        {
            Name: "Empty",
            IsStatic: true,
            ContainingType.SpecialType: SpecialType.System_String,
        };
    }

    private static bool IsEmptyCollectionInitializer(ExpressionSyntax initializer) => initializer switch
    {
        CollectionExpressionSyntax { Elements.Count: 0 } => true,
        ObjectCreationExpressionSyntax creation
            when (creation.ArgumentList?.Arguments.Count ?? 0) == 0
                 && (creation.Initializer?.Expressions.Count ?? 0) == 0 => true,
        ImplicitObjectCreationExpressionSyntax creation
            when creation.ArgumentList.Arguments.Count == 0
                 && (creation.Initializer?.Expressions.Count ?? 0) == 0 => true,
        _ => false,
    };

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

    private static ExpressionSyntax? TryGetInitializerSyntax(IPropertySymbol p)
    {
        if (p.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is PropertyDeclarationSyntax property
            && property.Initializer is not null)
            return property.Initializer.Value;
        return null;
    }

}
