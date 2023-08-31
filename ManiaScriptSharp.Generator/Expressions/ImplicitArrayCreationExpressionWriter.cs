using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class ImplicitArrayCreationExpressionWriter : ExpressionWriter<ImplicitArrayCreationExpressionSyntax>
{
    public override void Write(ImplicitArrayCreationExpressionSyntax expression)
    {
        Writer.Write('[');

        var first = true;

        foreach (var e in expression.Initializer.Expressions)
        {
            if (first)
            {
                first = false;
            }
            else
            {
                Writer.Write(", ");
            }

            WriteSyntax(e);
        }

        Writer.Write(']');
    }
}