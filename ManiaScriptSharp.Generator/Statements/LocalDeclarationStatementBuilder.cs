using System.Collections.Immutable;
using ManiaScriptSharp.Generator.Expressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class LocalDeclarationStatementBuilder : StatementBuilder<LocalDeclarationStatementSyntax>
{
    public override void Write(int ident, LocalDeclarationStatementSyntax statement,
        ImmutableDictionary<string, ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
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
}