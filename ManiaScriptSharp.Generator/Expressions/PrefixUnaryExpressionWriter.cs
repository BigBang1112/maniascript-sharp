using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class PrefixUnaryExpressionWriter : ExpressionWriter<PrefixUnaryExpressionSyntax>
{
    public override void Write(PrefixUnaryExpressionSyntax expression)
    {
        Writer.Write(expression.OperatorToken.Text);
        WriteSyntax(expression.Operand);
    }
}