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
    /// Pre-pass: register every valid virtual / override method as a label so call sites are
    /// rewritten to <c>+++Name+++</c> by ExpressionEmitter. Labels are insertion points rather
    /// than functions, so they cannot accept parameters or return a value.
    /// </summary>
    public void CollectLabels()
    {
        foreach (var m in _ctx.Info.Symbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (m.MethodKind != MethodKind.Ordinary) continue;
            if (!IsLabelCandidate(m)) continue;
            if (m.Name is "Settings" or "UpdateSettings" || m.IsIContextEntryPoint()) continue;
            if (!HasValidLabelSignature(m))
            {
                _ctx.Report(Diagnostics.InvalidLabelSignature, m.Locations.FirstOrDefault(), m.Name);
                continue;
            }

            // Register overridden declarations as well so `base.Label()` is recognised as
            // an already-assembled label contribution.
            for (var label = m; label is not null; label = label.OverriddenMethod)
                _ctx.LabelMethods.Add(label);
        }
    }

    public void Emit()
    {
        // Labels first (per common ManiaScript convention they live at the top of the script).
        foreach (var m in _ctx.Info.Symbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (m.MethodKind != MethodKind.Ordinary) continue;
            if (!IsLabelCandidate(m) || !HasValidLabelSignature(m)) continue;
            if (m.Name is "Settings" or "UpdateSettings" || m.IsIContextEntryPoint()) continue;
            if (!m.IsAbstract) EmitLabel(m);
        }

        // Plain functions and property accessors must be textually defined before any sibling
        // that calls them (ManiaScript has no forward declarations and disallows circular calls
        // between distinct functions — see docs/ManiaScriptReference.md "Functions").
        // Sort by call dependency (callees before callers) instead of raw C# declaration order.
        var methods = _ctx.Info.Symbol.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && !m.IsVirtual && !m.IsOverride
                        && !m.IsIContextEntryPoint())
            .Cast<ISymbol>();
        var properties = _ctx.Info.Symbol.GetMembers().OfType<IPropertySymbol>()
            .Where(p => !p.HasAttr("ManialinkControlAttribute") && IsUserDefinedProperty(p))
            .Cast<ISymbol>();

        foreach (var n in SortByCallDependency(methods.Concat(properties).ToList()))
        {
            if (n is IMethodSymbol m) EmitFunction(m);
            else if (n is IPropertySymbol p) EmitPropertyAccessors(p);
        }
    }

    /// <summary>
    /// Orders <paramref name="nodes"/> (own-class plain functions and property accessors) so
    /// that every callee is emitted before its caller. Dependencies are found by scanning each
    /// member's body for identifiers that resolve to another member of the same set (covers
    /// invocation targets, property reads, and property-setter writes alike). Falls back to the
    /// original relative order when there is no dependency constraint, and — since a genuine
    /// cycle between 2+ distinct functions is invalid ManiaScript — reports
    /// <see cref="Diagnostics.CircularFunctionCalls"/> once and breaks the cycle arbitrarily
    /// rather than looping forever. Self-recursion (a function calling itself) is fine and
    /// ignored here, since it imposes no ordering constraint.
    /// </summary>
    private List<ISymbol> SortByCallDependency(List<ISymbol> nodes)
    {
        var nodeSet = new HashSet<ISymbol>(nodes, SymbolEqualityComparer.Default);
        var deps = new Dictionary<ISymbol, HashSet<ISymbol>>(SymbolEqualityComparer.Default);

        foreach (var n in nodes)
        {
            var callees = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            foreach (var root in GetDependencyScanRoots(n))
            {
                foreach (var name in root.DescendantNodesAndSelf().OfType<SimpleNameSyntax>())
                {
                    var sym = _ctx.Model.GetSymbolInfo(name).Symbol;
                    if (sym is IMethodSymbol ms) sym = ms.OriginalDefinition;
                    if (sym is not null && !SymbolEqualityComparer.Default.Equals(sym, n) && nodeSet.Contains(sym))
                        callees.Add(sym);
                }
            }
            deps[n] = callees;
        }

        var result = new List<ISymbol>(nodes.Count);
        var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var visiting = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var reportedCycle = false;

        void Visit(ISymbol n)
        {
            if (visited.Contains(n)) return;
            if (visiting.Contains(n))
            {
                if (!reportedCycle)
                {
                    reportedCycle = true;
                    _ctx.Report(Diagnostics.CircularFunctionCalls, n.Locations.FirstOrDefault(), n.Name);
                }
                return;
            }
            visiting.Add(n);
            foreach (var dep in deps[n]) Visit(dep);
            visiting.Remove(n);
            visited.Add(n);
            result.Add(n);
        }

        foreach (var n in nodes) Visit(n);
        return result;
    }

    /// <summary>Root syntax nodes (bodies/expression-bodies) to scan when building the call-dependency graph.</summary>
    private static IEnumerable<SyntaxNode> GetDependencyScanRoots(ISymbol symbol)
    {
        switch (symbol)
        {
            case IMethodSymbol m when m.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is MethodDeclarationSyntax md:
                if (md.Body is not null) yield return md.Body;
                else if (md.ExpressionBody is not null) yield return md.ExpressionBody.Expression;
                break;

            case IPropertySymbol p when p.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is PropertyDeclarationSyntax pd:
                if (pd.ExpressionBody is not null) yield return pd.ExpressionBody.Expression;
                if (pd.AccessorList is not null)
                {
                    foreach (var acc in pd.AccessorList.Accessors)
                    {
                        if (acc.Body is not null) yield return acc.Body;
                        else if (acc.ExpressionBody is not null) yield return acc.ExpressionBody.Expression;
                    }
                }
                break;
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

        var msType = _ctx.MapType(p.Type);
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
                if (!ReportInvalidExpressionBody(eb.Expression, returnsVoid: false))
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
                var mutated = FindMutatedParameters(setter);
                var valueParameter = p.SetMethod.Parameters[0];
                _ctx.W.Line($"Void {setName}({msType} {ParameterName(valueParameter, mutated)}) {{");
                _ctx.W.Push();
                EmitMutableParameterCopies(p.SetMethod.Parameters, mutated);
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
    /// Every property backing variable is a global-style <c>G_Name</c> identifier.
    /// </summary>
    private static string AutoPropBacking(IPropertySymbol p) => NameMangler.Global(p);

    private void EmitAccessorBody(AccessorDeclarationSyntax accessor, bool returnsVoid)
    {
        if (accessor.Body is { } body)
        {
            foreach (var s in body.Statements) _stmt.Emit(s);
        }
        else if (accessor.ExpressionBody is { } eb)
        {
            if (!ReportInvalidExpressionBody(eb.Expression, returnsVoid))
            {
                if (returnsVoid) _ctx.W.Line(_expr.Translate(eb.Expression) + ";");
                else _ctx.W.Line($"return {_expr.Translate(eb.Expression)};");
            }
        }
    }

    private void EmitFunction(IMethodSymbol m)
    {
        var mutated = FindMutatedParameters(m.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax());
        var ret = _ctx.MapType(m.ReturnType);
        var name = NameMangler.Method(m);
        var ps = string.Join(", ", m.Parameters.Select(p => $"{_ctx.MapType(p.Type)} {ParameterName(p, mutated)}"));
        _ctx.W.Line($"{ret} {name}({ps}) {{");
        _ctx.W.Push();
        EmitMutableParameterCopies(m.Parameters, mutated);
        EmitBody(m);
        _ctx.W.Pop();
        _ctx.W.Line("}");
        _ctx.W.Line();
    }

    // Keep body references at their usual _Name spelling. Only a written parameter gets a
    // different signature name, so the body can declare a mutable _Name copy on entry.
    private static string ParameterName(IParameterSymbol parameter, HashSet<IParameterSymbol> mutated)
        => mutated.Contains(parameter) ? "__Input" + NameMangler.Parameter(parameter) : NameMangler.Parameter(parameter);

    private void EmitMutableParameterCopies(IEnumerable<IParameterSymbol> parameters, HashSet<IParameterSymbol> mutated)
    {
        foreach (var parameter in parameters)
        {
            if (!mutated.Contains(parameter)) continue;
            _ctx.W.Line($"declare {_ctx.MapType(parameter.Type)} {NameMangler.Parameter(parameter)} = {ParameterName(parameter, mutated)};");
        }
    }

    private HashSet<IParameterSymbol> FindMutatedParameters(SyntaxNode? syntax)
    {
        var mutated = new HashSet<IParameterSymbol>(SymbolEqualityComparer.Default);
        if (syntax is null) return mutated;

        void Mark(ExpressionSyntax expression, bool collectionMutation = false)
        {
            if (expression is TupleExpressionSyntax tuple)
            {
                foreach (var argument in tuple.Arguments) Mark(argument.Expression);
                return;
            }

            var indirect = false;
            while (true)
            {
                switch (expression)
                {
                    case IdentifierNameSyntax identifier:
                        if (_ctx.Model.GetSymbolInfo(identifier).Symbol is IParameterSymbol parameter)
                        {
                            var collection = _ctx.MapType(parameter.Type).EndsWith("]", StringComparison.Ordinal);
                            // A write through a class reference changes the referenced object,
                            // not the parameter binding. Collections and structs need a local copy.
                            if (collectionMutation ? !indirect && collection
                                : !indirect || collection || parameter.Type.TypeKind == TypeKind.Struct)
                                mutated.Add(parameter);
                        }
                        return;
                    case ParenthesizedExpressionSyntax parenthesized:
                        expression = parenthesized.Expression;
                        break;
                    case MemberAccessExpressionSyntax member:
                        indirect = true;
                        expression = member.Expression;
                        break;
                    case ElementAccessExpressionSyntax element:
                        indirect = true;
                        expression = element.Expression;
                        break;
                    case PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                        expression = postfix.Operand;
                        break;
                    default:
                        return;
                }
            }
        }

        foreach (var node in syntax.DescendantNodesAndSelf())
        {
            switch (node)
            {
                case AssignmentExpressionSyntax assignment:
                    Mark(assignment.Left);
                    break;
                case PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.PreIncrementExpression)
                    || prefix.IsKind(SyntaxKind.PreDecrementExpression):
                    Mark(prefix.Operand);
                    break;
                case PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.PostIncrementExpression)
                    || postfix.IsKind(SyntaxKind.PostDecrementExpression):
                    Mark(postfix.Operand);
                    break;
                case ArgumentSyntax argument when argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                    || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword):
                    Mark(argument.Expression);
                    break;
                case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member }
                    when member.Name.Identifier.ValueText is "Add" or "AddRange" or "Insert" or "Remove"
                        or "RemoveAt" or "Clear" or "Sort" or "Reverse":
                    Mark(member.Expression, collectionMutation: true);
                    break;
            }
        }
        return mutated;
    }

    private void EmitLabel(IMethodSymbol m)
    {
        _ctx.W.Line($"***{m.Name}***");
        _ctx.W.Line("***");
        EmitBody(m);
        _ctx.W.Line("***");
        _ctx.W.Line();
    }

    private static bool IsLabelCandidate(IMethodSymbol method)
        => method.IsVirtual || method.IsOverride;

    private static bool HasValidLabelSignature(IMethodSymbol method)
        => method.ReturnsVoid && method.Parameters.Length == 0 && method.Arity == 0;

    private void EmitBody(IMethodSymbol m)
    {
        var syntaxRef = m.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef?.GetSyntax() is not MethodDeclarationSyntax decl) return;
        if (decl.Body is { } body)
        {
            ValidateBaseLabelCalls(body);
            foreach (var s in body.Statements) _stmt.Emit(s);
        }
        else if (decl.ExpressionBody is { } eb)
        {
            if (!ReportInvalidExpressionBody(eb.Expression, m.ReturnsVoid))
            {
                if (m.ReturnsVoid) _ctx.W.Line(_expr.Translate(eb.Expression) + ";");
                else _ctx.W.Line($"return {_expr.Translate(eb.Expression)};");
            }
        }
    }

    private bool ReportInvalidExpressionBody(ExpressionSyntax expression, bool returnsVoid)
    {
        var assignment = expression.DescendantNodesAndSelf()
            .OfType<AssignmentExpressionSyntax>()
            .FirstOrDefault(candidate => !AssignmentSyntax.IsInitializerEntry(candidate));
        if (assignment is null) return false;

        // A void expression body may be a single standalone assignment (e.g. `set => x = value`).
        if (returnsVoid && assignment == expression) return false;

        _ctx.Report(
            returnsVoid ? Diagnostics.NestedAssignment : Diagnostics.AssignmentInReturn,
            assignment.GetLocation());
        return true;
    }

    /// <summary>
    /// A base-label call has no emitted call form: the parent label contribution is already
    /// present at the insertion point. Permit the familiar C# idiom only as the first direct
    /// statement of an override; all other placements would imply a call ordering ManiaScript
    /// cannot express.
    /// </summary>
    private void ValidateBaseLabelCalls(BlockSyntax body)
    {
        var permitted = body.Statements.FirstOrDefault() is ExpressionStatementSyntax first
            ? GetBaseLabelInvocation(first.Expression)
            : null;

        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation == permitted || !IsBaseLabelInvocation(invocation)) continue;
            _ctx.Report(Diagnostics.BaseLabelCallMustBeFirst, invocation.GetLocation(), invocation.Expression);
        }
    }

    private InvocationExpressionSyntax? GetBaseLabelInvocation(ExpressionSyntax expression)
        => expression is InvocationExpressionSyntax invocation && IsBaseLabelInvocation(invocation)
            ? invocation
            : null;

    private bool IsBaseLabelInvocation(InvocationExpressionSyntax invocation)
        => invocation.Expression is MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax }
           && _ctx.IsLabelMethod(_ctx.Model.GetSymbolInfo(invocation).Symbol as IMethodSymbol);
}
