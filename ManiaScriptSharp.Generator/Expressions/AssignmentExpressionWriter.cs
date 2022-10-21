using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class AssignmentExpressionWriter : ExpressionWriter<AssignmentExpressionSyntax>
{
    public override void Write(AssignmentExpressionSyntax expression)
    {
        WriteSyntax(expression.Left);
        Writer.Write(" = ");
        WriteSyntax(expression.Right);
    }
}