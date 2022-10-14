using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public abstract class StatementBuilder<T> : StatementBuilder where T : StatementSyntax
{
    public abstract void Write(int ident, T statement, ImmutableDictionary<string, ParameterSyntax> parameters,
        ManiaScriptBodyBuilder bodyBuilder);

    public override void Write(int ident, StatementSyntax statement,
        ImmutableDictionary<string, ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        base.Write(ident, statement, parameters, bodyBuilder);
        Write(ident, (T) statement, parameters, bodyBuilder);
    }
}