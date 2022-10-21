using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class BinaryExpressionWriter : ExpressionWriter<BinaryExpressionSyntax>
{
    public override void Write(BinaryExpressionSyntax expression)
    {
        WriteSyntax(expression.Left);
        Writer.Write(' ');

        if (expression.OperatorToken.Text == "+")
        {
            // if left literal expression is string
            if (expression.Left is LiteralExpressionSyntax {Token.Value: string}
                || expression.Right is LiteralExpressionSyntax {Token.Value: string})
            {
                Writer.Write('^');
            }
            else
            {
                Writer.Write('+');
            }
        }
        else
        {
            Writer.Write(expression.OperatorToken.Text);
        }
        
        Writer.Write(' ');
        WriteSyntax(expression.Right);
    }
}