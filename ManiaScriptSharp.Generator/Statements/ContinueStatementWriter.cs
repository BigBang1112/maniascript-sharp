using ManiaScriptSharp.Generator.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class ContinueStatementWriter : StatementWriter<ContinueStatementSyntax>
{
    public override void Write(ContinueStatementSyntax statement)
    {
        Writer.Write(Indent, "continue; ");
        WriteLocationComment();
        Writer.WriteLine();
    }
}