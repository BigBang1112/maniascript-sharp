using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class ThisExpressionWriter : ExpressionWriter<ThisExpressionSyntax>
{
    public override void Write(ThisExpressionSyntax expression)
    {
        Writer.Write("This");
    }
}