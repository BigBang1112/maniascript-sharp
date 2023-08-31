using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class AssignmentExpressionWriter : ExpressionWriter<AssignmentExpressionSyntax>
{
    public override void Write(AssignmentExpressionSyntax expression)
    {
        var symbol = GetSymbol(expression.Left);

        if (symbol is IPropertySymbol property && property.GetAttributes().Any(x => x.AttributeClass?.Name is NameConsts.NetwriteAttribute or NameConsts.LocalAttribute))
        {
            Writer.Write("Set");
            Writer.Write(property.Name);
            Writer.Write('(');
            WriteSyntax(expression.Right);
            Writer.Write(')');

            return;
        }

        WriteSyntax(expression.Left);
        Writer.Write(' ');
        Writer.Write(expression.OperatorToken.Text);
        Writer.Write(' ');
        WriteSyntax(expression.Right);
    }
}