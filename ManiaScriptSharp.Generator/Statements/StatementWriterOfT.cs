using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public abstract class StatementWriter<T> : StatementWriter where T : StatementSyntax
{
    public abstract void Write(T statement);

    public override void Write()
    {
        if (Utils is null)
        {
            throw new InvalidOperationException();
        }

        Write((T)Utils.Statement);
    }
}