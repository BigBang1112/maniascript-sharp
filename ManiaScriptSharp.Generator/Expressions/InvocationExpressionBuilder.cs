using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class InvocationExpressionBuilder : ExpressionBuilder<InvocationExpressionSyntax>
{
    public override void Write(int ident, InvocationExpressionSyntax expression,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        var symbol = bodyBuilder.SemanticModel.GetSymbolInfo(expression.Expression).Symbol;

        if (symbol is null)
        {
            throw new Exception("Symbol does not exist");
        }
        
        WriteSyntax(ident, expression.Expression, parameters, bodyBuilder);
        
        if (symbol.Name == "Yield" && symbol.ContainingType.Name == "ManiaScript")
        {
            return;
        }
        
        Writer.Write('(');

        for (var i = 0; i < expression.ArgumentList.Arguments.Count; i++)
        {
            if (i != 0)
            {
                Writer.Write(", ");
            }
            
            var argument = expression.ArgumentList.Arguments[i];
            
            WriteSyntax(ident, argument.Expression, parameters, bodyBuilder);
        }

        Writer.Write(')');
    }
}