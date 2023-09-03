using ManiaScriptSharp.Generator.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class ReturnStatementWriter : StatementWriter<ReturnStatementSyntax>
{
    public override void Write(ReturnStatementSyntax statement)
    {
        if (LinqChecker.GenerateSupportedLinqMethod(this, statement.Expression, out var replacementCode))
        {
            Writer.Write(Indent, "return ");
            Writer.Write(replacementCode);
        }
        else
        {
            if (BodyBuilder.IsBuildingLoop)
            {
                Writer.Write(Indent, "continue");
            }
            else
            {
                Writer.Write(Indent, "return");
            }

            if (statement.Expression is not null)
            {
                Writer.Write(' ');
                WriteSyntax(statement.Expression);
            }
        }
        
        Writer.Write("; ");
        WriteLocationComment();
        Writer.WriteLine();
    }
}