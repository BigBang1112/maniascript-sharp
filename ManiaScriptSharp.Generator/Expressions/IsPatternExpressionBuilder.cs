using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ManiaScriptSharp.Generator.Expressions;

public class IsPatternExpressionBuilder : ExpressionBuilder<IsPatternExpressionSyntax>
{
    public override void Write(int ident, IsPatternExpressionSyntax expression, ImmutableDictionary<string, ParameterSyntax> parameters,
        ManiaScriptBodyBuilder bodyBuilder)
    {
        WriteSyntax(ident, expression.Expression, parameters, bodyBuilder);
        
        if (expression.Pattern is DeclarationPatternSyntax declarationPatternSyntax)
        {
            Writer.Write(" is ");
            WriteSyntax(ident, declarationPatternSyntax.Type, parameters, bodyBuilder);

            WriteDesignation(declarationPatternSyntax.Designation, declarationPatternSyntax.Type);
        }
        else if (expression.Pattern is RecursivePatternSyntax recursivePatternSyntax)
        {
            var typeCheckCompleted = false;
            
            if (recursivePatternSyntax.Type is not null)
            {
                Writer.Write(" is ");
                WriteSyntax(ident, recursivePatternSyntax.Type, parameters, bodyBuilder);
                typeCheckCompleted = true;
            }

            if (recursivePatternSyntax.PropertyPatternClause is not null)
            {
                foreach (var subpattern in recursivePatternSyntax.PropertyPatternClause.Subpatterns)
                {
                    if (typeCheckCompleted)
                    {
                        Writer.Write(" && ");
                    }

                    if (subpattern.NameColon is not null)
                    {
                        if (typeCheckCompleted)
                        {
                            WriteSyntax(ident, expression.Expression, parameters, bodyBuilder);
                        }

                        Writer.Write('.');
                        
                        WriteSyntax(ident, subpattern.NameColon.Expression, parameters, bodyBuilder);

                        Writer.Write(' ');
                        
                        switch (subpattern.Pattern)
                        {
                            case ConstantPatternSyntax constantPatternSyntax:
                                Writer.Write("== ");
                                WriteSyntax(ident, constantPatternSyntax.Expression, parameters, bodyBuilder);
                                break;
                            case RelationalPatternSyntax relationalPatternSyntax:
                                Writer.Write(relationalPatternSyntax.OperatorToken.Text);
                                Writer.Write(' ');
                                WriteSyntax(ident, relationalPatternSyntax.Expression, parameters, bodyBuilder);
                                break;
                            case BinaryPatternSyntax binaryPatternSyntax:

                                break;
                            default:
                                Writer.Write("/* ");
                                Writer.Write(subpattern.Pattern.GetType().Name);
                                Writer.Write(" */");
                                break;
                        }
                    }
                    
                    typeCheckCompleted = true;
                }
            }
            
            if (recursivePatternSyntax.Designation is not null)
            {
                WriteDesignation(recursivePatternSyntax.Designation, recursivePatternSyntax.Type);
            }
        }
        else
        {
            Writer.Write("/*");
            Writer.Write(expression.Pattern.GetType().Name);
            Writer.Write("*/");
        }

        void WriteDesignation(VariableDesignationSyntax designation, ExpressionSyntax? type)
        {
            if (designation is SingleVariableDesignationSyntax singleVariableSyntax)
            {
                var lineBuilder = new StringWriter();
                lineBuilder.Write("declare ");
                lineBuilder.Write(Standardizer.StandardizeName(singleVariableSyntax.Identifier.Text));

                lineBuilder.Write(" = ");
                
                if (type is not null)
                {
                    lineBuilder.Write('(');
                }

                WriteSyntax(ident, expression.Expression, parameters,
                    new ManiaScriptBodyBuilder(bodyBuilder.ScriptSymbol, bodyBuilder.SemanticModel, lineBuilder,
                        bodyBuilder.Head, bodyBuilder.Helper));

                if (type is not null)
                {
                    lineBuilder.Write(" as ");

                    WriteSyntax(ident, type, parameters,
                        new ManiaScriptBodyBuilder(bodyBuilder.ScriptSymbol, bodyBuilder.SemanticModel, lineBuilder,
                            bodyBuilder.Head, bodyBuilder.Helper));

                    lineBuilder.Write(')');
                }
                
                lineBuilder.Write(';');

                bodyBuilder.BlockLineQueue.Enqueue(lineBuilder.ToString());
            }
            else
            {
                Writer.Write("/*");
                Writer.Write(designation.GetType().Name);
                Writer.Write("*/");
            }
        }
    }
}