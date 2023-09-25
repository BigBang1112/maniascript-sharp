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
        }

        if (symbol?.Name is "Get" or "Set" && CachedData.DeclarationModes.Contains(symbol.ContainingType.Name)
                                          && expression.Expression is MemberAccessExpressionSyntax memberAccessExpressionSyntax)
        {
            if (symbol.ContainingType.Name is "Netwrite" or "Netread")
            {
                Writer.Write("Net_");
            }

            WriteSyntax(memberAccessExpressionSyntax.Expression);

            if (symbol.Name is not "Set")
            {
                return;
            }

            Writer.Write(" = ");

            WriteSyntax(expression.ArgumentList.Arguments[0].Expression);

            return;
        }

        var isVirtual = symbol?.IsVirtual == true && symbol.Name != "ToString";

        if (isVirtual == true)
        {
            Writer.Write("+++");
        }
        
        if (symbol?.DeclaredAccessibility is Accessibility.Private)
        {
            Writer.Write("Private_");
        }

        if (symbol?.Name == "ToString" && expression.Expression is MemberAccessExpressionSyntax memberSyntax)
        {
            WriteSyntax(memberSyntax.Expression);
            Writer.Write(" ^ \"\"");
            return;
        }

        if (symbol?.Name == "ToArray" && expression.Expression is MemberAccessExpressionSyntax mm)
        {
            WriteSyntax(mm.Expression);
            return;
        }

        WriteSyntax(expression.Expression);

        if (isVirtual == true)
        {
            Writer.Write("+++");
            return;
        }

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

            if (symbol is IMethodSymbol methodSym
                && methodSym.Parameters[i].Type.Name is "Ident"
                && argument.Expression is LiteralExpressionSyntax literal && literal.Token.Value is null)
            {
                Writer.Write("NullId");
                continue;
            }

            WriteSyntax(argument.Expression);

            if (symbol is IMethodSymbol methodSymbol
                && argument.Expression is LiteralExpressionSyntax { Token.Value: int }
                or PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax { Token.Value: int } }
                && methodSymbol.Parameters[i].Type.Name is "Single" or "Double")
            {
                Writer.Write(".0");
            }

        }

        Writer.Write(')');
    }
}