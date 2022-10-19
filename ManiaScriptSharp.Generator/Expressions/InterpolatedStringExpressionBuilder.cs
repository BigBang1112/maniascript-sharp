using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace ManiaScriptSharp.Generator.Expressions;

public class InterpolatedStringExpressionBuilder : ExpressionBuilder<InterpolatedStringExpressionSyntax>
{
    public override void Write(int ident, InterpolatedStringExpressionSyntax expression,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        var stringBefore = false;
        var interpolationBefore = false;

        foreach (var content in expression.Contents)
        {
            switch (content)
            {
                case InterpolatedStringTextSyntax text:
                    
                    if (interpolationBefore)
                    {
                        Writer.Write(" ^ ");
                        interpolationBefore = false;
                    }
                    
                    Writer.Write('"');
                    Writer.Write(text.TextToken.Text);
                    Writer.Write('"');
                    
                    stringBefore = true;
                    
                    break;
                case InterpolationSyntax interpolation:
                    
                    if (stringBefore)
                    {
                        Writer.Write(" ^ ");
                        stringBefore = false;
                    }
                    
                    WriteSyntax(ident, interpolation.Expression, parameters, bodyBuilder);

                    interpolationBefore = true;

                    break;
            }
        }
    }
}
