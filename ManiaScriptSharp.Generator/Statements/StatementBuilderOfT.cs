using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public abstract class StatementBuilder<T> : StatementBuilder where T : StatementSyntax
{
    public abstract void Write(int ident, T statement, ImmutableArray<ParameterSyntax> parameters,
        ManiaScriptBodyBuilder bodyBuilder);

    public override void Write(int ident, StatementSyntax statement,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        base.Write(ident, statement, parameters, bodyBuilder);
        Write(ident, (T) statement, parameters, bodyBuilder);
    }

    protected void WriteLocationComment(T statement)
    {
        Writer.Write("/* ");
        
        var loc = statement.GetLocation();
        var line = loc.GetLineSpan();
        Writer.Write('[');
        Writer.Write(line.StartLinePosition.Line + 1);
        Writer.Write(',');
        Writer.Write(line.StartLinePosition.Character + 1);
        Writer.Write("] */");
    }
}