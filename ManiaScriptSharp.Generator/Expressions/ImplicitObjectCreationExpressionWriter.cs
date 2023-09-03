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
            case "ImmutableArray":
                Writer.Write("[]");
                break;
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
