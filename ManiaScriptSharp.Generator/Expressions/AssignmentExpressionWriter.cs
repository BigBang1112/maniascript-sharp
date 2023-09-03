using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class AssignmentExpressionWriter : ExpressionWriter<AssignmentExpressionSyntax>
{
    public override void Write(AssignmentExpressionSyntax expression)
    {
        var symbol = GetSymbol(expression.Left);

        if (symbol is IPropertySymbol property && property.GetAttributes()
            .Any(x => x.AttributeClass?.Name is NameConsts.NetwriteAttribute or NameConsts.LocalAttribute))
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

        if (expression.Right is LiteralExpressionSyntax { Token.Value: int }
            or PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax { Token.Value: int } } && IsReal(symbol))
        {
            Writer.Write(".0");
        }
    }

    private bool IsReal(ISymbol? symbol) => symbol
        is IPropertySymbol { Type.Name: "Single" or "Double" }
        or IFieldSymbol { Type.Name: "Single" or "Double" };
}