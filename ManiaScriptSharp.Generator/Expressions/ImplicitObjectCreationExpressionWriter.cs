using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class ImplicitObjectCreationExpressionWriter : ExpressionWriter<ImplicitObjectCreationExpressionSyntax>
{
    public override void Write(ImplicitObjectCreationExpressionSyntax expression)
    {
        var symbol = GetSymbol();

        switch (symbol?.ContainingType.Name)
        {
            case "Dictionary":
                {
                    if (expression.Initializer?.Kind() is not SyntaxKind.CollectionInitializerExpression)
                    {
                        Writer.Write("[]");
                        break;
                    }

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

                        if (e is not InitializerExpressionSyntax initializerExpression || initializerExpression.Expressions.Count != 2)
                        {
                            throw new Exception("Dictionary initializer expression must have 2 expressions");
                        }

                        WriteSyntax(initializerExpression.Expressions[0]);
                        Writer.Write(" => ");
                        WriteSyntax(initializerExpression.Expressions[1]);
                    }
                    Writer.Write("]");
                    return;
                }
            case "IList":
            case "List":
                Writer.Write("[]");
                break;
            case "ImmutableArray":
                Writer.Write('[');
                if (expression.Initializer is not null)
                {
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
                }
                Writer.Write("]");
                return;
            case null:
                // May leave empty assignments
                return;
        }

        if (expression.Initializer is not null)
        {
            foreach (var e in expression.Initializer.Expressions)
            {
                Writer.WriteLine(';');
                Writer.WriteIndent(Indent);

                if (expression.Parent is AssignmentExpressionSyntax assignment)
                {
                    WriteSyntax(assignment.Left);
                    WriteSyntax(e);
                }
                else if (expression.Parent is EqualsValueClauseSyntax equals && equals.Parent is VariableDeclaratorSyntax declarator)
                {
                    Writer.Write(Standardizer.StandardizeName(declarator.Identifier.Text));
                    Writer.Write('.');
                    WriteSyntax(e);
                }
                else
                {
                    Writer.Write($"// expression parent {expression.Parent?.GetType().Name ?? "unknown"}");
                }
            }
        }
    }
}
