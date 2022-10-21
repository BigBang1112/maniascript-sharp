using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class LiteralExpressionWriter : ExpressionWriter<LiteralExpressionSyntax>
{
    public override void Write(LiteralExpressionSyntax expression)
    {
        switch (expression.Token.Value)
        {
            case null:
                Writer.Write("Null");
                break;
            case string str:
                Writer.Write($"\"{str}\"");
                break;
            default:
                Writer.Write(expression.Token.Value);
                break;
        }
    }
}