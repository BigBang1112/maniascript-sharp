﻿using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class InterpolatedStringExpressionWriter : ExpressionWriter<InterpolatedStringExpressionSyntax>
{
    public override void Write(InterpolatedStringExpressionSyntax expression)
    {
        var stringBefore = false;
        var interpolationBefore = false;

        foreach (var content in expression.Contents)
        {
            switch (content)
            {
                case InterpolatedStringTextSyntax text:
                    
                    if (stringBefore || interpolationBefore)
                    {
                        Writer.Write(" ^ ");
                        interpolationBefore = false;
                    }
                    
                    Writer.Write('"');
                    Writer.Write(text.TextToken.Text.Replace("{{", "{").Replace("}}", "}"));
                    Writer.Write('"');
                    
                    stringBefore = true;
                    
                    break;
                case InterpolationSyntax interpolation:
                    
                    if (stringBefore || interpolationBefore)
                    {
                        Writer.Write(" ^ ");
                        stringBefore = false;
                    }
                    
                    WriteSyntax(interpolation.Expression);

                    interpolationBefore = true;

                    break;
            }
        }
    }
}
