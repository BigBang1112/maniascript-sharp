using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class TypeExpressionBuilder : ExpressionBuilder<TypeSyntax>
{
    public override void Write(int ident, TypeSyntax expression,
        ImmutableDictionary<string, ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        var symbol = bodyBuilder.SemanticModel.GetSymbolInfo(expression).Symbol;

        if (symbol is null)
        {
            throw new ExpressionStatementException("Symbol not found");
        }
        
        Writer.Write(Standardizer.CSharpTypeToManiaScriptType(symbol.Name));
    }
}