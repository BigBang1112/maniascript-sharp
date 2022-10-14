using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class BlockBuilder : StatementBuilder<BlockSyntax>
{
    public override void Write(int ident, BlockSyntax statement,
        ImmutableDictionary<string, ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        Writer.WriteLine("{");
        
        foreach (var statementSyntax in statement.Statements)
        {
            WriteSyntax(ident + 1, statementSyntax, parameters, bodyBuilder);
        }
        
        Writer.WriteLine(ident, "}");
    }
}