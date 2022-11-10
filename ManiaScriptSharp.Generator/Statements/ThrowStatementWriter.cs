using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class ThrowStatementWriter : StatementWriter<ThrowStatementSyntax>
{
    public override void Write(ThrowStatementSyntax statement)
    {
        Writer.Write(Indent, "assert(False, \"Exception was thrown: ");
        
        if (statement.Expression is ObjectCreationExpressionSyntax objectCreationExpressionSyntax)
        {
            WriteSyntax(objectCreationExpressionSyntax.Type);
        }

        Writer.Write(" in ");

        var loc = statement.GetLocation();
        var line = loc.GetLineSpan();
        var fileName = Path.GetFileName(line.Path);
        Writer.Write(fileName);
        Writer.Write(" [");
        Writer.Write(line.StartLinePosition.Line + 1);
        Writer.Write(',');
        Writer.Write(line.StartLinePosition.Character + 1);
        Writer.WriteLine("]\");");
    }
}