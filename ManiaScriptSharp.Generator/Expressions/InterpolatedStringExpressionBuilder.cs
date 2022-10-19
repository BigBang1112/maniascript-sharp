using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace ManiaScriptSharp.Generator.Expressions;

public class InterpolatedStringExpressionBuilder : ExpressionBuilder<InterpolatedStringExpressionSyntax>
{
    public override void Write(int ident, InterpolatedStringExpressionSyntax expression,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        Writer.Write('"');
        
        foreach (var content in expression.Contents)
        {
            switch (content)
            {
                case InterpolatedStringTextSyntax text:
                    Writer.Write(text.TextToken.Text);
                    break;
                case InterpolationSyntax interpolation:
                    Writer.Write("\" ^ ");
                    WriteSyntax(ident, interpolation.Expression, parameters, bodyBuilder);
                    Writer.Write(" ^ \"");
                    break;
            }
        }

        Writer.Write('"');
    }
}
