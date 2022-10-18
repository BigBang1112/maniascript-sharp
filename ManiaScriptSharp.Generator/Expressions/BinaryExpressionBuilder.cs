using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class BinaryExpressionBuilder : ExpressionBuilder<BinaryExpressionSyntax>
{
    public override void Write(int ident, BinaryExpressionSyntax expression,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        WriteSyntax(ident, expression.Left, parameters, bodyBuilder);
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
        WriteSyntax(ident, expression.Right, parameters, bodyBuilder);
    }
}