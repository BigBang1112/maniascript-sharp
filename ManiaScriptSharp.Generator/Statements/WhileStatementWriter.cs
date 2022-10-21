using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class WhileStatementWriter : StatementWriter<WhileStatementSyntax>
{
    public override void Write(WhileStatementSyntax statement)
    {
        Writer.Write(Ident, "while (");
        WriteSyntax(statement.Condition);
        Writer.Write(") ");
        WriteLocationComment();
        Writer.Write(' ');
        WriteSyntax(statement.Statement);
    }
}