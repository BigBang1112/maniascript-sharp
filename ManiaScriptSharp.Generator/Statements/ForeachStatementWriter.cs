using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class ForeachStatementWriter : StatementWriter<ForEachStatementSyntax>
{
    public override void Write(ForEachStatementSyntax statement)
    {
        Writer.Write(Indent, "foreach (");
        Writer.Write(Standardizer.StandardizeName(statement.Identifier.Text));
        Writer.Write(" in ");
        WriteSyntax(statement.Expression);
        Writer.Write(") ");
        WriteLocationComment();
        Writer.Write(' ');
        WriteSyntax(statement.Statement); // Statement
    }
}