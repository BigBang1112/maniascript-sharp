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
