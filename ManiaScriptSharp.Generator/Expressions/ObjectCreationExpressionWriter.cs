using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class ObjectCreationExpressionWriter : ExpressionWriter<ObjectCreationExpressionSyntax>
{
    public override void Write(ObjectCreationExpressionSyntax expression)
    {
        var symbol = GetSymbol(expression.Type);

        if (symbol?.Name == "Vec2" && symbol.ContainingNamespace?.Name == "ManiaScriptSharp")
        {
            Writer.Write('<');

            var first = true;

            foreach (var argument in expression.ArgumentList?.Arguments ?? Enumerable.Empty<ArgumentSyntax>())
            {
                if (!first)
                {
                    Writer.Write(", ");
                }

                first = false;

                WriteSyntax(argument.Expression);

                if (argument.Expression is LiteralExpressionSyntax literal && literal.Token.Value is int)
                {
                    Writer.Write('.');
                }
            }

            Writer.Write('>');

            return;
        }

        if (symbol?.Name == "Dictionary")
        {
            Writer.Write("[]");
            return;
        }

        Writer.Write("/* ");
        Writer.Write(expression);
        Writer.Write(" */");
    }
}
