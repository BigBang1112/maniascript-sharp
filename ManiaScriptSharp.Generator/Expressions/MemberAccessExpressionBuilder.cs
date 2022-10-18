using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class MemberAccessExpressionBuilder : ExpressionBuilder<MemberAccessExpressionSyntax>
{
    public override void Write(int ident, MemberAccessExpressionSyntax expression,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        switch (expression.Expression)
        {
            case BaseExpressionSyntax:
                throw new ExpressionStatementException("NOTE: Base expressions are ignored as they are not supported. They can be removed from code.");
            case ThisExpressionSyntax:
                return;
        }

        var expressionSymbol = bodyBuilder.SemanticModel.GetSymbolInfo(expression.Expression).Symbol;

        switch (expressionSymbol)
        {
            case null:
                throw new ExpressionStatementException("NOTE: Symbol does not exist.");
            case INamespaceSymbol:
                return;
        }
        
        if (expressionSymbol.IsStatic && expressionSymbol.Name == "ManiaScript")
        {
            return;
        }
        
        var nameSymbol = bodyBuilder.SemanticModel.GetSymbolInfo(expression.Name).Symbol;

        if (expressionSymbol is ITypeSymbol {TypeKind: TypeKind.Enum} && expression.Expression is IdentifierNameSyntax)
        {
            Writer.Write(expressionSymbol.ContainingType.Name);
            Writer.Write("::");
        }

        WriteSyntax(ident, expression.Expression, parameters, bodyBuilder);

        if (expressionSymbol.IsStatic || expressionSymbol is ITypeSymbol {TypeKind: TypeKind.Enum}
                                      || nameSymbol is ITypeSymbol {TypeKind: TypeKind.Enum})
        {
            Writer.Write("::");
        }
        else
        {
            Writer.Write('.');
        }

        WriteSyntax(ident, expression.Name, parameters, bodyBuilder);
    }
}