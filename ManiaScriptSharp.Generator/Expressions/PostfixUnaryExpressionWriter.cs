using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class PostfixUnaryExpressionWriter : ExpressionWriter<PostfixUnaryExpressionSyntax>
{
    public override void Write(PostfixUnaryExpressionSyntax expression)
    {
        WriteSyntax(expression.Operand);
        Writer.Write("/* Null is not expected */");
    }
}