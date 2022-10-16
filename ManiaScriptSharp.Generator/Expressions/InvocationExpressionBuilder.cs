using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class InvocationExpressionBuilder : ExpressionBuilder<InvocationExpressionSyntax>
{
    public override void Write(int ident, InvocationExpressionSyntax expression,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        WriteSyntax(ident, expression.Expression, parameters, bodyBuilder);

        // TODO: abstract this better in the future
        if (expression.Expression is IdentifierNameSyntax {Identifier.Text: "Yield"})
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