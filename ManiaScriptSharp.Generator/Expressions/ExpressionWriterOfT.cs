using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public abstract class ExpressionWriter<T> : ExpressionWriter where T : ExpressionSyntax
{
    public abstract void Write(T expression);

    public override void Write()
    {
        if (Utils is null)
        {
            throw new InvalidOperationException();
        }

        Write((T)Utils.Expression);
    }
}