using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class ReturnStatementWriter : StatementWriter<ReturnStatementSyntax>
{
    public override void Write(ReturnStatementSyntax statement)
    {
        Writer.Write(Ident, "return");
        
        if (statement.Expression is not null)
        {
            Writer.Write(' ');
            WriteSyntax(statement.Expression);
        }
        
        Writer.Write("; ");
        WriteLocationComment();
        Writer.WriteLine();
    }
}