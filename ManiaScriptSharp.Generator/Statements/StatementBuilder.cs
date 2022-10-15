using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public abstract class StatementBuilder
{
    protected TextWriter Writer { get; private set; } = default!;
    
    public static void WriteSyntax(int ident, StatementSyntax statement,
        ImmutableDictionary<string, ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        StatementBuilder? builder = statement switch
        {
            ForEachStatementSyntax => new ForeachStatementBuilder(),
            BlockSyntax => new BlockBuilder(),
            ExpressionStatementSyntax => new ExpressionStatementBuilder(),
            ThrowStatementSyntax => new ThrowStatementBuilder(),
            IfStatementSyntax => new IfStatementBuilder(),
            _ => null
        };

        if (builder is null)
        {
            bodyBuilder.Writer.Write(ident, "/* ");
            bodyBuilder.Writer.Write(statement.GetType().Name);
            bodyBuilder.Writer.WriteLine(" */");
        }
        else
        {
            builder.Write(ident, statement, parameters, bodyBuilder);
        }
    }

    public virtual void Write(int ident, StatementSyntax statement,
        ImmutableDictionary<string, ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        Writer = bodyBuilder.Writer;
    }
}