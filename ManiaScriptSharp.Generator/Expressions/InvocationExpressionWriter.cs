using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class InvocationExpressionWriter : ExpressionWriter<InvocationExpressionSyntax>
{
    public override void Write(InvocationExpressionSyntax expression)
    {
        var symbol = GetSymbol(expression.Expression);

        if (symbol?.Name is "Get" or "Set" && CachedData.DeclarationModes.Contains(symbol.ContainingType.Name)
                                          && expression.Expression is MemberAccessExpressionSyntax memberAccessExpressionSyntax)
        {
            WriteSyntax(memberAccessExpressionSyntax.Expression);

            if (symbol.Name is not "Set")
            {
                return;
            }

            Writer.Write(" = ");
            WriteSyntax(expression.ArgumentList.Arguments[0].Expression);

            return;
        }
        
        if (symbol?.DeclaredAccessibility is Accessibility.Private)
        {
            Writer.Write("Private_");
        }
        
        WriteSyntax(expression.Expression);
        
        if (symbol?.Name == "Yield" && symbol.ContainingType.Name == "ManiaScript")
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
            
            WriteSyntax(argument.Expression);
        }

        Writer.Write(')');
    }
}