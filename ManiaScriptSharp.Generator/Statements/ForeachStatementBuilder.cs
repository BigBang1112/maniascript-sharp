using System.Collections.Immutable;
using ManiaScriptSharp.Generator.Expressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class ForeachStatementBuilder : StatementBuilder<ForEachStatementSyntax>
{
    public override void Write(int ident, ForEachStatementSyntax statement,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        Writer.Write(ident, "foreach (");
        Writer.Write(Standardizer.StandardizeName(statement.Identifier.Text));
        Writer.Write(" in ");
        ExpressionBuilder.WriteSyntax(ident, statement.Expression, parameters, bodyBuilder);
        Writer.Write(") ");
        WriteLocationComment(statement);
        Writer.Write(' ');
        WriteSyntax(ident, statement.Statement, parameters, bodyBuilder); // Statement
    }
}