using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class BinaryExpressionBuilder : ExpressionBuilder<BinaryExpressionSyntax>
{
    public override void Write(int ident, BinaryExpressionSyntax expression,
        ImmutableDictionary<string, ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        WriteSyntax(ident, expression.Left, parameters, bodyBuilder);
        Writer.Write(" is ");
        WriteSyntax(ident, expression.Right, parameters, bodyBuilder);
    }
}