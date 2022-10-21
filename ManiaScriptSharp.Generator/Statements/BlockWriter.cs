using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class BlockWriter : StatementWriter<BlockSyntax>
{
    public override void Write(BlockSyntax statement)
    {
        Writer.WriteLine("{");
        
        while (BodyBuilder.BlockLineQueue.Count > 0)
        {
            var line = BodyBuilder.BlockLineQueue.Dequeue();
            Writer.WriteLine(Ident + 1, line);
        }
        
        foreach (var statementSyntax in statement.Statements)
        {
            WriteSyntax(statementSyntax, Ident + 1);
        }
        
        Writer.WriteLine(Ident, "}");
    }
}