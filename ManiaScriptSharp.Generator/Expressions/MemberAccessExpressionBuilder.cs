using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class MemberAccessExpressionBuilder : ExpressionBuilder<MemberAccessExpressionSyntax>
{
    public override void Write(int ident, MemberAccessExpressionSyntax expression,
        ImmutableDictionary<string, ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        var oper = ".";
        var topMostExpression = expression as ExpressionSyntax;
        
        // Validation of the member access
        while (topMostExpression is MemberAccessExpressionSyntax memberAccessExpression)
        {
            var symbol = bodyBuilder.SemanticModel.GetSymbolInfo(topMostExpression).Symbol;

            if (symbol is null)
            {
                throw new ExpressionStatementException("NOTE: Symbol does not exist.");
            }

            if (symbol is ITypeSymbol {TypeKind: TypeKind.Enum})
            {
                oper = "::";
            }
            
            topMostExpression = memberAccessExpression.Expression;
            
            if (topMostExpression is BaseExpressionSyntax)
            {
                throw new ExpressionStatementException("NOTE: Base expressions are ignored as they are not supported. They can be removed from code.");
            }
        }
        
        var topMostSymbol = bodyBuilder.SemanticModel.GetSymbolInfo(topMostExpression).Symbol;
        
        switch (topMostSymbol)
        {
            case null:
                throw new ExpressionStatementException("NOTE: Symbol does not exist.");
            case ITypeSymbol typeSymbol:
            {
                if (typeSymbol.Name == "ManiaScript")
                {
                    topMostExpression = topMostExpression.Parent as ExpressionSyntax;
                }

                break;
            }
        }
        
        // Write the member access

        var exp = topMostExpression;

        while (exp is not null && exp != expression)
        {
            switch (exp)
            {
                case IdentifierNameSyntax identName:
                    WriteSyntax(ident, identName, parameters, bodyBuilder);
                    Writer.Write(oper);
                    break;
                case MemberAccessExpressionSyntax memAccess:
                    WriteSyntax(ident, memAccess.Name, parameters, bodyBuilder);
                    Writer.Write(oper);
                    break;
            }

            exp = exp?.Parent as MemberAccessExpressionSyntax;
        }
        
        WriteSyntax(ident, expression.Name, parameters, bodyBuilder);
    }
}