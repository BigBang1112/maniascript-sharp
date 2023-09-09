using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class CastExpressionWriter : ExpressionWriter<CastExpressionSyntax>
{
    public override void Write(CastExpressionSyntax expression)
    {
        WriteSyntax(expression.Expression);
    }
}
