namespace ManiaScriptSharp.Generator.Emission;

/// <summary>
/// Top-level orchestrator: wires the sub-emitters together and produces the final ManiaScript text.
/// Each <c>Emit*</c> sub-emitter contributes a section. Order matters because of the deferred
/// public-field initialisers and the label/manialink-binding bookkeeping kept on <see cref="EmitContext"/>.
/// </summary>
internal sealed class ScriptEmitter
{
    private readonly EmitContext _ctx;

    /// <summary>Manialink control bindings collected during <see cref="Emit"/>. Available after emit completes.</summary>
    public IReadOnlyList<ManialinkBinding> ManialinkBindings => _ctx.ManialinkBindings;

    public ScriptEmitter(ContextClassInfo info, Microsoft.CodeAnalysis.SourceProductionContext spc, BuildSettings settings)
    {
        _ctx = new EmitContext(info, spc, settings);
    }

    /// <summary>Emits only the function/label bodies into an external writer (used for lib inlining).</summary>
    internal void EmitFunctionsOnly(IndentedWriter targetWriter)
    {
        var lit = new LiteralEmitter();
        var expr = new ExpressionEmitter(_ctx);
        var stmt = new StatementEmitter(_ctx, expr);
        var pat = new PatternEmitter(_ctx, expr, stmt);
        expr.Bind(pat);
        stmt.Bind(pat);
        var functions = new FunctionEmitter(_ctx, stmt, expr);
        functions.CollectLabels();
        functions.Emit();
        targetWriter.Raw(_ctx.W.ToString());
    }

    /// <summary>Emits only the <c>#Include</c> directives of this lib into an external writer, skipping any already in <paramref name="seenPaths"/>.</summary>
    internal void EmitDirectivesOnly(IndentedWriter targetWriter, HashSet<string> seenPaths)
    {
        new DirectivesEmitter(_ctx).EmitIncludesOnly(seenPaths);
        targetWriter.Raw(_ctx.W.ToString());
    }

    public string Emit()
    {
        var lit = new LiteralEmitter();
        var expr = new ExpressionEmitter(_ctx);
        var stmt = new StatementEmitter(_ctx, expr);
        var pat = new PatternEmitter(_ctx, expr, stmt);
        expr.Bind(pat);
        stmt.Bind(pat);

        var directives = new DirectivesEmitter(_ctx);
        var functions = new FunctionEmitter(_ctx, stmt, expr);

        if (_ctx.IsLib)
        {
            // Lib scripts only need: #RequireContext T, #Include for nested libs, and functions.
            directives.Emit();
            functions.CollectLabels();
            functions.Emit();
            return _ctx.W.ToString();
        }

        var structs = new StructEmitter(_ctx);
        var constsSettings = new ConstSettingEmitter(_ctx, lit);
        var globals = new GlobalEmitter(_ctx, expr);
        var events = new EventCollector(_ctx);
        var main = new MainEmitter(_ctx, stmt, expr, events);

        directives.Emit();

        // In manialink mode, emit lib #Include directives (e.g. TextLib) right after the consuming class's directives.
        if (_ctx.IsManialink)
            InlineLibDirectives();

        structs.Emit();
        constsSettings.Emit();

        // In manialink mode, user-defined ILib fields cannot be #Include'd;
        // emit their functions inline right after the directives section.
        if (_ctx.IsManialink)
            InlineLibFunctions();

        // Two-pass: register labels before any expression is translated so call sites are rewritten.
        functions.CollectLabels();

        globals.Emit();
        functions.Emit();
        main.Emit();

        return _ctx.W.ToString();
    }

    private IEnumerable<(string Name, ContextClassInfo Info, ScriptEmitter Emitter)> UserLibEmitters()
    {
        var emittedTypes = new HashSet<string>();
        foreach (var f in _ctx.Info.Symbol.GetMembers().OfType<Microsoft.CodeAnalysis.IFieldSymbol>())
        {
            if (f.IsStatic || f.IsConst) continue;
            if (!f.IsLibImplementation()) continue;
            if (f.Type is not Microsoft.CodeAnalysis.INamedTypeSymbol libType) continue;
            if (libType.ContainingNamespace?.ToDisplayString() == "ManiaScriptSharp") continue;
            if (!emittedTypes.Add(libType.Name)) continue;

            var syntaxRef = libType.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef?.GetSyntax() is not Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax classDecl) continue;
            var model = _ctx.Info.Model.Compilation.GetSemanticModel(syntaxRef.SyntaxTree);
            var libInfo = new ContextClassInfo(classDecl, libType, model);
            yield return (libType.Name, libInfo, new ScriptEmitter(libInfo, _ctx.Spc, _ctx.Settings));
        }
    }

    /// <summary>Emits the <c>#Include</c> directives from each inlined lib into the parent output.</summary>
    private void InlineLibDirectives()
    {
        foreach (var (_, _, emitter) in UserLibEmitters())
            emitter.EmitDirectivesOnly(_ctx.W, _ctx.EmittedIncludes);
    }

    /// <summary>Emits the functions from each inlined lib into the parent output (called near the top, after directives).</summary>
    private void InlineLibFunctions()
    {
        foreach (var (name, _, emitter) in UserLibEmitters())
        {
            _ctx.W.Line($"// Inlined lib: {name}");
            emitter.EmitFunctionsOnly(_ctx.W);
        }
    }
}
