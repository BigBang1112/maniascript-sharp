using System.Collections.Immutable;
using ManiaScriptSharp.Generator.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class LocalDeclarationStatementBuilder : StatementBuilder<LocalDeclarationStatementSyntax>
{
    public override void Write(int ident, LocalDeclarationStatementSyntax statement,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        if (WriteDifferentDeclarationMode(ident, statement, parameters, bodyBuilder))
        {
            return;
        }

        foreach (var variable in statement.Declaration.Variables)
        {
            Writer.Write(ident, "declare ");

            if (!statement.Declaration.Type.IsVar)
            {
                ExpressionBuilder.WriteSyntax(ident, statement.Declaration.Type, parameters, bodyBuilder);
                Writer.Write(' ');
            }

            Writer.Write(Standardizer.StandardizeName(variable.Identifier.Text));

            if (variable.Initializer is not null)
            {
                Writer.Write(" = ");
                ExpressionBuilder.WriteSyntax(ident, variable.Initializer.Value, parameters, bodyBuilder);
            }

            Writer.Write("; ");
            WriteLocationComment(statement);
            Writer.WriteLine();
        }
    }

    private bool WriteDifferentDeclarationMode(int ident, LocalDeclarationStatementSyntax statement,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
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

        if (bodyBuilder.SemanticModel.GetSymbolInfo(expression).Symbol is not IMethodSymbol symbol
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
        
        Writer.Write(ident, "declare ");
        
        if (declarationModeKeyword.Length > 0)
        {
            Writer.Write(declarationModeKeyword);
            Writer.Write(' ');
        }
        
        Writer.Write(Standardizer.CSharpTypeToManiaScriptType(symbol.ContainingType.TypeArguments[0].Name));
        Writer.Write(' ');
        Writer.Write(Standardizer.StandardizeName(variable.Identifier.Text));
        Writer.Write(" for ");
        
        ExpressionBuilder.WriteSyntax(ident, expression.ArgumentList.Arguments[0].Expression, parameters, bodyBuilder); 
        
        Writer.Write("; ");
        WriteLocationComment(statement);
        Writer.WriteLine();

        return true;
    }
}