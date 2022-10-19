using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace ManiaScriptSharp.Generator.Expressions;

public class ElementAccessExpressionBuilder : ExpressionBuilder<ElementAccessExpressionSyntax>
{
    public override void Write(int ident, ElementAccessExpressionSyntax expression,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        Writer.Write(expression.Expression);
        Writer.Write('[');
        WriteSyntax(ident, expression.ArgumentList.Arguments[0].Expression, parameters, bodyBuilder);
        Writer.Write(']');
    }
}
