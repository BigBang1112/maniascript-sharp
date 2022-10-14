using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class ForeachStatementBuilder : StatementBuilder<ForEachStatementSyntax>
{
    public override void Write(int ident, ForEachStatementSyntax statement,
        ImmutableDictionary<string, ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        Writer.Write(ident, "foreach (");
        Writer.Write(Standardizer.StandardizeName(statement.Identifier.Text));
        Writer.Write(" in ");
        Writer.Write(Standardizer.StandardizeName(statement.Expression.ToString()));
        Writer.Write(") ");
        WriteSyntax(ident, statement.Statement, parameters, bodyBuilder); // Statement
    }
}