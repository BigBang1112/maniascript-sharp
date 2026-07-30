using System.Collections.Generic;
using System.Linq;
using ManiaScriptSharp.Generator.Naming;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>
/// Desugars C# LINQ chains into ManiaScript <c>foreach</c> / <c>for</c> loops at the statement level.
/// Only invoked from <see cref="StatementEmitter"/> when a local variable is initialised
/// with a LINQ expression that contains at least one lambda (predicate or selector).
///
/// <para>Supported stages: <c>Where</c>, <c>Select</c>, <c>Distinct</c>, <c>OrderBy/OrderByDescending</c>,
/// <c>SelectMany</c>, <c>Skip</c>, <c>Take</c>.</para>
/// <para>Supported terminals: <c>Count</c>, <c>Any</c>, <c>All</c>, <c>Sum</c>,
/// <c>First/FirstOrDefault</c>, <c>Last/LastOrDefault</c>, <c>Single/SingleOrDefault</c>,
/// <c>ToList/ToArray</c>, <c>ToDictionary</c>, <c>Contains</c>, <c>Aggregate</c>,
/// <c>Min</c>, <c>Max</c>, <c>Zip</c>,
/// or no terminal (materialise to array).</para>
/// <para>All unsupported patterns emit <see cref="Diagnostics.UnsupportedLinq"/>.</para>
/// </summary>
internal sealed class LinqChainEmitter
{
    // ── Internal model ──────────────────────────────────────────────────────

    private enum StageKind { Where, Select, OrderBy, OrderByDescending, Distinct, SelectMany, Skip, Take }

    private enum TerminalKind
    {
        Materialize,
        Count, Any, All, Sum,
        First, FirstOrDefault,
        Last, LastOrDefault,
        Single, SingleOrDefault,
        ToDictionary,
        Contains,
        Aggregate, AggregateNoSeed,
        Min, Max,
        Zip,
        SelectManyTerminal,
    }

    private class Stage
    {
        public StageKind Kind { get; }
        public LambdaExpressionSyntax? Lambda { get; }
        /// <summary>Non-lambda constant argument for <c>Skip</c>/<c>Take</c> stages.</summary>
        public ExpressionSyntax? ConstArg { get; set; }
        public Stage(StageKind kind, LambdaExpressionSyntax? lambda) { Kind = kind; Lambda = lambda; }
    }

    private sealed class SelectManyStage : Stage
    {
        /// <summary>Optional result selector lambda: <c>(outer, inner) =&gt; result</c>.</summary>
        public LambdaExpressionSyntax? ResultLambda { get; }
        public SelectManyStage(LambdaExpressionSyntax collectionSelector, LambdaExpressionSyntax? resultLambda)
            : base(StageKind.SelectMany, collectionSelector) { ResultLambda = resultLambda; }
    }

    private sealed class LinqChain
    {
        public ExpressionSyntax Source { get; }
        public IReadOnlyList<Stage> Stages { get; }
        public TerminalKind Terminal { get; }
        public LambdaExpressionSyntax? TerminalLambda { get; }
        /// <summary>Second lambda argument for terminals that take two lambdas (e.g. ToDictionary value selector, Zip result selector, SelectMany result selector).</summary>
        public LambdaExpressionSyntax? TerminalLambda2 { get; }
        /// <summary>First non-lambda argument for terminals/stages that take a value (e.g. Contains value, Aggregate seed, Zip second source).</summary>
        public ExpressionSyntax? TerminalArg { get; }
        public LinqChain(ExpressionSyntax source, IReadOnlyList<Stage> stages,
            TerminalKind terminal, LambdaExpressionSyntax? terminalLambda,
            LambdaExpressionSyntax? terminalLambda2 = null, ExpressionSyntax? terminalArg = null)
        { Source = source; Stages = stages; Terminal = terminal; TerminalLambda = terminalLambda;
          TerminalLambda2 = terminalLambda2; TerminalArg = terminalArg; }
    }

    // ── Fields ──────────────────────────────────────────────────────────────

    private readonly EmitContext _ctx;
    private readonly ExpressionEmitter _expr;
    /// <summary>
    /// Cached result of translating the Select lambda body under pre-select bindings,
    /// set by <see cref="BindParamsSplit"/> when the select param name collides with a
    /// post-select param name. Used by <see cref="BeginBody"/> instead of re-translating.
    /// </summary>
    private string? _preTranslatedSelectBody;

    public LinqChainEmitter(EmitContext ctx, ExpressionEmitter expr) { _ctx = ctx; _expr = expr; }

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// If <paramref name="initExpr"/> is a LINQ chain with <em>no</em> materialising terminal,
    /// registers it under <paramref name="csharpVarName"/> in <see cref="EmitContext.PendingLinqChains"/>
    /// and returns <c>true</c> — suppressing any immediate code generation.
    /// When that variable is later used as the source of a chain that <em>does</em> have a terminal,
    /// <see cref="TryEmit"/> inlines the stored stages at that point.
    /// </summary>
    public bool TryRegister(ExpressionSyntax initExpr, string csharpVarName)
    {
        if (initExpr is not InvocationExpressionSyntax inv) return false;
        if (inv.Expression is not MemberAccessExpressionSyntax ma) return false;
        var sym = _ctx.Model.GetSymbolInfo(ma).Symbol as IMethodSymbol;
        if (sym is null || !IsLinq(sym)) return false;
        // If there is already an explicit terminal, let TryEmit handle it immediately.
        if (TryParseTerminal(inv, sym, out _, out _, out _, out _)) return false;
        // No terminal — defer: remember the expression and suppress the declaration.
        _ctx.PendingLinqChains[csharpVarName] = initExpr;
        return true;
    }

    /// <summary>
    /// If <paramref name="initExpr"/> is a lambda-bearing LINQ chain, emits the desugared
    /// ManiaScript foreach loop and the result variable declaration, then returns <c>true</c>.
    /// Returns <c>false</c> when the expression is not a LINQ chain or has no lambdas
    /// (handled by the simpler <see cref="ExpressionEmitter.MapLinqMethod"/>).
    /// </summary>
    public bool TryEmit(ExpressionSyntax initExpr, string varName, ITypeSymbol? varType)
    {
        if (!TryBuildChain(initExpr, out var chain) || chain is null) return false;
        EmitChain(chain, varName, varType);
        return true;
    }

    // ── Chain building ──────────────────────────────────────────────────────

    private bool TryBuildChain(ExpressionSyntax expr, out LinqChain? chain)
    {
        chain = null;
        if (expr is not InvocationExpressionSyntax outerInv) return false;
        if (outerInv.Expression is not MemberAccessExpressionSyntax outerMa) return false;
        var outerSym = _ctx.Model.GetSymbolInfo(outerMa).Symbol as IMethodSymbol;
        if (outerSym is null || !IsLinq(outerSym)) return false;

        // The outermost call must be a recognised terminal (ToList/ToArray for materialisation,
        // or an aggregate: Count/Any/All/Sum/First/Last).  A bare chain without an explicit
        // terminal (e.g. nums.Where(x => x > 0) with no .ToList()) is not handled here —
        // it falls through to the expression emitter which emits an MSS008 diagnostic.
        if (!TryParseTerminal(outerInv, outerSym, out var terminal, out var termLambda, out var termLambda2, out var termArg))
            return false;

        ExpressionSyntax current = outerMa.Expression; // receiver of the terminal call

        // Walk inward collecting stages in source-to-terminal order.
        var stages = new List<Stage>();
        while (current is InvocationExpressionSyntax si
               && si.Expression is MemberAccessExpressionSyntax sm)
        {
            var ss = _ctx.Model.GetSymbolInfo(sm).Symbol as IMethodSymbol;
            if (ss is null || !IsLinq(ss)) break;
            if (!TryParseStage(si, ss, out var stage)) break;
            stages.Insert(0, stage); // prepend so stages end up source-to-terminal
            current = sm.Expression;
        }

        // Inline any pending lazy chains: if the innermost source resolved to a variable that
        // was registered via TryRegister (no terminal at that point), expand its stages here.
        InlinePendingChains(ref current, stages);

        // Special case: if a Materialize terminal wraps a Zip call that was not consumed as a
        // stage, promote Zip to the real terminal and unwrap it (e.g. nums.Zip(strs, sel).ToList()).
        if (terminal == TerminalKind.Materialize
            && current is InvocationExpressionSyntax zipInv
            && zipInv.Expression is MemberAccessExpressionSyntax zipMa)
        {
            var zipSym = _ctx.Model.GetSymbolInfo(zipMa).Symbol as IMethodSymbol;
            if (zipSym is not null && IsLinq(zipSym) && zipSym.Name == "Zip"
                && zipInv.ArgumentList.Arguments.Count >= 1)
            {
                termArg    = zipInv.ArgumentList.Arguments[0].Expression;
                termLambda = zipInv.ArgumentList.Arguments.Count >= 2
                    ? zipInv.ArgumentList.Arguments[1].Expression as LambdaExpressionSyntax : null;
                terminal   = TerminalKind.Zip;
                current    = zipMa.Expression; // true source
            }
        }

        // Only desugar when there is at least one lambda somewhere, OR when the terminal
        // itself requires foreach desugaring even without a lambda (Min/Max no-selector,
        // AggregateNoSeed, Contains with a value arg).
        var hasLambda = stages.Any(s => s.Lambda is not null) || termLambda is not null;
        var terminalRequiresDesugar = terminal is TerminalKind.Min or TerminalKind.Max
            or TerminalKind.AggregateNoSeed or TerminalKind.Contains
            or TerminalKind.SelectManyTerminal;
        if (!hasLambda && stages.Count == 0 && !terminalRequiresDesugar) return false;

        chain = new LinqChain(current, stages, terminal, termLambda, termLambda2, termArg);
        return true;
    }

    /// <summary>
    /// If <paramref name="current"/> is an identifier that maps to a pending lazy LINQ chain,
    /// expands its stages (prepending them to <paramref name="stages"/>) and advances
    /// <paramref name="current"/> to the ultimate source. Repeats until the source is not pending.
    /// </summary>
    private void InlinePendingChains(ref ExpressionSyntax current, List<Stage> stages)
    {
        while (current is IdentifierNameSyntax id)
        {
            var localSym = _ctx.Model.GetSymbolInfo(id).Symbol as ILocalSymbol;
            if (localSym is null || !_ctx.PendingLinqChains.TryGetValue(localSym.Name, out var pendingExpr))
                break;

            // Walk the pending expression to extract its stages and true source.
            var pendingStages = new List<Stage>();
            ExpressionSyntax pendingCurrent = pendingExpr;
            while (pendingCurrent is InvocationExpressionSyntax pi
                   && pi.Expression is MemberAccessExpressionSyntax pm)
            {
                var ps = _ctx.Model.GetSymbolInfo(pm).Symbol as IMethodSymbol;
                if (ps is null || !IsLinq(ps)) break;
                if (!TryParseStage(pi, ps, out var pStage)) break;
                pendingStages.Insert(0, pStage);
                pendingCurrent = pm.Expression;
            }

            stages.InsertRange(0, pendingStages);
            current = pendingCurrent;
            // Loop: the new source may itself be another pending chain.
        }
    }

    private static bool TryParseTerminal(InvocationExpressionSyntax inv, IMethodSymbol sym,
        out TerminalKind kind, out LambdaExpressionSyntax? lambda,
        out LambdaExpressionSyntax? lambda2, out ExpressionSyntax? arg)
    {
        lambda  = FirstLambda(inv.ArgumentList);
        lambda2 = null;
        arg     = null;
        switch (sym.Name)
        {
            case "Count":          kind = TerminalKind.Count;          return true;
            case "Any":            kind = TerminalKind.Any;            return true;
            case "All":            kind = TerminalKind.All;            return true;
            case "Sum":            kind = TerminalKind.Sum;            return true;
            case "First":          kind = TerminalKind.First;          return true;
            case "FirstOrDefault": kind = TerminalKind.FirstOrDefault; return true;
            case "Last":           kind = TerminalKind.Last;           return true;
            case "LastOrDefault":  kind = TerminalKind.LastOrDefault;  return true;
            case "Single":         kind = TerminalKind.Single;         return true;
            case "SingleOrDefault": kind = TerminalKind.SingleOrDefault; return true;
            case "Min":            kind = TerminalKind.Min;            return true;
            case "Max":            kind = TerminalKind.Max;            return true;
            case "Contains" when inv.ArgumentList.Arguments.Count >= 1:
                lambda = null;
                // value argument — may be a lambda-less expression
                arg = inv.ArgumentList.Arguments[0].Expression as ExpressionSyntax
                    ?? inv.ArgumentList.Arguments[0].Expression;
                kind = TerminalKind.Contains; return true;
            case "Aggregate":
                // Seeded: Aggregate(seed, (acc,x) => ...) — first arg is seed, second is lambda.
                if (inv.ArgumentList.Arguments.Count >= 2
                    && inv.ArgumentList.Arguments[1].Expression is LambdaExpressionSyntax seedLambda)
                {
                    lambda = seedLambda;
                    arg    = inv.ArgumentList.Arguments[0].Expression;
                    kind   = TerminalKind.Aggregate; return true;
                }
                // No-seed: Aggregate((acc,x) => ...)
                kind = TerminalKind.AggregateNoSeed; return true;
            case "ToDictionary":
                // One-arg: ToDictionary(keySelector)
                // Two-arg: ToDictionary(keySelector, valueSelector)
                if (inv.ArgumentList.Arguments.Count >= 2)
                    lambda2 = inv.ArgumentList.Arguments[1].Expression as LambdaExpressionSyntax;
                kind = TerminalKind.ToDictionary; return true;
            case "Zip" when inv.ArgumentList.Arguments.Count >= 1:
                // Zip(second, resultSelector)
                arg = inv.ArgumentList.Arguments[0].Expression;
                if (inv.ArgumentList.Arguments.Count >= 2)
                    lambda = inv.ArgumentList.Arguments[1].Expression as LambdaExpressionSyntax;
                // result selector's first param is from source, second from second source
                lambda2 = null; // reuse TerminalLambda only; second param resolved via FirstParamName2
                kind = TerminalKind.Zip; return true;
            case "SelectMany":
                // SelectMany used as a terminal (e.g. items.SelectMany(x => x.Items)).
                lambda = FirstLambda(inv.ArgumentList);
                if (inv.ArgumentList.Arguments.Count >= 2)
                    lambda2 = inv.ArgumentList.Arguments[1].Expression as LambdaExpressionSyntax;
                kind = TerminalKind.SelectManyTerminal; return true;
            case "ToList" or "ToArray" when inv.ArgumentList.Arguments.Count == 0:
                lambda = null; kind = TerminalKind.Materialize; return true;
            default:
                lambda = null; kind = TerminalKind.Materialize; return false;
        }
    }

    private static bool TryParseStage(InvocationExpressionSyntax inv, IMethodSymbol sym, out Stage stage)
    {
        stage = null!;
        switch (sym.Name)
        {
            case "Where":
                var wl = FirstLambda(inv.ArgumentList);
                if (wl is null) return false;
                stage = new Stage(StageKind.Where, wl); return true;
            case "Select":
                var sl = FirstLambda(inv.ArgumentList);
                if (sl is null) return false;
                stage = new Stage(StageKind.Select, sl); return true;
            case "OrderBy" or "ThenBy":
                stage = new Stage(StageKind.OrderBy, FirstLambda(inv.ArgumentList)); return true;
            case "OrderByDescending" or "ThenByDescending":
                stage = new Stage(StageKind.OrderByDescending, FirstLambda(inv.ArgumentList)); return true;
            case "Distinct" when inv.ArgumentList.Arguments.Count == 0:
                stage = new Stage(StageKind.Distinct, null); return true;
            case "SelectMany":
                var sml = FirstLambda(inv.ArgumentList);
                if (sml is null) return false;
                // Store optional result selector as Lambda2 via a subclass-less trick:
                // pack both lambdas as a SelectMany stage; Lambda holds collection selector,
                // extra lambda stored separately via SelectManyResultLambda on Stage.
                var sml2 = inv.ArgumentList.Arguments.Count >= 2
                    ? inv.ArgumentList.Arguments[1].Expression as LambdaExpressionSyntax : null;
                stage = new SelectManyStage(sml, sml2); return true;
            case "Skip" when inv.ArgumentList.Arguments.Count == 1
                            && inv.ArgumentList.Arguments[0].Expression is not LambdaExpressionSyntax:
                stage = new Stage(StageKind.Skip, null) { ConstArg = inv.ArgumentList.Arguments[0].Expression }; return true;
            case "Take" when inv.ArgumentList.Arguments.Count == 1
                            && inv.ArgumentList.Arguments[0].Expression is not LambdaExpressionSyntax:
                stage = new Stage(StageKind.Take, null) { ConstArg = inv.ArgumentList.Arguments[0].Expression }; return true;
            default: return false;
        }
    }

    private static LambdaExpressionSyntax? FirstLambda(ArgumentListSyntax args)
        => args.Arguments.Count > 0 ? args.Arguments[0].Expression as LambdaExpressionSyntax : null;

    // ── Chain emission ──────────────────────────────────────────────────────

    private void EmitChain(LinqChain chain, string varName, ITypeSymbol? varType)
    {
        var source   = _expr.Translate(chain.Source);
        var loopVar  = ResolveLoopVar(chain);

        // SelectMany and Zip are handled separately — they don't follow the standard
        // pre/post filter + single-source loop pattern.
        if (chain.Terminal == TerminalKind.Zip)
        {
            EmitZip(varName, varType, source, chain.TerminalArg!, chain.TerminalLambda, loopVar);
            return;
        }

        if (chain.Terminal == TerminalKind.SelectManyTerminal)
        {
            // Wrap the terminal lambdas into a synthetic SelectManyStage so EmitSelectMany can handle it.
            var syntheticSm = new SelectManyStage(chain.TerminalLambda!, chain.TerminalLambda2);
            EmitSelectMany(varName, varType, source, chain.Stages, syntheticSm, loopVar, null);
            return;
        }

        var selectManyStage = chain.Stages.OfType<SelectManyStage>().FirstOrDefault();
        if (selectManyStage is not null)
        {
            EmitSelectMany(varName, varType, source, chain.Stages, selectManyStage, loopVar, chain.TerminalLambda);
            return;
        }

        // Split Where stages into pre-Select and post-Select groups.
        // Post-Select Where predicates operate on the projected value, not the raw element.
        Stage? select = null;
        var preFilters  = new List<Stage>();
        var postFilters = new List<Stage>();
        // Skip/Take stages are separated out for post-loop handling.
        Stage? skipStage = null;
        Stage? takeStage = null;
        foreach (var s in chain.Stages)
        {
            if (s.Kind == StageKind.Select) { select = s; continue; }
            if (s.Kind == StageKind.Where)
                (select is null ? preFilters : postFilters).Add(s);
            if (s.Kind == StageKind.Skip) skipStage = s;
            if (s.Kind == StageKind.Take) takeStage = s;
        }
        var distinct = chain.Stages.Any(s => s.Kind == StageKind.Distinct);
        var sortDesc = chain.Stages.Any(s => s.Kind == StageKind.OrderByDescending);
        var sortAsc  = chain.Stages.Any(s => s.Kind == StageKind.OrderBy);

        // When post-Select Where stages exist, the projected value needs its own variable so
        // the guards can reference it.  Derive the name from the first post-Where lambda param;
        // append "Sel" if that would clash with the loop variable name.
        string? projVar = null;
        if (select is not null && postFilters.Count > 0)
        {
            var postParam = postFilters
                .Select(f => FirstParamName(f.Lambda))
                .FirstOrDefault(p => p is not null);
            var candidate = postParam is not null ? NameMangler.Local(postParam) : loopVar + "Sel";
            projVar = candidate == loopVar ? loopVar + "Sel" : candidate;
        }

        using var scope = BindParamsSplit(loopVar, preFilters, select, postFilters, projVar, chain.TerminalLambda);

        switch (chain.Terminal)
        {
            case TerminalKind.Materialize:
                EmitMaterialize(varName, varType, source, loopVar, preFilters, select, projVar, postFilters, distinct, sortAsc || sortDesc, sortDesc, skipStage, takeStage);
                break;
            case TerminalKind.Count:
                EmitCount(varName, source, loopVar, preFilters, select, projVar, postFilters, chain.TerminalLambda);
                break;
            case TerminalKind.Any:
                EmitAny(varName, source, loopVar, preFilters, select, projVar, postFilters, chain.TerminalLambda);
                break;
            case TerminalKind.All:
                EmitAll(varName, source, loopVar, preFilters, select, projVar, postFilters, chain.TerminalLambda);
                break;
            case TerminalKind.Sum:
                EmitSum(varName, varType, source, loopVar, preFilters, select, projVar, postFilters, chain.TerminalLambda);
                break;
            case TerminalKind.First or TerminalKind.FirstOrDefault:
                EmitFirst(varName, varType, source, loopVar, preFilters, select, projVar, postFilters, chain.TerminalLambda);
                break;
            case TerminalKind.Last or TerminalKind.LastOrDefault:
                EmitLast(varName, varType, source, loopVar, preFilters, select, projVar, postFilters, chain.TerminalLambda);
                break;
            case TerminalKind.Single:
                EmitSingle(varName, varType, source, loopVar, preFilters, select, projVar, postFilters, chain.TerminalLambda, requireMatch: true);
                break;
            case TerminalKind.SingleOrDefault:
                EmitSingle(varName, varType, source, loopVar, preFilters, select, projVar, postFilters, chain.TerminalLambda, requireMatch: false);
                break;
            case TerminalKind.ToDictionary:
                EmitToDictionary(varName, varType, source, loopVar, preFilters, select, projVar, postFilters, chain.TerminalLambda, chain.TerminalLambda2);
                break;
            case TerminalKind.Contains:
                EmitContains(varName, source, loopVar, preFilters, select, projVar, postFilters, chain.TerminalArg!);
                break;
            case TerminalKind.Aggregate:
                EmitAggregate(varName, varType, source, loopVar, preFilters, select, projVar, postFilters, chain.TerminalLambda!, chain.TerminalArg!);
                break;
            case TerminalKind.AggregateNoSeed:
                EmitAggregateNoSeed(varName, varType, source, loopVar, preFilters, select, projVar, postFilters, chain.TerminalLambda!);
                break;
            case TerminalKind.Min:
                EmitMinMax(varName, varType, source, loopVar, preFilters, select, projVar, postFilters, chain.TerminalLambda, isMax: false);
                break;
            case TerminalKind.Max:
                EmitMinMax(varName, varType, source, loopVar, preFilters, select, projVar, postFilters, chain.TerminalLambda, isMax: true);
                break;
        }
    }

    // ── Terminal emitters ───────────────────────────────────────────────────

    private void EmitMaterialize(string varName, ITypeSymbol? varType, string source, string loopVar,
        List<Stage> preFilters, Stage? select, string? projVar, List<Stage> postFilters,
        bool distinct, bool needsSort, bool sortDesc, Stage? skipStage = null, Stage? takeStage = null)
    {
        var elemType = GetElementType(varType, select);

        // Pure Skip/Take with no Where: use a for loop with index bounds.
        if (skipStage is not null || takeStage is not null)
        {
            var skipArg = skipStage?.ConstArg is not null ? _expr.Translate(skipStage.ConstArg) : "0";
            _ctx.W.Line($"declare {elemType}[] {varName};");
            if (takeStage?.ConstArg is not null)
            {
                var takeArg = _expr.Translate(takeStage.ConstArg);
                _ctx.W.Line($"declare Integer {varName}SkipI = 0;");
                _ctx.W.Line($"foreach ({loopVar} in {source}) {{");
                _ctx.W.Push();
                int preG2 = OpenGuards(preFilters);
                _ctx.W.Line($"if ({varName}SkipI < {skipArg}) {{ {varName}SkipI += 1; }} else {{");
                _ctx.W.Push();
                _ctx.W.Line($"{varName}.add({loopVar});");
                _ctx.W.Line($"if ({varName}.count >= {takeArg}) break;");
                _ctx.W.Pop();
                _ctx.W.Line("}");
                CloseGuards(preG2);
                _ctx.W.Pop();
                _ctx.W.Line("}");
            }
            else
            {
                // Skip only — iterate from skipArg onward
                _ctx.W.Line($"declare Integer {varName}SkipI = 0;");
                _ctx.W.Line($"foreach ({loopVar} in {source}) {{");
                _ctx.W.Push();
                _ctx.W.Line($"if ({varName}SkipI < {skipArg}) {{ {varName}SkipI += 1; continue; }}");
                int preG3 = OpenGuards(preFilters);
                _ctx.W.Line($"{varName}.add({loopVar});");
                CloseGuards(preG3);
                _ctx.W.Pop();
                _ctx.W.Line("}");
            }
            return;
        }

        _ctx.W.Line($"declare {elemType}[] {varName};");
        _ctx.W.Line($"foreach ({loopVar} in {source}) {{");
        _ctx.W.Push();

        var (item, preG, postG) = BeginBody(preFilters, select, loopVar, projVar, postFilters);

        if (distinct)
        {
            _ctx.W.Line($"if (!{varName}.exists({item})) {{");
            _ctx.W.Push();
            _ctx.W.Line($"{varName}.add({item});");
            _ctx.W.Pop();
            _ctx.W.Line("}");
        }
        else
        {
            _ctx.W.Line($"{varName}.add({item});");
        }

        EndBody(preG, postG);
        _ctx.W.Pop();
        _ctx.W.Line("}");

        if (needsSort)
            _ctx.W.Line(sortDesc
                ? $"{varName} = {varName}.sortreverse();"
                : $"{varName} = {varName}.sort();");
    }

    private void EmitCount(string varName, string source, string loopVar,
        List<Stage> preFilters, Stage? select, string? projVar, List<Stage> postFilters,
        LambdaExpressionSyntax? termPred)
    {
        _ctx.W.Line($"declare Integer {varName} = 0;");
        _ctx.W.Line($"foreach ({loopVar} in {source}) {{");
        _ctx.W.Push();
        var (_, preG, postG) = BeginBody(preFilters, select, loopVar, projVar, postFilters, termPred);
        _ctx.W.Line($"{varName} += 1;");
        EndBody(preG, postG);
        _ctx.W.Pop();
        _ctx.W.Line("}");
    }

    private void EmitAny(string varName, string source, string loopVar,
        List<Stage> preFilters, Stage? select, string? projVar, List<Stage> postFilters,
        LambdaExpressionSyntax? termPred)
    {
        _ctx.W.Line($"declare Boolean {varName} = False;");
        _ctx.W.Line($"foreach ({loopVar} in {source}) {{");
        _ctx.W.Push();
        var (_, preG, postG) = BeginBody(preFilters, select, loopVar, projVar, postFilters, termPred);
        _ctx.W.Line($"{varName} = True;");
        _ctx.W.Line("break;");
        EndBody(preG, postG);
        _ctx.W.Pop();
        _ctx.W.Line("}");
    }

    private void EmitAll(string varName, string source, string loopVar,
        List<Stage> preFilters, Stage? select, string? projVar, List<Stage> postFilters,
        LambdaExpressionSyntax? termPred)
    {
        _ctx.W.Line($"declare Boolean {varName} = True;");
        _ctx.W.Line($"foreach ({loopVar} in {source}) {{");
        _ctx.W.Push();
        // All(pred): emit pre-select guards, projection, then negate the combined post+termPred.
        int preG = OpenGuards(preFilters);
        if (select?.Lambda is not null)
        {
            var selBody = _preTranslatedSelectBody ?? LambdaBody(select.Lambda);
            _preTranslatedSelectBody = null;
            if (projVar is not null) _ctx.W.Line($"declare {projVar} = {selBody};");
        }
        var conds = BuildCondList(postFilters, termPred);
        if (conds.Count > 0)
        {
            _ctx.W.Line($"if (!({string.Join(" && ", conds)})) {{");
            _ctx.W.Push();
            _ctx.W.Line($"{varName} = False;");
            _ctx.W.Line("break;");
            _ctx.W.Pop();
            _ctx.W.Line("}");
        }
        CloseGuards(preG);
        _ctx.W.Pop();
        _ctx.W.Line("}");
    }

    private void EmitSum(string varName, ITypeSymbol? varType, string source, string loopVar,
        List<Stage> preFilters, Stage? select, string? projVar, List<Stage> postFilters,
        LambdaExpressionSyntax? selectorLambda)
    {
        var msType     = varType is not null ? TypeMapper.Map(varType) : "Integer";
        var defaultVal = msType == "Real" ? "0." : "0";
        _ctx.W.Line($"declare {msType} {varName} = {defaultVal};");
        _ctx.W.Line($"foreach ({loopVar} in {source}) {{");
        _ctx.W.Push();
        var (item, preG, postG) = BeginBody(preFilters, select, loopVar, projVar, postFilters);
        var sumItem = selectorLambda is not null ? LambdaBody(selectorLambda) : item;
        _ctx.W.Line($"{varName} += {sumItem};");
        EndBody(preG, postG);
        _ctx.W.Pop();
        _ctx.W.Line("}");
    }

    private void EmitFirst(string varName, ITypeSymbol? varType, string source, string loopVar,
        List<Stage> preFilters, Stage? select, string? projVar, List<Stage> postFilters,
        LambdaExpressionSyntax? termPred)
    {
        var msType = varType is not null ? TypeMapper.Map(varType) : "";
        _ctx.W.Line($"declare {msType} {varName};");
        _ctx.W.Line($"foreach ({loopVar} in {source}) {{");
        _ctx.W.Push();
        var (item, preG, postG) = BeginBody(preFilters, select, loopVar, projVar, postFilters, termPred);
        _ctx.W.Line($"{varName} = {item};");
        _ctx.W.Line("break;");
        EndBody(preG, postG);
        _ctx.W.Pop();
        _ctx.W.Line("}");
    }

    private void EmitLast(string varName, ITypeSymbol? varType, string source, string loopVar,
        List<Stage> preFilters, Stage? select, string? projVar, List<Stage> postFilters,
        LambdaExpressionSyntax? termPred)
    {
        var msType = varType is not null ? TypeMapper.Map(varType) : "";
        _ctx.W.Line($"declare {msType} {varName};");
        _ctx.W.Line($"foreach ({loopVar} in {source}) {{");
        _ctx.W.Push();
        var (item, preG, postG) = BeginBody(preFilters, select, loopVar, projVar, postFilters, termPred);
        _ctx.W.Line($"{varName} = {item};"); // keep overwriting — last matching wins
        EndBody(preG, postG);
        _ctx.W.Pop();
        _ctx.W.Line("}");
    }

    private void EmitSingle(string varName, ITypeSymbol? varType, string source, string loopVar,
        List<Stage> preFilters, Stage? select, string? projVar, List<Stage> postFilters,
        LambdaExpressionSyntax? termPred, bool requireMatch)
    {
        var msType = varType is not null ? TypeMapper.Map(varType) : "";
        var foundVar = varName + "Found";
        _ctx.W.Line($"declare {msType} {varName};");
        _ctx.W.Line($"declare Boolean {foundVar} = False;");
        _ctx.W.Line($"foreach ({loopVar} in {source}) {{");
        _ctx.W.Push();
        var (item, preG, postG) = BeginBody(preFilters, select, loopVar, projVar, postFilters, termPred);
        _ctx.W.Line($"assert(!{foundVar});");
        _ctx.W.Line($"{varName} = {item};");
        _ctx.W.Line($"{foundVar} = True;");
        EndBody(preG, postG);
        _ctx.W.Pop();
        _ctx.W.Line("}");
        if (requireMatch)
            _ctx.W.Line($"assert({foundVar});");
    }

    private void EmitToDictionary(string varName, ITypeSymbol? varType, string source, string loopVar,
        List<Stage> preFilters, Stage? select, string? projVar, List<Stage> postFilters,
        LambdaExpressionSyntax? keyLambda, LambdaExpressionSyntax? valueLambda)
    {
        // Infer key/value ManiaScript types from the variable type (Dictionary<K,V>).
        string keyType = "Text", valType = "Text";
        if (varType is INamedTypeSymbol nt && nt.TypeArguments.Length >= 2)
        {
            keyType = TypeMapper.Map(nt.TypeArguments[0]);
            valType = TypeMapper.Map(nt.TypeArguments[1]);
        }

        _ctx.W.Line($"declare {valType}[{keyType}] {varName};");
        _ctx.W.Line($"foreach ({loopVar} in {source}) {{");
        _ctx.W.Push();
        var (item, preG, postG) = BeginBody(preFilters, select, loopVar, projVar, postFilters);

        var keyExpr  = keyLambda is not null ? LambdaBody(keyLambda) : item;
        var valExpr  = valueLambda is not null ? LambdaBody(valueLambda) : item;
        _ctx.W.Line($"assert(!{varName}.existskey({keyExpr}));");
        _ctx.W.Line($"{varName}[{keyExpr}] = {valExpr};");

        EndBody(preG, postG);
        _ctx.W.Pop();
        _ctx.W.Line("}");
    }

    private void EmitContains(string varName, string source, string loopVar,
        List<Stage> preFilters, Stage? select, string? projVar, List<Stage> postFilters,
        ExpressionSyntax valueArg)
    {
        var value = _expr.Translate(valueArg);
        _ctx.W.Line($"declare Boolean {varName} = False;");
        _ctx.W.Line($"foreach ({loopVar} in {source}) {{");
        _ctx.W.Push();
        var (item, preG, postG) = BeginBody(preFilters, select, loopVar, projVar, postFilters);
        _ctx.W.Line($"if ({item} == {value}) {{");
        _ctx.W.Push();
        _ctx.W.Line($"{varName} = True;");
        _ctx.W.Line("break;");
        _ctx.W.Pop();
        _ctx.W.Line("}");
        EndBody(preG, postG);
        _ctx.W.Pop();
        _ctx.W.Line("}");
    }

    private void EmitAggregate(string varName, ITypeSymbol? varType, string source, string loopVar,
        List<Stage> preFilters, Stage? select, string? projVar, List<Stage> postFilters,
        LambdaExpressionSyntax accLambda, ExpressionSyntax seedExpr)
    {
        var msType = varType is not null ? TypeMapper.Map(varType) : "Integer";
        var seed   = _expr.Translate(seedExpr);

        // Aggregate((acc, x) => body): first param = accumulator, second = element.
        var accParam  = FirstParamName(accLambda)  ?? "Acc";
        var elemParam = SecondParamName(accLambda) ?? "X";

        _ctx.DeclareForLocals[accParam]  = varName;                  // acc → result var name
        _ctx.DeclareForLocals[elemParam] = loopVar;                  // element → loop var

        _ctx.W.Line($"declare {msType} {varName} = {seed};");
        _ctx.W.Line($"foreach ({loopVar} in {source}) {{");
        _ctx.W.Push();
        var (_, preG, postG) = BeginBody(preFilters, select, loopVar, projVar, postFilters);
        _ctx.W.Line($"{varName} = {LambdaBody(accLambda)};");
        EndBody(preG, postG);
        _ctx.W.Pop();
        _ctx.W.Line("}");

        _ctx.DeclareForLocals.Remove(accParam);
        _ctx.DeclareForLocals.Remove(elemParam);
    }

    private void EmitAggregateNoSeed(string varName, ITypeSymbol? varType, string source, string loopVar,
        List<Stage> preFilters, Stage? select, string? projVar, List<Stage> postFilters,
        LambdaExpressionSyntax accLambda)
    {
        var msType = varType is not null ? TypeMapper.Map(varType) : "Integer";

        _ctx.W.Line($"assert({source}.count > 0);");
        _ctx.W.Line($"declare {msType} {varName} = {source}[0];");

        var elemParam = FirstParamName(accLambda)  ?? "Acc";
        var accParam  = SecondParamName(accLambda) ?? "X";
        _ctx.DeclareForLocals[elemParam] = varName;

        _ctx.W.Line($"for (AggI, 1, {source}.count - 1) {{");
        _ctx.W.Push();
        _ctx.DeclareForLocals[accParam] = $"{source}[AggI]";
        _ctx.W.Line($"{varName} = {LambdaBody(accLambda)};");
        _ctx.DeclareForLocals.Remove(accParam);
        _ctx.W.Pop();
        _ctx.W.Line("}");

        _ctx.DeclareForLocals.Remove(elemParam);
    }

    private void EmitMinMax(string varName, ITypeSymbol? varType, string source, string loopVar,
        List<Stage> preFilters, Stage? select, string? projVar, List<Stage> postFilters,
        LambdaExpressionSyntax? selectorLambda, bool isMax)
    {
        var msType = varType is not null ? TypeMapper.Map(varType) : "Integer";
        var op = isMax ? ">" : "<";

        _ctx.W.Line($"assert({source}.count > 0);");

        if (selectorLambda is not null)
        {
            // Seed: evaluate selector on first element.
            // Temporarily bind the lambda param to the source[0] expression.
            var paramName = FirstParamName(selectorLambda) ?? "X";
            _ctx.DeclareForLocals[paramName] = $"{source}[0]";
            var seedExpr = LambdaBody(selectorLambda);
            _ctx.DeclareForLocals.Remove(paramName);

            _ctx.W.Line($"declare {msType} {varName} = {seedExpr};");
            _ctx.W.Line($"for (MinMaxI, 1, {source}.count - 1) {{");
            _ctx.W.Push();
            _ctx.DeclareForLocals[paramName] = $"{source}[MinMaxI]";
            var valExpr = LambdaBody(selectorLambda);
            _ctx.DeclareForLocals.Remove(paramName);
            _ctx.W.Line($"declare Val = {valExpr};");
            _ctx.W.Line($"if (Val {op} {varName}) {varName} = Val;");
            _ctx.W.Pop();
            _ctx.W.Line("}");
        }
        else
        {
            _ctx.W.Line($"declare {msType} {varName} = {source}[0];");
            _ctx.W.Line($"for (MinMaxI, 1, {source}.count - 1) {{");
            _ctx.W.Push();
            _ctx.W.Line($"if ({source}[MinMaxI] {op} {varName}) {varName} = {source}[MinMaxI];");
            _ctx.W.Pop();
            _ctx.W.Line("}");
        }
    }

    private void EmitZip(string varName, ITypeSymbol? varType, string source,
        ExpressionSyntax secondSourceExpr, LambdaExpressionSyntax? resultLambda, string loopVar)
    {
        var elemType = varType is INamedTypeSymbol nt2 && nt2.TypeArguments.Length > 0
            ? TypeMapper.Map(nt2.TypeArguments[0])
            : GetElementType(varType, null);
        var second = _expr.Translate(secondSourceExpr);

        // Resolve the two parameter names from the result selector lambda.
        var param1 = FirstParamName(resultLambda) ?? "A";
        var param2 = SecondParamName(resultLambda) ?? "B";
        var ms1 = NameMangler.Local(param1);
        var ms2 = NameMangler.Local(param2);

        _ctx.W.Line($"declare {elemType}[] {varName};");
        _ctx.W.Line($"declare ZipCount = {source}.count;");
        _ctx.W.Line($"if ({second}.count < ZipCount) ZipCount = {second}.count;");
        _ctx.W.Line($"for (ZipI, 0, ZipCount - 1) {{");
        _ctx.W.Push();

        if (resultLambda is not null)
        {
            _ctx.DeclareForLocals[param1] = $"{source}[ZipI]";
            _ctx.DeclareForLocals[param2] = $"{second}[ZipI]";
            var resultExpr = LambdaBody(resultLambda);
            _ctx.DeclareForLocals.Remove(param1);
            _ctx.DeclareForLocals.Remove(param2);
            _ctx.W.Line($"{varName}.add({resultExpr});");
        }
        else
        {
            // No result selector: produce tuples — not directly supported; emit both elements.
            _ctx.W.Line($"{varName}.add({source}[ZipI]);");
        }

        _ctx.W.Pop();
        _ctx.W.Line("}");
    }

    private void EmitSelectMany(string varName, ITypeSymbol? varType, string source,
        IReadOnlyList<Stage> allStages, SelectManyStage smStage, string loopVar,
        LambdaExpressionSyntax? terminalLambda)
    {
        var elemType = GetElementType(varType, null);
        var innerParam = FirstParamName(smStage.Lambda) ?? "Item";
        var innerVar   = NameMangler.Local(innerParam);

        // Pre-SelectMany Where filters.
        var preFilters = allStages
            .TakeWhile(s => s.Kind != StageKind.SelectMany)
            .Where(s => s.Kind == StageKind.Where)
            .ToList();

        _ctx.W.Line($"declare {elemType}[] {varName};");
        _ctx.W.Line($"foreach ({loopVar} in {source}) {{");
        _ctx.W.Push();
        int preG = OpenGuards(preFilters);

        // Bind outer loop var for collection selector lambda.
        var outerParam = FirstParamName(smStage.Lambda)!;
        _ctx.DeclareForLocals[outerParam] = loopVar;

        var collectionExpr = LambdaBody(smStage.Lambda!);

        _ctx.DeclareForLocals.Remove(outerParam);

        _ctx.W.Line($"foreach ({innerVar} in {collectionExpr}) {{");
        _ctx.W.Push();

        string resultItem;
        if (smStage.ResultLambda is not null)
        {
            var rp1 = FirstParamName(smStage.ResultLambda) ?? outerParam;
            var rp2 = SecondParamName(smStage.ResultLambda) ?? innerParam;
            _ctx.DeclareForLocals[rp1] = loopVar;
            _ctx.DeclareForLocals[rp2] = innerVar;
            resultItem = LambdaBody(smStage.ResultLambda);
            _ctx.DeclareForLocals.Remove(rp1);
            _ctx.DeclareForLocals.Remove(rp2);
        }
        else
        {
            resultItem = innerVar;
        }

        _ctx.W.Line($"{varName}.add({resultItem});");
        _ctx.W.Pop();
        _ctx.W.Line("}");

        CloseGuards(preG);
        _ctx.W.Pop();
        _ctx.W.Line("}");
    }

    // ── Guard helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Opens the loop body structure: pre-select guards, optional projection declaration,
    /// post-select guards + optional terminal predicate guard.
    /// Returns the effective item string (projVar if projection exists, otherwise loopVar or
    /// the inline select body) and the guard counts to pass to <see cref="EndBody"/>.
    /// </summary>
    private (string item, int preGuards, int postGuards) BeginBody(
        List<Stage> preFilters, Stage? select, string loopVar, string? projVar,
        List<Stage> postFilters, LambdaExpressionSyntax? termPred = null)
    {
        int preG = OpenGuards(preFilters);

        string item = loopVar;
        if (select?.Lambda is not null)
        {
            // Use pre-translated body when available (avoids param-name collision where
            // Select and post-Where both use the same identifier, e.g. x => x*2 / x => x>5).
            var selBody = _preTranslatedSelectBody ?? LambdaBody(select.Lambda);
            _preTranslatedSelectBody = null; // consume
            if (projVar is not null)
            {
                _ctx.W.Line($"declare {projVar} = {selBody};");
                item = projVar;
            }
            else
            {
                item = selBody; // no post-guards — inline the expression
            }
        }

        int postG = OpenGuards(postFilters, termPred);
        return (item, preG, postG);
    }

    private void EndBody(int preGuards, int postGuards)
    {
        CloseGuards(postGuards);
        CloseGuards(preGuards);
    }

    /// <summary>
    /// Emits a combined <c>if (cond1 &amp;&amp; cond2 …)</c> guard for all Where stages
    /// plus an optional extra predicate.  Returns 1 if a guard was opened, 0 if not.
    /// </summary>
    private int OpenGuards(IReadOnlyList<Stage> filters, LambdaExpressionSyntax? extra = null)
    {
        var conds = BuildCondList(filters, extra);
        if (conds.Count == 0) return 0;
        _ctx.W.Line($"if ({string.Join(" && ", conds)}) {{");
        _ctx.W.Push();
        return 1;
    }

    private void CloseGuards(int count)
    {
        for (var i = 0; i < count; i++) { _ctx.W.Pop(); _ctx.W.Line("}"); }
    }

    private List<string> BuildCondList(IReadOnlyList<Stage> filters,
        LambdaExpressionSyntax? extra = null)
    {
        var list = new List<string>();
        foreach (var s in filters.Where(s => s.Lambda is not null))
            list.Add(LambdaBody(s.Lambda!));
        if (extra is not null) list.Add(LambdaBody(extra));
        return list;
    }

    // ── Lambda helpers ──────────────────────────────────────────────────────

    private string LambdaBody(LambdaExpressionSyntax lambda)
    {
        SyntaxNode body = lambda switch
        {
            SimpleLambdaExpressionSyntax sl => sl.Body,
            ParenthesizedLambdaExpressionSyntax pl => pl.Body,
            _ => lambda,
        };
        if (body is ExpressionSyntax expr) return _expr.Translate(expr);
        _ctx.Report(Diagnostics.UnsupportedLinq, lambda.GetLocation(), "block lambda body");
        return "/* block lambda */";
    }

    private string ResolveLoopVar(LinqChain chain)
    {
        // For Aggregate, the terminal lambda's first param is the accumulator, not an element.
        // Use the second param (the element param) as the loop variable instead.
        if (chain.Terminal is TerminalKind.Aggregate or TerminalKind.AggregateNoSeed
            && chain.TerminalLambda is not null)
        {
            var ep = SecondParamName(chain.TerminalLambda);
            if (ep is not null) return NameMangler.Local(ep);
        }

        // Use the first lambda's parameter name (PascalCased) as the loop variable.
        foreach (var s in chain.Stages)
        {
            var p = FirstParamName(s.Lambda);
            if (p is not null) return NameMangler.Local(p);
        }
        var tp = FirstParamName(chain.TerminalLambda);
        return tp is not null ? NameMangler.Local(tp) : "Item";
    }

    private static string? FirstParamName(LambdaExpressionSyntax? lambda) => lambda switch
    {
        SimpleLambdaExpressionSyntax sl => sl.Parameter.Identifier.Text,
        ParenthesizedLambdaExpressionSyntax pl when pl.ParameterList.Parameters.Count > 0
            => pl.ParameterList.Parameters[0].Identifier.Text,
        _ => null,
    };

    private static string? SecondParamName(LambdaExpressionSyntax? lambda) => lambda switch
    {
        ParenthesizedLambdaExpressionSyntax pl when pl.ParameterList.Parameters.Count >= 2
            => pl.ParameterList.Parameters[1].Identifier.Text,
        _ => null,
    };

    // ── Type helpers ────────────────────────────────────────────────────────

    private string GetElementType(ITypeSymbol? varType, Stage? selectStage)
    {
        // If there is a Select, try to get the projected type from the lambda body's inferred type.
        if (selectStage?.Lambda is not null)
        {
            ExpressionSyntax? bodyExpr = selectStage.Lambda switch
            {
                SimpleLambdaExpressionSyntax sl => sl.Body as ExpressionSyntax,
                ParenthesizedLambdaExpressionSyntax pl => pl.Body as ExpressionSyntax,
                _ => null,
            };
            if (bodyExpr is not null)
            {
                var t = _ctx.Model.GetTypeInfo(bodyExpr).Type;
                if (t is not null) return TypeMapper.Map(t);
            }
        }

        // Fall back to unwrapping the collection element type from varType.
        if (varType is INamedTypeSymbol nt && nt.TypeArguments.Length > 0)
            return TypeMapper.Map(nt.TypeArguments[0]);
        if (varType is IArrayTypeSymbol arr)
            return TypeMapper.Map(arr.ElementType);
        return TypeMapper.Map(varType);
    }

    // ── Lambda param binding ────────────────────────────────────────────────

    /// <summary>
    /// Registers lambda parameter names in <see cref="EmitContext.DeclareForLocals"/>:
    /// <list type="bullet">
    /// <item>Pre-select stage params and the Select param → <paramref name="loopVar"/></item>
    /// <item>Post-select Where params and the terminal lambda param →
    ///   <paramref name="projVar"/> (when set) or <paramref name="loopVar"/></item>
    /// </list>
    /// </summary>
    private BindingScope BindParamsSplit(
        string loopVar, List<Stage> preFilters, Stage? select,
        List<Stage> postFilters, string? projVar, LambdaExpressionSyntax? termLambda)
    {
        var bindings = new Dictionary<string, string>();

        void Bind(LambdaExpressionSyntax? lam, string target)
        {
            var p = FirstParamName(lam);
            if (p is not null) bindings[p] = target;
        }

        foreach (var s in preFilters) Bind(s.Lambda, loopVar);
        Bind(select?.Lambda, loopVar);
        foreach (var kvp in bindings) _ctx.DeclareForLocals[kvp.Key] = kvp.Value;

        // Pre-translate the select body while pre-select bindings are active.
        // This is needed when the Select param name collides with a post-Where param name
        // (e.g., both use 'x'): the select body must see x→loopVar, not x→projVar.
        if (select?.Lambda is not null && projVar is not null)
            _preTranslatedSelectBody = LambdaBody(select.Lambda);

        // Now add/overwrite post-select bindings.
        var postBindings = new Dictionary<string, string>();
        var postTarget = projVar ?? loopVar;
        foreach (var s in postFilters) { var p = FirstParamName(s.Lambda); if (p is not null) postBindings[p] = postTarget; }
        { var p = FirstParamName(termLambda); if (p is not null) postBindings[p] = postTarget; }
        foreach (var kvp in postBindings) _ctx.DeclareForLocals[kvp.Key] = kvp.Value;

        var allKeys = new System.Collections.Generic.HashSet<string>(bindings.Keys);
        foreach (var k in postBindings.Keys) allKeys.Add(k);
        return new BindingScope(_ctx, allKeys);
    }

    private static bool IsLinq(IMethodSymbol m)
        => (m.ReducedFrom ?? m).ContainingType?.ToDisplayString() == "System.Linq.Enumerable";

    // ── Cleanup scope ───────────────────────────────────────────────────────

    private readonly struct BindingScope : System.IDisposable
    {
        private readonly EmitContext _ctx;
        private readonly System.Collections.Generic.HashSet<string> _names;
        public BindingScope(EmitContext ctx, System.Collections.Generic.HashSet<string> names)
        { _ctx = ctx; _names = names; }
        public void Dispose() { foreach (var n in _names) _ctx.DeclareForLocals.Remove(n); }
    }
}
