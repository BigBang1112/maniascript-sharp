using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class LocalDeclarationStatementWriter : StatementWriter<LocalDeclarationStatementSyntax>
{
    public override void Write(LocalDeclarationStatementSyntax statement)
    {
        if (WriteDifferentDeclarationMode(statement))
        {
            return;
        }

        foreach (var variable in statement.Declaration.Variables)
        {
            Writer.Write(Indent, "declare ");

            if (statement.Declaration.Type.IsVar)
            {
                if (GetSymbol(statement.Declaration.Type) is ITypeSymbol typeSymbol)
                {
                    Writer.Write(Standardizer.CSharpTypeToManiaScriptType(typeSymbol));
                    Writer.Write(' ');
                }
            }
            else
            {
                WriteSyntax(statement.Declaration.Type);
                Writer.Write(' ');
            }

            Writer.Write(Standardizer.StandardizeName(variable.Identifier.Text));

            if (variable.Initializer is not null)
            {
                Writer.Write(" = ");
                WriteSyntax(variable.Initializer.Value);
            }

            Writer.Write("; ");
            WriteLocationComment();
            Writer.WriteLine();
        }
    }

    private bool WriteDifferentDeclarationMode(LocalDeclarationStatementSyntax statement)
    {
        if (statement.Declaration.Variables.Count != 1)
        {
            return false;
        }

        var variable = statement.Declaration.Variables[0];

        if (variable.Initializer?.Value is not InvocationExpressionSyntax expression)
        {
            return false;
        }

        if (expression.Expression is MemberAccessExpressionSyntax memberAccessExpressionSyntax && memberAccessExpressionSyntax.Name.Identifier.Text != "For")
        {
            return false;
        }

        if (GetSymbol(expression) is not IMethodSymbol symbol
            || !CachedData.DeclarationModes.Contains(symbol.ContainingType.Name))
        {
            return false;
        }

        var declarationModeAtt = symbol.ContainingType.GetAttributes()
            .FirstOrDefault(x => x.AttributeClass?.Name == "DeclarationModeAttribute");

        var declarationModeKeyword = declarationModeAtt?.ConstructorArguments[0].Value?.ToString();
        
        if (declarationModeKeyword is null)
        {
            return false;
        }
        
        Writer.Write(Indent, "declare ");
        
        if (declarationModeKeyword.Length > 0)
        {
            Writer.Write(declarationModeKeyword);
            Writer.Write(' ');
        }

        if (symbol.ContainingType.TypeArguments[0] is INamedTypeSymbol namedTypeSymbol)
        {
            Writer.Write(Standardizer.CSharpTypeToManiaScriptType(namedTypeSymbol));
        }
        else
        {
            Writer.Write(Standardizer.CSharpTypeToManiaScriptType(symbol.ContainingType.TypeArguments[0].Name));
        }

        Writer.Write(' ');
        Writer.Write(Standardizer.StandardizeName(variable.Identifier.Text));
        Writer.Write(" for ");
        
        WriteSyntax(expression.ArgumentList.Arguments[0].Expression); 
        
        Writer.Write("; ");
        WriteLocationComment();
        Writer.WriteLine();

        return true;
    }
}