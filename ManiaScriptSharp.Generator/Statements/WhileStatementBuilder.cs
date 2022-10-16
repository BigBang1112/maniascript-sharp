using System.Collections.Immutable;
using ManiaScriptSharp.Generator.Expressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class WhileStatementBuilder : StatementBuilder<WhileStatementSyntax>
{
    public override void Write(int ident, WhileStatementSyntax statement,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        Writer.Write(ident, "while (");
        ExpressionBuilder.WriteSyntax(ident, statement.Condition, parameters, bodyBuilder);
        Writer.Write(") ");
        WriteSyntax(ident, statement.Statement, parameters, bodyBuilder);
    }
}