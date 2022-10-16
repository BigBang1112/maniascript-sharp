using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class LiteralExpressionBuilder : ExpressionBuilder<LiteralExpressionSyntax>
{
    public override void Write(int ident, LiteralExpressionSyntax expression,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
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