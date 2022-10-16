using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class ParenthesizedExpressionBuilder : ExpressionBuilder<ParenthesizedExpressionSyntax>
{
    public override void Write(int ident, ParenthesizedExpressionSyntax expression, ImmutableArray<ParameterSyntax> parameters,
        ManiaScriptBodyBuilder bodyBuilder)
    {
        Writer.Write(expression.OpenParenToken.Text);
        WriteSyntax(ident, expression.Expression, parameters, bodyBuilder);
        Writer.Write(expression.CloseParenToken.Text);
    }
}