using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class ParenthesizedLambdaExpressionBuilder : ExpressionBuilder<ParenthesizedLambdaExpressionSyntax>
{
    public override void Write(int ident, ParenthesizedLambdaExpressionSyntax expression,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        if (expression.ExpressionBody is null)
        {
            Writer.Write("/* ExpressionBody is null */");
            return;
        }
        
        WriteSyntax(ident, expression.ExpressionBody, parameters, bodyBuilder);
    }
}