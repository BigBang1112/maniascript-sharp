using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public abstract class ExpressionBuilder<T> : ExpressionBuilder where T : ExpressionSyntax
{
    public abstract void Write(int ident, T expression, ImmutableArray<ParameterSyntax> parameters,
        ManiaScriptBodyBuilder bodyBuilder);

    public override void Write(int ident, ExpressionSyntax expression,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        base.Write(ident, expression, parameters, bodyBuilder);
        Write(ident, (T) expression, parameters, bodyBuilder);
    }
}