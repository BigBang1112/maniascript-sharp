using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>
/// Translates C# `is` patterns into ManiaScript. Complex patterns that bind variables or
/// combine clauses are decomposed into nested if blocks via <see cref="EmitTypePatternBinding"/>.
/// </summary>
internal sealed class PatternEmitter
{
    private readonly EmitContext _ctx;
    private readonly ExpressionEmitter _expr;
    private readonly StatementEmitter _stmt;

    public PatternEmitter(EmitContext ctx, ExpressionEmitter expr, StatementEmitter stmt)
    { _ctx = ctx; _expr = expr; _stmt = stmt; }

    /// <summary>Translate an `is` pattern that appears as a Boolean expression (no variable bound).</summary>
    public string TranslateAsExpression(IsPatternExpressionSyntax isp)
    {
        var lhs = _expr.Translate(isp.Expression);
        return Translate(lhs, isp.Pattern);
    }

    public string Translate(string lhs, PatternSyntax p) => p switch
    {
        ConstantPatternSyntax cp => $"{lhs} == {_expr.Translate(cp.Expression)}",
        RelationalPatternSyntax rp => $"{lhs} {rp.OperatorToken.Text} {_expr.Translate(rp.Expression)}",
        DeclarationPatternSyntax dp => $"{lhs} is {dp.Type}",
        TypePatternSyntax tp => $"{lhs} is {tp.Type}",
        UnaryPatternSyntax up when up.OperatorToken.IsKind(SyntaxKind.NotKeyword)
            => up.Pattern switch
            {
                ConstantPatternSyntax cp => $"{lhs} != {_expr.Translate(cp.Expression)}",
                _ => $"!({Translate(lhs, up.Pattern)})",
            },
        RecursivePatternSyntax rp when rp.PropertyPatternClause is { } pc
            => "(" + string.Join(" && ", pc.Subpatterns.Select(sp =>
                $"{lhs}.{sp.NameColon?.Name.Identifier.Text ?? sp.ExpressionColon?.Expression.ToString()} == {SubpatternValue(sp.Pattern)}")) + ")",
        BinaryPatternSyntax bp
            => "(" + Translate(lhs, bp.Left) + " "
              + (bp.OperatorToken.IsKind(SyntaxKind.AndKeyword) ? "&&" : "||") + " "
              + Translate(lhs, bp.Right) + ")",
        _ => $"{lhs} is ???",
    };

    private string SubpatternValue(PatternSyntax p) => p switch
    {
        ConstantPatternSyntax cp => _expr.Translate(cp.Expression),
        _ => p.ToString(),
    };

    /// <summary>
    /// If an <c>if (control is CMlLabel label)</c> is detected, emit
    ///   <c>if (Control is CMlLabel) { declare Label = (Control as CMlLabel); ... }</c>
    /// Returns true if the if-statement was fully handled.
    /// </summary>
    public bool TryEmitDeclarationIf(IfStatementSyntax ifs)
    {
        if (ifs.Condition is not IsPatternExpressionSyntax isp) return false;
        if (isp.Pattern is not DeclarationPatternSyntax dp) return false;

        var subjectExpr = _expr.Translate(isp.Expression);
        var typeName = dp.Type.ToString();
        var bound = dp.Designation is SingleVariableDesignationSyntax svd
            ? Naming.NameMangler.Local(svd.Identifier.Text)
            : null;

        _ctx.W.Line($"if ({subjectExpr} is {typeName}) {{");
        _ctx.W.Push();
        if (bound is not null)
            _ctx.W.Line($"declare {bound} = ({subjectExpr} as {typeName});");
        _stmt.EmitInline(ifs.Statement);
        _ctx.W.Pop();
        EmitElseChain(ifs.Else);
        _ctx.W.Line("}");
        return true;
    }

    /// <summary>
    /// Recursively emit chained else-ifs so <c>else if (x is T)</c> stays flat instead of nesting.
    /// </summary>
    private void EmitElseChain(ElseClauseSyntax? elseClause)
    {
        if (elseClause is null) return;
        if (elseClause.Statement is IfStatementSyntax inner)
        {
            if (inner.Condition is IsPatternExpressionSyntax innerIs && innerIs.Pattern is DeclarationPatternSyntax innerDp)
            {
                var subj = _expr.Translate(innerIs.Expression);
                var t = innerDp.Type.ToString();
                var b = innerDp.Designation is SingleVariableDesignationSyntax sv ? Naming.NameMangler.Local(sv.Identifier.Text) : null;
                _ctx.W.Line($"}} else if ({subj} is {t}) {{");
                _ctx.W.Push();
                if (b is not null) _ctx.W.Line($"declare {b} = ({subj} as {t});");
                _stmt.EmitInline(inner.Statement);
                _ctx.W.Pop();
                EmitElseChain(inner.Else);
            }
            else
            {
                _ctx.W.Line($"}} else if ({_expr.Translate(inner.Condition)}) {{");
                _ctx.W.Push();
                _stmt.EmitInline(inner.Statement);
                _ctx.W.Pop();
                EmitElseChain(inner.Else);
            }
        }
        else
        {
            _ctx.W.Line("} else {");
            _ctx.W.Push();
            _stmt.EmitInline(elseClause.Statement);
            _ctx.W.Pop();
        }
    }
}
