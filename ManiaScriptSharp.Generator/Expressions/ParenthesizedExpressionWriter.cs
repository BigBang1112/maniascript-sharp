using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class ParenthesizedExpressionWriter : ExpressionWriter<ParenthesizedExpressionSyntax>
{
    public override void Write(ParenthesizedExpressionSyntax expression)
    {
        Writer.Write(expression.OpenParenToken.Text);
        WriteSyntax(expression.Expression);
        Writer.Write(expression.CloseParenToken.Text);
    }
}