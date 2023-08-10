using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class MemberAccessExpressionWriter : ExpressionWriter<MemberAccessExpressionSyntax>
{
    public override void Write(MemberAccessExpressionSyntax expression)
    {
        switch (expression.Expression)
        {
            case BaseExpressionSyntax:
                throw new ExpressionStatementException("NOTE: Base expressions are ignored as they are not supported. They can be removed from code.");
            case ThisExpressionSyntax:
                return;
        }

        var expressionSymbol = GetSymbol(expression.Expression);

        switch (expressionSymbol)
        {
            case INamespaceSymbol:
            case {IsStatic: true, Name: "ManiaScript"}:
                WriteSyntax(expression.Name);
                return;
        }

        var nameSymbol = GetSymbol(expression.Name);

        if (expressionSymbol is ITypeSymbol {TypeKind: TypeKind.Enum} && expression.Expression is IdentifierNameSyntax)
        {
            Writer.Write(expressionSymbol.ContainingType.Name);
            Writer.Write("::");
        }

        WriteSyntax(expression.Expression);

        if (expressionSymbol?.IsStatic == true || expressionSymbol is ITypeSymbol {TypeKind: TypeKind.Enum}
                                               || nameSymbol is ITypeSymbol {TypeKind: TypeKind.Enum})
        {
            Writer.Write("::");
        }
        else
        {
            Writer.Write('.');
        }

        WriteSyntax(expression.Name);
    }
}