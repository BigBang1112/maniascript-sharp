using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class AssignmentExpressionBuilder : ExpressionBuilder<AssignmentExpressionSyntax>
{
    public override void Write(int ident, AssignmentExpressionSyntax expression,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        WriteSyntax(ident, expression.Left, parameters, bodyBuilder);
        Writer.Write(" = ");
        WriteSyntax(ident, expression.Right, parameters, bodyBuilder);
    }
}