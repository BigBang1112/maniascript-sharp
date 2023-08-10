using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class InvocationExpressionWriter : ExpressionWriter<InvocationExpressionSyntax>
{
    public override void Write(InvocationExpressionSyntax expression)
    {
        var symbol = GetSymbol(expression.Expression);

        var identifierNameSyntax = expression.Expression as IdentifierNameSyntax;

        if (symbol is null)
        {
            if (identifierNameSyntax is not null)
            {
                if (identifierNameSyntax.Identifier.Text == "nameof")
                {
                    Writer.Write('"');
                    WriteSyntax(expression.ArgumentList.Arguments[0].Expression);
                    Writer.Write('"');
                    return;
                }
            }

            Writer.Write("/* InvocationExpressionWriter: unknown symbol */");
            return;
        }

        if (symbol.Name is "Get" or "Set" && CachedData.DeclarationModes.Contains(symbol.ContainingType.Name)
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

        if (symbol.IsVirtual)
        {
            Writer.Write("+++");
        }
        
        if (symbol.DeclaredAccessibility is Accessibility.Private)
        {
            Writer.Write("Private_");
        }
        
        WriteSyntax(expression.Expression);

        if (symbol.IsVirtual)
        {
            Writer.Write("+++");
            return;
        }

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
            
            WriteSyntax(argument.Expression);
        }

        Writer.Write(')');
    }
}