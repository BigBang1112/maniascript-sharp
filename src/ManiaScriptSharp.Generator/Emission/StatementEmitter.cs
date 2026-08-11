using ManiaScriptSharp.Generator.Naming;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>Translates C# statements into ManiaScript.</summary>
internal sealed class StatementEmitter
{
    private readonly EmitContext _ctx;
    private readonly ExpressionEmitter _expr;
    private PatternEmitter? _patterns;
    private LinqChainEmitter? _linq;
    private LinqChainEmitter Linq => _linq ??= new LinqChainEmitter(_ctx, _expr);

    public StatementEmitter(EmitContext ctx, ExpressionEmitter expr) { _ctx = ctx; _expr = expr; }
    public void Bind(PatternEmitter p) => _patterns = p;

    /// <summary>Emit a statement; an outer block is inlined (no extra braces).</summary>
    public void EmitInline(StatementSyntax stmt)
    {
        if (stmt is BlockSyntax b) { foreach (var s in b.Statements) Emit(s); return; }
        Emit(stmt);
    }

    public void Emit(StatementSyntax stmt)
    {
        switch (stmt)
        {
            case BlockSyntax block:
                _ctx.W.Line("{");
                _ctx.W.Push();
                foreach (var s in block.Statements) Emit(s);
                _ctx.W.Pop();
                _ctx.W.Line("}");
                break;

            case LocalDeclarationStatementSyntax local:
                EmitLocalDecl(local);
                break;

            case ExpressionStatementSyntax es:
                if (es.Expression is InvocationExpressionSyntax inv && TryEmitDeclareFor(inv))
                    break;
                if (es.Expression is InvocationExpressionSyntax onChangeInv && TryEmitOnChange(onChangeInv))
                    break;
                // Skip event subscription statements — consumed by EventCollector → event loop.
                if (IsEventSubscription(es.Expression))
                    break;
                // ManiaScript has no `??=`: rewrite `left ??= right;` as `if (left == Null) { left = right; }`.
                if (es.Expression is AssignmentExpressionSyntax coalesce
                    && coalesce.OperatorToken.IsKind(SyntaxKind.QuestionQuestionEqualsToken))
                {
                    var lhs = _expr.Translate(coalesce.Left);
                    _ctx.W.Line($"if ({lhs} == Null) {{");
                    _ctx.W.Push();
                    EmitTernaryAsIfElse(coalesce.Right, v => _ctx.W.Line($"{_expr.TranslateAssignTo(coalesce.Left, _expr.Translate(v))};"));
                    _ctx.W.Pop();
                    _ctx.W.Line("}");
                    break;
                }
                // ManiaScript has no inline conditional or switch expression: rewrite
                // `left = cond ? a : b;` / `left = x switch { ... };` as if/else.
                if (es.Expression is AssignmentExpressionSyntax plainAsg
                    && plainAsg.OperatorToken.IsKind(SyntaxKind.EqualsToken)
                    && plainAsg.Right is ConditionalExpressionSyntax or SwitchExpressionSyntax)
                {
                    EmitTernaryAsIfElse(plainAsg.Right, v => _ctx.W.Line($"{_expr.TranslateAssignTo(plainAsg.Left, _expr.Translate(v))};"));
                    break;
                }
                var text = _expr.Translate(es.Expression);
                // Label calls already form a complete statement (+++Name+++) — no trailing ';'.
                if (text.StartsWith("+++") && text.EndsWith("+++"))
                    _ctx.W.Line(text);
                else
                    _ctx.W.Line(text + ";");
                break;

            case ReturnStatementSyntax rs:
                if (_ctx.ReturnIsContinue && rs.Expression is null)
                    _ctx.W.Line("continue;");
                else if (rs.Expression is ConditionalExpressionSyntax or SwitchExpressionSyntax)
                    EmitTernaryAsIfElse(rs.Expression, v => _ctx.W.Line($"return {_expr.Translate(v)};"));
                else
                    _ctx.W.Line(rs.Expression is null ? "return;" : $"return {_expr.Translate(rs.Expression)};");
                break;

            case IfStatementSyntax ifs:
                EmitIf(ifs);
                break;

            case WhileStatementSyntax ws:
                _ctx.W.Line($"while ({_expr.Translate(ws.Condition)}) {{");
                _ctx.W.Push(); EmitInline(ws.Statement); _ctx.W.Pop();
                _ctx.W.Line("}");
                break;

            case ForEachStatementSyntax fes:
                EmitForeach(fes);
                break;

            case ForEachVariableStatementSyntax fev:
                EmitForeachVariable(fev);
                break;

            case ForStatementSyntax fs:
                EmitFor(fs);
                break;

            case SwitchStatementSyntax sws:
                EmitSwitch(sws);
                break;

            case BreakStatementSyntax: _ctx.W.Line("break;"); break;
            case ContinueStatementSyntax: _ctx.W.Line("continue;"); break;
            case EmptyStatementSyntax: break;

            case ThrowStatementSyntax ts:
                EmitThrow(ts);
                break;

            default:
                _ctx.Report(Diagnostics.Unsupported, stmt.GetLocation(), stmt.Kind().ToString());
                _ctx.W.Line($"// unsupported: {stmt.Kind()}");
                break;
        }
    }

    private void EmitLocalDecl(LocalDeclarationStatementSyntax local)
    {
        var typeSym = _ctx.Model.GetTypeInfo(local.Declaration.Type).Type;
        var msType = TypeMapper.Map(typeSym);
        foreach (var v in local.Declaration.Variables)
        {
            var name = NameMangler.Local(v.Identifier.Text);

            // Strip empty `new MyStruct()` so `declare MyStruct X;` is produced.
            // But first, disallow instantiation of CNod-derived API classes.
            if (v.Initializer is { Value: ObjectCreationExpressionSyntax oc }
                && (oc.ArgumentList is null || oc.ArgumentList.Arguments.Count == 0)
                && oc.Initializer is null)
            {
                if (typeSym is INamedTypeSymbol namedOc && namedOc.IsCNodeDerived())
                {
                    _ctx.Report(Diagnostics.CNodeInstantiation, oc.GetLocation(), typeSym.Name);
                    continue;
                }
                _ctx.W.Line($"declare {msType} {name};");
                continue;
            }

            // LINQ chain desugaring: Where/Select/Count/Any/All/Sum/First/Last/Distinct.
            // Must come before the normal translate so the foreach loop is emitted instead.
            if (v.Initializer?.Value is { } linqInit)
            {
                // No terminal (e.g. .Where(pred) alone): register as a pending lazy chain —
                // code is emitted later when this variable is used as the source of a terminal call.
                if (Linq.TryRegister(linqInit, v.Identifier.Text)) continue;
                // Has a terminal: emit the foreach/aggregate loop now.
                if (Linq.TryEmit(linqInit, name, typeSym)) continue;
            }

            // ManiaScript has no inline conditional or switch expression: split
            // `declare X = cond ? a : b;` / `declare X = x switch { ... };` into a
            // bare declaration followed by an if/else assigning X in each branch.
            if (v.Initializer?.Value is ConditionalExpressionSyntax or SwitchExpressionSyntax)
            {
                // Unlike the plain-init case below, there's no value here yet to infer a type
                // from (it's assigned per-branch afterwards), so the type can never be elided.
                _ctx.W.Line($"declare {msType} {name};");
                EmitTernaryAsIfElse(v.Initializer.Value, v2 => _ctx.W.Line($"{name} = {_expr.Translate(v2)};"));
                continue;
            }

            var init = v.Initializer is null ? "" : $" = {_expr.Translate(v.Initializer.Value)}";

            // Type can be elided for var-style declarations (ManiaScript allows `declare X = ...;`).
            if (local.Declaration.Type.IsVar)
                _ctx.W.Line($"declare {name}{init};");
            else
                _ctx.W.Line($"declare {msType} {name}{init};");
        }
    }

    /// <summary>
    /// Recursively lowers a (possibly nested) C# ternary or switch expression into ManiaScript
    /// if/else blocks, since ManiaScript has neither an inline conditional operator nor a switch
    /// expression. <paramref name="emitLeaf"/> emits the final statement (assignment, return, …)
    /// for each non-branching leaf value.
    /// </summary>
    private void EmitTernaryAsIfElse(ExpressionSyntax valueExpr, System.Action<ExpressionSyntax> emitLeaf)
    {
        if (valueExpr is ConditionalExpressionSyntax cond)
        {
            _ctx.W.Line($"if ({_expr.Translate(cond.Condition)}) {{");
            _ctx.W.Push();
            EmitTernaryAsIfElse(cond.WhenTrue, emitLeaf);
            _ctx.W.Pop();
            _ctx.W.Line("} else {");
            _ctx.W.Push();
            EmitTernaryAsIfElse(cond.WhenFalse, emitLeaf);
            _ctx.W.Pop();
            _ctx.W.Line("}");
        }
        else if (valueExpr is SwitchExpressionSyntax swe)
        {
            EmitSwitchExpressionAsIfElse(swe, emitLeaf);
        }
        else
        {
            emitLeaf(valueExpr);
        }
    }

    /// <summary>
    /// Lowers a C# switch expression (<c>subject switch { pat1 => e1, pat2 => e2, _ => e3 }</c>)
    /// into a real ManiaScript <c>switch</c> statement when every arm is a plain constant (or a
    /// single trailing catch-all), falling back to nested if/else blocks otherwise (relational/
    /// type/`when`-guarded arms have no case-label equivalent). The governing expression is
    /// translated once and reused for every arm's check — if it has side effects, prefer
    /// assigning it to a local variable first.
    /// </summary>
    private void EmitSwitchExpressionAsIfElse(SwitchExpressionSyntax swe, System.Action<ExpressionSyntax> emitLeaf)
    {
        var subject = _expr.Translate(swe.GoverningExpression);
        if (TryEmitSwitchExpressionAsSwitch(subject, swe.Arms, emitLeaf)) return;
        EmitSwitchExpressionArm(subject, swe.Arms, 0, emitLeaf);
    }

    /// <summary>
    /// Emits <paramref name="arms"/> as a real <c>switch</c> statement when viable: every arm's
    /// pattern is a <see cref="ConstantPatternSyntax"/> with no <c>when</c> clause, except for at
    /// most one trailing catch-all (<c>_</c> / <c>var x</c>) arm that becomes <c>default:</c>.
    /// Returns <see langword="false"/> without emitting anything when the shape doesn't qualify.
    /// </summary>
    private bool TryEmitSwitchExpressionAsSwitch(string subject, SeparatedSyntaxList<SwitchExpressionArmSyntax> arms, System.Action<ExpressionSyntax> emitLeaf)
    {
        for (var i = 0; i < arms.Count; i++)
        {
            var arm = arms[i];
            if (arm.WhenClause is not null) return false;
            var isCatchAll = arm.Pattern is DiscardPatternSyntax or VarPatternSyntax;
            if (isCatchAll) { if (i != arms.Count - 1) return false; }
            else if (arm.Pattern is not ConstantPatternSyntax) return false;
        }

        _ctx.W.Line($"switch ({subject}) {{");
        _ctx.W.Push();
        foreach (var arm in arms)
        {
            var isCatchAll = arm.Pattern is DiscardPatternSyntax or VarPatternSyntax;
            _ctx.W.Line(isCatchAll
                ? "default: {"
                : $"case {_expr.Translate(((ConstantPatternSyntax)arm.Pattern).Expression)}: {{");
            _ctx.W.Push();

            var (boundName, boundTarget) = BindingFor(subject, arm.Pattern);
            if (boundName is not null) _ctx.DeclareForLocals[boundName] = boundTarget!;
            EmitTernaryAsIfElse(arm.Expression, emitLeaf);
            if (boundName is not null) _ctx.DeclareForLocals.Remove(boundName);

            _ctx.W.Pop();
            _ctx.W.Line("}");
        }
        _ctx.W.Pop();
        _ctx.W.Line("}");
        return true;
    }

    private void EmitSwitchExpressionArm(string subject, SeparatedSyntaxList<SwitchExpressionArmSyntax> arms, int index, System.Action<ExpressionSyntax> emitLeaf)
    {
        var arm = arms[index];
        var isLast = index == arms.Count - 1;
        // `_ => …` / `var x => …` always matches — treated as an unconditional catch-all.
        var isCatchAll = arm.Pattern is DiscardPatternSyntax or VarPatternSyntax;

        // Pattern-bound variable (declaration/var pattern) is aliased — not physically declared —
        // to the subject expression, the same way lambda params are bound in LinqChainEmitter.
        // This lets the `when` clause reference it before any ManiaScript statement could declare it.
        var (boundName, boundTarget) = BindingFor(subject, arm.Pattern);
        if (boundName is not null) _ctx.DeclareForLocals[boundName] = boundTarget!;

        // A trailing catch-all with no `when` clause needs no if/else wrapper at all.
        if (isCatchAll && arm.WhenClause is null && isLast)
        {
            EmitTernaryAsIfElse(arm.Expression, emitLeaf);
            if (boundName is not null) _ctx.DeclareForLocals.Remove(boundName);
            return;
        }

        var cond = isCatchAll ? "True" : _patterns!.Translate(subject, arm.Pattern);
        if (arm.WhenClause is not null)
        {
            var when = _expr.Translate(arm.WhenClause.Condition);
            cond = isCatchAll ? when : $"({cond}) && ({when})";
        }

        _ctx.W.Line($"if ({cond}) {{");
        _ctx.W.Push();
        EmitTernaryAsIfElse(arm.Expression, emitLeaf);
        _ctx.W.Pop();
        if (boundName is not null) _ctx.DeclareForLocals.Remove(boundName);

        if (isLast)
        {
            _ctx.W.Line("}");
        }
        else
        {
            _ctx.W.Line("} else {");
            _ctx.W.Push();
            EmitSwitchExpressionArm(subject, arms, index + 1, emitLeaf);
            _ctx.W.Pop();
            _ctx.W.Line("}");
        }
    }

    /// <summary>Resolves the name/alias-target pair for a switch-expression arm's pattern binding, if any.</summary>
    private static (string? name, string? target) BindingFor(string subject, PatternSyntax pattern) => pattern switch
    {
        DeclarationPatternSyntax { Designation: SingleVariableDesignationSyntax svd } dp
            => (svd.Identifier.Text, $"({subject} as {dp.Type})"),
        VarPatternSyntax { Designation: SingleVariableDesignationSyntax svd2 }
            => (svd2.Identifier.Text, subject),
        _ => (null, null),
    };

    private void EmitIf(IfStatementSyntax ifs)
    {
        // declaration-pattern → declare-cast form
        if (_patterns is not null && ifs.Condition is IsPatternExpressionSyntax isp
            && isp.Pattern is DeclarationPatternSyntax
            && _patterns.TryEmitDeclarationIf(ifs))
            return;

        if (TryEmitDictionaryTryGetValueIf(ifs))
            return;

        _ctx.W.Line($"if ({_expr.Translate(ifs.Condition)}) {{");
        _ctx.W.Push(); EmitInline(ifs.Statement); _ctx.W.Pop();
        if (ifs.Else is not null)
        {
            // else-if chaining
            if (ifs.Else.Statement is IfStatementSyntax inner)
            {
                _ctx.W.Line($"}} else if ({_expr.Translate(inner.Condition)}) {{");
                _ctx.W.Push(); EmitInline(inner.Statement); _ctx.W.Pop();
                if (inner.Else is not null)
                {
                    _ctx.W.Line("} else {");
                    _ctx.W.Push(); EmitInline(inner.Else.Statement); _ctx.W.Pop();
                }
            }
            else
            {
                _ctx.W.Line("} else {");
                _ctx.W.Push(); EmitInline(ifs.Else.Statement); _ctx.W.Pop();
            }
        }
        _ctx.W.Line("}");
    }

    /// <summary>
    /// Detects <c>if (dict.TryGetValue(key, out var value)) { ... }</c> (and its negated form
    /// <c>if (!dict.TryGetValue(key, out var value)) { ... }</c>) and rewrites it using
    /// ManiaScript's <c>existskey</c>/indexer, since ManiaScript has no out-parameter equivalent.
    /// Returns <c>true</c> when the statement was consumed.
    /// </summary>
    private bool TryEmitDictionaryTryGetValueIf(IfStatementSyntax ifs)
    {
        var condition = ifs.Condition;
        var negated = false;
        if (condition is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } notExpr)
        {
            condition = notExpr.Operand;
            negated = true;
        }

        if (condition is not InvocationExpressionSyntax inv) return false;
        if (inv.Expression is not MemberAccessExpressionSyntax ma || ma.Name.Identifier.Text != "TryGetValue") return false;
        if (_ctx.Model.GetSymbolInfo(inv).Symbol is not IMethodSymbol sym) return false;
        if (!ExpressionEmitter.IsDictionaryType(sym.ContainingType)) return false;

        var args = inv.ArgumentList.Arguments;
        if (args.Count != 2) return false;
        var outArg = args[1];
        if (!outArg.RefKindKeyword.IsKind(SyntaxKind.OutKeyword)) return false;
        if (outArg.Expression is not DeclarationExpressionSyntax decl) return false;
        if (decl.Designation is not SingleVariableDesignationSyntax desig) return false;

        var recv = _expr.Translate(ma.Expression);
        var keyArg = _expr.Translate(args[0].Expression);
        var valueName = NameMangler.Local(desig.Identifier.Text);
        var msValueType = TypeMapper.Map(sym.Parameters[1].Type);

        if (!negated)
        {
            _ctx.W.Line($"if ({recv}.existskey({keyArg})) {{");
            _ctx.W.Push();
            _ctx.W.Line($"declare {msValueType} {valueName} = {recv}[{keyArg}];");
            EmitInline(ifs.Statement);
            _ctx.W.Pop();
            if (ifs.Else is not null)
            {
                _ctx.W.Line("} else {");
                _ctx.W.Push(); EmitInline(ifs.Else.Statement); _ctx.W.Pop();
            }
            _ctx.W.Line("}");
        }
        else
        {
            // The value is only definitely assigned on the "exists" path, so declare it
            // ahead of the if and assign it in the (possibly synthetic) else branch.
            _ctx.W.Line($"declare {msValueType} {valueName};");
            _ctx.W.Line($"if (!{recv}.existskey({keyArg})) {{");
            _ctx.W.Push(); EmitInline(ifs.Statement); _ctx.W.Pop();
            _ctx.W.Line("} else {");
            _ctx.W.Push();
            _ctx.W.Line($"{valueName} = {recv}[{keyArg}];");
            if (ifs.Else is not null) EmitInline(ifs.Else.Statement);
            _ctx.W.Pop();
            _ctx.W.Line("}");
        }
        return true;
    }

    private void EmitForeach(ForEachStatementSyntax fes)
    {
        // If an event-loop injector is active and this foreach iterates a known event list,
        // inject the generated event-dispatching switch into the body instead of auto-generating
        // a separate foreach at the end of the while loop.
        if (_ctx.EventLoopInjector != null
            && fes.Expression is IdentifierNameSyntax evtId
            && evtId.Identifier.Text is "PendingEvents")
        {
            // Force the loop variable to "Event" so the injected switch can reference Event.Type etc.
            _ctx.W.Line($"foreach (Event in {_expr.Translate(fes.Expression)}) {{");
            _ctx.W.Push();
            EmitInline(fes.Statement);
            _ctx.EventLoopInjector();
            _ctx.EventLoopWasInjected = true;
            _ctx.W.Pop();
            _ctx.W.Line("}");
            return;
        }

        if (TryEmitDictionaryForeach(fes)) return;

        var name = NameMangler.Local(fes.Identifier.Text);
        _ctx.W.Line($"foreach ({name} in {_expr.Translate(fes.Expression)}) {{");
        _ctx.W.Push(); EmitInline(fes.Statement); _ctx.W.Pop();
        _ctx.W.Line("}");
    }

    /// <summary>
    /// Handles <c>foreach</c> over a <c>Dictionary</c> (or its <c>.Keys</c>/<c>.Values</c>), all of
    /// which ManiaScript requires as <c>foreach (Key => Value in Dict)</c> — there is no plain
    /// single-variable form for associative arrays. Returns <see langword="false"/> when
    /// <paramref name="fes"/> isn't one of these cases, so the caller falls back to the plain form.
    /// </summary>
    private bool TryEmitDictionaryForeach(ForEachStatementSyntax fes)
    {
        // foreach (var k in dict.Keys) / foreach (var v in dict.Values) — the ignored side gets
        // a synthesized placeholder name; the used side keeps the C#-declared loop variable name.
        if (fes.Expression is MemberAccessExpressionSyntax ma && ma.Name.Identifier.Text is "Keys" or "Values"
            && ExpressionEmitter.IsDictionaryType(_ctx.Model.GetTypeInfo(ma.Expression).Type as INamedTypeSymbol))
        {
            var loopName = NameMangler.Local(fes.Identifier.Text);
            var isKeys = ma.Name.Identifier.Text == "Keys";
            var keyName = isKeys ? loopName : loopName + "Key";
            var valName = isKeys ? loopName + "Value" : loopName;

            _ctx.W.Line($"foreach ({keyName} => {valName} in {_expr.Translate(ma.Expression)}) {{");
            _ctx.W.Push(); EmitInline(fes.Statement); _ctx.W.Pop();
            _ctx.W.Line("}");
            return true;
        }

        // foreach (var pair in dict) → foreach (PairKey => PairValue in Dict), remapping
        // pair.Key/pair.Value to the bare Key/Value names inside the loop body.
        if (ExpressionEmitter.IsDictionaryType(_ctx.Model.GetTypeInfo(fes.Expression).Type as INamedTypeSymbol))
        {
            var pairName = fes.Identifier.Text;
            if (HasUnsupportedPairUsage(fes, pairName))
            {
                _ctx.Report(Diagnostics.Unsupported, fes.GetLocation(),
                    $"foreach (var {pairName} in ...) over a Dictionary used beyond .Key/.Value " +
                    "(switch to 'foreach (var (key, value) in ...)' instead)");
                return false;
            }

            var baseName = NameMangler.Local(pairName);
            var keyName = baseName + "Key";
            var valName = baseName + "Value";

            _ctx.DictPairLocals[pairName] = (keyName, valName);
            _ctx.W.Line($"foreach ({keyName} => {valName} in {_expr.Translate(fes.Expression)}) {{");
            _ctx.W.Push(); EmitInline(fes.Statement); _ctx.W.Pop();
            _ctx.W.Line("}");
            _ctx.DictPairLocals.Remove(pairName);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="pairName"/> is referenced in
    /// <paramref name="fes"/>'s body some way other than <c>.Key</c>/<c>.Value</c> member access
    /// (e.g. passed around whole), which the automatic Key/Value rewrite cannot express.
    /// </summary>
    private bool HasUnsupportedPairUsage(ForEachStatementSyntax fes, string pairName)
    {
        foreach (var id in fes.Statement.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (id.Identifier.Text != pairName) continue;
            if (_ctx.Model.GetSymbolInfo(id).Symbol is not ILocalSymbol) continue;
            if (id.Parent is MemberAccessExpressionSyntax { Name.Identifier.Text: "Key" or "Value" } ma2
                && ma2.Expression == id)
                continue;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="expr"/> is a <c>+=</c> event subscription
    /// (<c>x.Event += handler</c> or <c>Event += handler</c>) so the statement can be silently
    /// skipped — the EventCollector already processes it to build the event loop.
    /// </summary>
    private bool IsEventSubscription(ExpressionSyntax expr)
    {
        if (expr is not AssignmentExpressionSyntax asg) return false;
        if (!asg.OperatorToken.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PlusEqualsToken)) return false;
        return _ctx.Model.GetSymbolInfo(asg.Left).Symbol is IEventSymbol;
    }

    /// <summary>
    /// Translates <c>throw new SomeException("msg")</c> (or bare <c>throw;</c>) to
    /// <c>assert(False, "msg");</c> or <c>assert(False);</c>.
    /// </summary>
    internal void EmitThrow(ThrowStatementSyntax ts)
    {
        var msg = ExtractThrowMessage(ts.Expression);
        _ctx.W.Line(msg is null ? "assert(False);" : $"assert(False, {msg});");
    }

    /// <summary>
    /// Extracts a ManiaScript-ready message string from a throw expression, if available.
    /// Handles <c>new Exception("literal")</c>, <c>new Exception($"interpolated")</c>, etc.
    /// Returns <c>null</c> when no message argument is present.
    /// </summary>
    internal static string? ExtractThrowMessage(ExpressionSyntax? expr)
    {
        if (expr is not ObjectCreationExpressionSyntax oc) return null;
        var args = oc.ArgumentList?.Arguments;
        if (args is null || args.Value.Count == 0) return null;
        // Use the first argument as the message; translate string literals and interpolations.
        var first = args.Value[0].Expression;
        return first switch
        {
            LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression)
                => lit.Token.Text,                                       // already quoted
            InterpolatedStringExpressionSyntax istr
                => $"\"{istr.Contents}\"",                               // best-effort fallback
            _ => null                                                    // skip non-literal messages
        };
    }

    private void EmitForeachVariable(ForEachVariableStatementSyntax fev)
    {
        if (fev.Variable is DeclarationExpressionSyntax de &&
            de.Designation is ParenthesizedVariableDesignationSyntax pvd &&
            pvd.Variables.Count == 2 &&
            pvd.Variables[0] is SingleVariableDesignationSyntax k &&
            pvd.Variables[1] is SingleVariableDesignationSyntax v)
        {
            var keyName = NameMangler.Local(k.Identifier.Text);
            var valName = NameMangler.Local(v.Identifier.Text);

            // `.Index()` guarantees a 0-based sequential position on *any* list, unlike a
            // ManiaScript array's native key (Key => Val), which is only sequential for plain
            // ordered arrays — not for associative/dictionary-style ones. So it can't reuse
            // that sugar; instead declare a counter before the loop and bump it each iteration.
            if (TryUnwrapIndexCall(fev.Expression, out var indexSource))
            {
                _ctx.W.Line($"declare Integer {keyName} = 0;");
                _ctx.W.Line($"foreach ({valName} in {_expr.Translate(indexSource)}) {{");
                _ctx.W.Push();
                EmitInline(fev.Statement);
                _ctx.W.Line($"{keyName} += 1;");
                _ctx.W.Pop();
                _ctx.W.Line("}");
                return;
            }

            // foreach (var (i, x) in arr) → foreach (I => X in arr), using the array's native key.
            _ctx.W.Line($"foreach ({keyName} => {valName} in {_expr.Translate(fev.Expression)}) {{");
            _ctx.W.Push(); EmitInline(fev.Statement); _ctx.W.Pop();
            _ctx.W.Line("}");
            return;
        }
        _ctx.Report(Diagnostics.Unsupported, fev.GetLocation(), "complex foreach destructuring");
    }

    /// <summary>
    /// Recognizes <c>source.Index()</c> (the .NET <c>Enumerable.Index</c> LINQ method, which
    /// pairs each element with its 0-based position) and returns the underlying
    /// <paramref name="source"/> expression to iterate manually.
    /// </summary>
    private bool TryUnwrapIndexCall(ExpressionSyntax expr, out ExpressionSyntax source)
    {
        source = expr;
        if (expr is not InvocationExpressionSyntax inv) return false;
        if (inv.ArgumentList.Arguments.Count != 0) return false;
        if (inv.Expression is not MemberAccessExpressionSyntax ma) return false;
        if (ma.Name.Identifier.Text != "Index") return false;
        if (_ctx.Model.GetSymbolInfo(ma).Symbol is not IMethodSymbol sym) return false;
        if ((sym.ReducedFrom ?? sym).ContainingType?.ToDisplayString() != "System.Linq.Enumerable") return false;

        source = ma.Expression;
        return true;
    }

    private void EmitFor(ForStatementSyntax fs)
    {
        // Canonical for(int i = lo; i (<|<=) hi; i++) → ManiaScript for(I, lo, hi-or-hi-1)
        if (TryCanonicalFor(fs, out var name, out var lo, out var hi))
        {
            _ctx.W.Line($"for ({name}, {lo}, {hi}) {{");
            _ctx.W.Push(); EmitInline(fs.Statement); _ctx.W.Pop();
            _ctx.W.Line("}");
            return;
        }

        // Fallback while form — covers descending loops, custom steps, multiple declared
        // variables, and loop variables reused from an outer scope (no fresh `declare`).
        if (fs.Declaration is not null)
        {
            var typeSym = _ctx.Model.GetTypeInfo(fs.Declaration.Type).Type;
            var msType = TypeMapper.Map(typeSym);
            foreach (var v in fs.Declaration.Variables)
            {
                var init = v.Initializer is null ? "" : $" = {_expr.Translate(v.Initializer.Value)}";
                _ctx.W.Line(fs.Declaration.Type.IsVar
                    ? $"declare {NameMangler.Local(v.Identifier.Text)}{init};"
                    : $"declare {msType} {NameMangler.Local(v.Identifier.Text)}{init};");
            }
        }
        else
        {
            foreach (var init in fs.Initializers)
                _ctx.W.Line(_expr.Translate(init) + ";");
        }
        _ctx.W.Line($"while ({(fs.Condition is null ? "True" : _expr.Translate(fs.Condition))}) {{");
        _ctx.W.Push();
        EmitInline(fs.Statement);
        foreach (var inc in fs.Incrementors)
            _ctx.W.Line(_expr.Translate(inc) + ";");
        _ctx.W.Pop();
        _ctx.W.Line("}");
    }

    /// <summary>
    /// Matches the only shape ManiaScript's <c>for(Var, Low, High)</c> can express: a single
    /// declared variable, a condition comparing that same variable against a bound, and an
    /// incrementor that steps it by exactly 1 (<c>i++</c>, <c>++i</c>, or <c>i += 1</c>).
    /// Everything else (descending loops, custom steps, multiple variables, reused variables,
    /// non-matching condition subject) falls back to a <c>while</c> loop in <see cref="EmitFor"/>.
    /// </summary>
    private bool TryCanonicalFor(ForStatementSyntax fs, out string name, out string lo, out string hi)
    {
        name = lo = hi = "";
        if (fs.Declaration is null || fs.Declaration.Variables.Count != 1) return false;
        var v = fs.Declaration.Variables[0];
        if (v.Initializer is null) return false;
        if (fs.Condition is not BinaryExpressionSyntax cond) return false;
        if (cond.Left is not IdentifierNameSyntax condVar || condVar.Identifier.Text != v.Identifier.Text) return false;
        if (!IsUnitIncrement(fs.Incrementors, v.Identifier.Text)) return false;

        name = NameMangler.Local(v.Identifier.Text);
        lo = _expr.Translate(v.Initializer.Value);
        var bound = _expr.Translate(cond.Right);
        hi = cond.OperatorToken.Text switch
        {
            "<" => $"{bound} - 1",
            "<=" => bound,
            _ => "",
        };
        return hi.Length > 0;
    }

    /// <summary>True when the sole incrementor increases <paramref name="varName"/> by exactly 1.</summary>
    private static bool IsUnitIncrement(SeparatedSyntaxList<ExpressionSyntax> incrementors, string varName)
    {
        if (incrementors.Count != 1) return false;
        return incrementors[0] switch
        {
            PostfixUnaryExpressionSyntax post when post.IsKind(SyntaxKind.PostIncrementExpression)
                => post.Operand is IdentifierNameSyntax id && id.Identifier.Text == varName,
            PrefixUnaryExpressionSyntax pre when pre.IsKind(SyntaxKind.PreIncrementExpression)
                => pre.Operand is IdentifierNameSyntax id && id.Identifier.Text == varName,
            AssignmentExpressionSyntax asg when asg.IsKind(SyntaxKind.AddAssignmentExpression)
                => asg.Left is IdentifierNameSyntax id && id.Identifier.Text == varName
                   && asg.Right is LiteralExpressionSyntax { Token.ValueText: "1" },
            _ => false,
        };
    }

    private void EmitSwitch(SwitchStatementSyntax sws)
    {
        var anyTypePattern = sws.Sections.Any(sec => sec.Labels.OfType<CasePatternSwitchLabelSyntax>()
            .Any(l => l.Pattern is DeclarationPatternSyntax or TypePatternSyntax));
        var keyword = anyTypePattern ? "switchtype" : "switch";
        var subject = _expr.Translate(sws.Expression);

        _ctx.W.Line($"{keyword} ({subject}) {{");
        _ctx.W.Push();
        foreach (var sec in sws.Sections)
            EmitSection(sec, anyTypePattern, subject);
        _ctx.W.Pop();
        _ctx.W.Line("}");
    }

    private void EmitSection(SwitchSectionSyntax sec, bool isTypeSwitch, string subject)
    {
        string? boundName = null;
        string? boundType = null;

        foreach (var label in sec.Labels)
        {
            switch (label)
            {
                case CaseSwitchLabelSyntax cs:
                    _ctx.W.Line($"case {_expr.Translate(cs.Value)}: {{");
                    break;
                case CasePatternSwitchLabelSyntax cps:
                    switch (cps.Pattern)
                    {
                        case DeclarationPatternSyntax dp:
                            boundType = dp.Type.ToString();
                            if (dp.Designation is SingleVariableDesignationSyntax svd)
                                boundName = NameMangler.Local(svd.Identifier.Text);
                            _ctx.W.Line($"case {boundType}: {{");
                            break;
                        case TypePatternSyntax tp:
                            boundType = tp.Type.ToString();
                            _ctx.W.Line($"case {boundType}: {{");
                            break;
                        case ConstantPatternSyntax cp:
                            _ctx.W.Line($"case {_expr.Translate(cp.Expression)}: {{");
                            break;
                        default:
                            _ctx.W.Line($"case {cps.Pattern}: {{");
                            break;
                    }
                    break;
                case DefaultSwitchLabelSyntax:
                    _ctx.W.Line("default: {");
                    break;
            }
        }

        _ctx.W.Push();
        if (boundName is not null && boundType is not null)
            _ctx.W.Line($"declare {boundName} = ({subject} as {boundType});");
        foreach (var s in sec.Statements)
        {
            if (s is BreakStatementSyntax) continue; // implicit in ManiaScript switch
            Emit(s);
        }
        _ctx.W.Pop();
        _ctx.W.Line("}");
    }

    /// <summary>
    /// Translates <c>OnChange(value, oldValue => { ... })</c> into
    /// <c>if (Value != OldValue) { ...; OldValue = Value; }</c>, using the backing global
    /// already declared by <see cref="OnChangeCollector"/>. Returns <c>true</c> when the
    /// statement was consumed (translated, or reported as an unsupported shape).
    /// </summary>
    private bool TryEmitOnChange(InvocationExpressionSyntax inv)
    {
        if (!OnChangeSupport.TryMatch(_ctx.Model, inv, out var match))
        {
            if (!OnChangeSupport.IsOnChangeMethod(_ctx.Model.GetSymbolInfo(inv).Symbol as IMethodSymbol))
                return false;
            _ctx.Report(Diagnostics.Unsupported, inv.GetLocation(),
                "OnChange(...) call shape (expected 'OnChange(value, oldValue => { ... })' " +
                "with a single field/property reference and a single-parameter callback)");
            return true;
        }

        var valueText = _expr.Translate(match.ValueExpr);
        _ctx.W.Line($"if ({valueText} != {match.BackingName}) {{");
        _ctx.W.Push();

        _ctx.DeclareForLocals[match.CallbackParamName] = match.BackingName;
        switch (match.Callback.Body)
        {
            case BlockSyntax block:
                foreach (var s in block.Statements) Emit(s);
                break;
            case ExpressionSyntax bodyExpr:
                _ctx.W.Line(_expr.Translate(bodyExpr) + ";");
                break;
        }
        _ctx.DeclareForLocals.Remove(match.CallbackParamName);

        _ctx.W.Line($"{match.BackingName} = {valueText};");
        _ctx.W.Pop();
        _ctx.W.Line("}");
        return true;
    }

    /// <summary>
    /// Detects <c>Persistent&lt;T&gt;.For(provider, out var x)</c>,
    /// <c>Local&lt;T&gt;.For(...)</c>, and <c>Metadata&lt;T&gt;.For(...)</c>
    /// and emits the ManiaScript <c>declare [keyword] Type [Prefix_]VarName for provider;</c> form.
    /// Returns <c>true</c> when the statement was consumed.
    /// </summary>
    private bool TryEmitDeclareFor(InvocationExpressionSyntax inv)
    {
        // Must be a static member access: Xxx<T>.For(...)
        if (inv.Expression is not MemberAccessExpressionSyntax ma) return false;
        if (ma.Name.Identifier.Text != "For") return false;
        if (ma.Expression is not GenericNameSyntax generic) return false;

        var outerName = generic.Identifier.Text;
        if (outerName is not ("Persistent" or "Local" or "Metadata" or "Netwrite" or "Netread")) return false;

        // Verify via symbol that it's from ManiaScriptSharp when resolution succeeds;
        // if the symbol is unresolved (e.g. assembly missing from Roslyn compilation)
        // we trust the name check above and proceed anyway.
        var sym = _ctx.Model.GetSymbolInfo(inv).Symbol as IMethodSymbol;
        var ns = sym?.ContainingType?.ContainingNamespace?.ToDisplayString();
        if (ns is not null && ns != "ManiaScriptSharp") return false;

        // Args: For(provider, out var x [, callerExpression])
        var args = inv.ArgumentList.Arguments;
        if (args.Count < 2) return false;

        var outArg = args[1];
        if (!outArg.RefKindKeyword.IsKind(SyntaxKind.OutKeyword)) return false;
        if (outArg.Expression is not DeclarationExpressionSyntax decl) return false;
        if (decl.Designation is not SingleVariableDesignationSyntax desig) return false;

        // Resolve type T from the generic argument list.
        var typeArgSyntax = generic.TypeArgumentList.Arguments.FirstOrDefault();
        if (typeArgSyntax is null) return false;
        var typeSym = _ctx.Model.GetTypeInfo(typeArgSyntax).Type;
        var msType = TypeMapper.Map(typeSym);

        // Provider expression (first argument).
        var provider = _expr.Translate(args[0].Expression);

        // ManiaScript variable name and optional keyword.
        var rawName = desig.Identifier.Text;
        var (keyword, prefix) = outerName switch
        {
            "Persistent" => ("persistent ", "Persistent_"),
            "Metadata"   => ("metadata ",   "Metadata_"),
            "Netwrite"   => ("netwrite ",   "Net_"),
            "Netread"    => ("netread ",    "Net_"),
            _            => ("",             ""),           // Local
        };
        // For Netwrite/Netread, strip any existing net_ prefix the user may have typed
        // to avoid double-prefixing (e.g. net_Score → Net_Score, not Net_Net_Score).
        if (outerName is "Netwrite" or "Netread"
            && rawName.StartsWith("net_", System.StringComparison.OrdinalIgnoreCase))
            rawName = rawName.Substring(4);
        var msVarName = prefix + NameMangler.PascalCase(rawName);

        _ctx.W.Line($"declare {keyword}{msType} {msVarName} for {provider};");

        // Register local name → ms-name mapping so TranslateIdentifier emits the correct prefixed name.
        _ctx.DeclareForLocals[desig.Identifier.Text] = msVarName;

        return true;
    }
}
