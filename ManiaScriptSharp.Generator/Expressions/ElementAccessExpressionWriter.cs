using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class ElementAccessExpressionWriter : ExpressionWriter<ElementAccessExpressionSyntax>
{
    public override void Write(ElementAccessExpressionSyntax expression)
    {
        WriteSyntax(expression.Expression);

        foreach (var argument in expression.ArgumentList.Arguments)
        {
            Writer.Write('[');
            WriteSyntax(argument.Expression);
            Writer.Write(']');
        }
    }
}
