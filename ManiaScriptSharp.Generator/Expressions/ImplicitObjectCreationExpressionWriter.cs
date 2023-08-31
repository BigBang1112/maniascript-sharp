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

                if (expression.Parent is not AssignmentExpressionSyntax assignment)
                {
                    Writer.Write("// expression parent not supported");
                    continue;
                }

                Writer.WriteIndent(Indent);
                WriteSyntax(assignment.Left);
                WriteSyntax(e);
            }
        }
    }
}
