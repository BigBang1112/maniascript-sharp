using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class BlockBuilder : StatementBuilder<BlockSyntax>
{
    public override void Write(int ident, BlockSyntax statement,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        Writer.WriteLine("{");
        
        while (bodyBuilder.BlockLineQueue.Count > 0)
        {
            var line = bodyBuilder.BlockLineQueue.Dequeue();
            Writer.WriteLine(ident + 1, line);
        }
        
        foreach (var statementSyntax in statement.Statements)
        {
            WriteSyntax(ident + 1, statementSyntax, parameters, bodyBuilder);
        }
        
        Writer.WriteLine(ident, "}");
    }
}