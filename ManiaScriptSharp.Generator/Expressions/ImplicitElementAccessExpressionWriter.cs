using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class ImplicitElementAccessExpressionWriter : ExpressionWriter<ImplicitElementAccessSyntax>
{
    public override void Write(ImplicitElementAccessSyntax expression)
    {
        Writer.Write(expression.ArgumentList.OpenBracketToken.Value);

        var first = true;

        foreach (var argument in expression.ArgumentList.Arguments)
        {
            if (!first)
            {
                Writer.Write(", ");
            }

            first = false;

            WriteSyntax(argument.Expression);
        }

        Writer.Write(expression.ArgumentList.CloseBracketToken.Value);
    }
}
