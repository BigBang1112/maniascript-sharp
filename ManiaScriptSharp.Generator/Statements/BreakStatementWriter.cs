using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class BreakStatementWriter : StatementWriter<BreakStatementSyntax>
{
    public override void Write(BreakStatementSyntax statement)
    {
        Writer.Write(Indent, "break; ");
        WriteLocationComment();
        Writer.WriteLine();
    }
}