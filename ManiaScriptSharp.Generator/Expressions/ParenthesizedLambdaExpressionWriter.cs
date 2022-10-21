using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class ParenthesizedLambdaExpressionWriter : ExpressionWriter<ParenthesizedLambdaExpressionSyntax>
{
    public override void Write(ParenthesizedLambdaExpressionSyntax expression)
    {
        if (expression.ExpressionBody is null)
        {
            Writer.Write("/* ExpressionBody is null */");
            return;
        }
        
        WriteSyntax(expression.ExpressionBody);
    }
}