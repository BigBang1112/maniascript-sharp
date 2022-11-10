using ManiaScriptSharp.Generator.Statements;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace ManiaScriptSharp.Generator;

public record StatementWriterUtils(int Indent, StatementSyntax Statement, ImmutableArray<ParameterSyntax> Parameters, ManiaScriptBodyBuilder BodyBuilder)
    : WriterUtils(Indent, Parameters, BodyBuilder)
{
    public TextWriter Writer => BodyBuilder.Writer;

    public StatementWriter? GetStatementWriter() => Statement switch
    {
        ForEachStatementSyntax => new ForeachStatementWriter(),
        BlockSyntax => new BlockWriter(),
        ExpressionStatementSyntax => new ExpressionStatementWriter(),
        ThrowStatementSyntax => new ThrowStatementWriter(),
        IfStatementSyntax => new IfStatementWriter(),
        LocalDeclarationStatementSyntax => new LocalDeclarationStatementWriter(),
        ForStatementSyntax => new ForStatementWriter(),
        WhileStatementSyntax => new WhileStatementWriter(),
        ReturnStatementSyntax => new ReturnStatementWriter(),
        _ => null
    };
}