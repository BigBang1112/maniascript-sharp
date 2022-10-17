using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class MemberAccessExpressionBuilder : ExpressionBuilder<MemberAccessExpressionSyntax>
{
    public override void Write(int ident, MemberAccessExpressionSyntax expression,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        if (expression.Expression is BaseExpressionSyntax)
        {
            throw new ExpressionStatementException("NOTE: Base expressions are ignored as they are not supported. They can be removed from code.");
        }

        WriteParentExpression(ident, expression, parameters, bodyBuilder);

        WriteSyntax(ident, expression.Name, parameters, bodyBuilder);
    }

    private void WriteParentExpression(int ident, MemberAccessExpressionSyntax expression, ImmutableArray<ParameterSyntax> parameters,
        ManiaScriptBodyBuilder bodyBuilder)
    {
        if (expression.Expression is ThisExpressionSyntax)
        {
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
        
        if (expressionSymbol.Name == "ManiaScript")
        {
            return;
        }

        WriteSyntax(ident, expression.Expression, parameters, bodyBuilder);
        
        var nameSymbol = bodyBuilder.SemanticModel.GetSymbolInfo(expression.Name).Symbol;

        if (expressionSymbol.IsStatic || expressionSymbol is ITypeSymbol {TypeKind: TypeKind.Enum}
            || nameSymbol is ITypeSymbol {TypeKind: TypeKind.Enum})
        {
            Writer.Write("::");
        }
        else
        {
            Writer.Write('.');
        }
    }
}