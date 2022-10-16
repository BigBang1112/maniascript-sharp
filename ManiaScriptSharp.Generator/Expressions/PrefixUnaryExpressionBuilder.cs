using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class PrefixUnaryExpressionBuilder : ExpressionBuilder<PrefixUnaryExpressionSyntax>
{
    public override void Write(int ident, PrefixUnaryExpressionSyntax expression, ImmutableArray<ParameterSyntax> parameters,
        ManiaScriptBodyBuilder bodyBuilder)
    {
        Writer.Write(expression.OperatorToken.Text);
        WriteSyntax(ident, expression.Operand, parameters, bodyBuilder);
    }
}