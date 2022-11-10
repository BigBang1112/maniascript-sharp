using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class IfStatementWriter : StatementWriter<IfStatementSyntax>
{
    public override void Write(IfStatementSyntax statement)
    {
        Writer.Write(statement.Parent is ElseClauseSyntax ? 0 : Indent, "if (");
        WriteSyntax(statement.Condition);
        Writer.Write(") ");
        WriteLocationComment();
        Writer.Write(' ');
        
        WriteSyntax(statement.Statement);
        
        if (statement.Else is not null)
        {
            Writer.Write(Indent, "else ");
            WriteSyntax(statement.Else.Statement);
        }
    }
}