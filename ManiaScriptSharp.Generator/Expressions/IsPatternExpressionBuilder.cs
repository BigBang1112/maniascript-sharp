using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ManiaScriptSharp.Generator.Expressions;

public class IsPatternExpressionBuilder : ExpressionBuilder<IsPatternExpressionSyntax>
{
    public override void Write(int ident, IsPatternExpressionSyntax expression,
        ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        WritePattern(expression.Pattern,
            expression.Expression,
            new[] {expression.Expression},
            new Dictionary<ExpressionSyntax, TypeSyntax>());

        void WritePattern(PatternSyntax pattern, ExpressionSyntax currentExpression, ExpressionSyntax[] expressions,
            Dictionary<ExpressionSyntax, TypeSyntax> types)
        {
            switch (pattern)
            {
                case RecursivePatternSyntax recursivePatternSyntax:
                    
                    if (recursivePatternSyntax.Type is not null)
                    {
                        WriteExpression(ident, parameters, bodyBuilder, expressions, types);
                        Writer.Write(" is ");
                        WriteSyntax(ident, recursivePatternSyntax.Type, parameters, bodyBuilder);
                        
                        types[currentExpression] = recursivePatternSyntax.Type;
                    }
                    
                    if (recursivePatternSyntax.PropertyPatternClause is not null)
                    {
                        for (var i = 0; i < recursivePatternSyntax.PropertyPatternClause.Subpatterns.Count; i++)
                        {
                            var subpattern = recursivePatternSyntax.PropertyPatternClause.Subpatterns[i];

                            var hasFurtherSubpatterns = subpattern.Pattern is not RecursivePatternSyntax
                            {
                                PropertyPatternClause.Subpatterns.Count: 0
                            };

                            if (hasFurtherSubpatterns)
                            {
                                if (recursivePatternSyntax.Type is not null || i != 0)
                                {
                                    Writer.Write(" && ");
                                }
                            }

                            if (subpattern.NameColon is null)
                            {
                                if (subpattern.ExpressionColon is null)
                                {
                                    Writer.Write("/* NameColon and ExpressionColon are null */");
                                    continue;
                                }
                                
                                WritePattern(subpattern.Pattern,
                                    subpattern.ExpressionColon.Expression,
                                    expressions.Append(subpattern.ExpressionColon.Expression).ToArray(),
                                    types);
                                
                                continue;
                            }

                            if (hasFurtherSubpatterns)
                            {
                                Writer.Write('(');
                            }

                            WritePattern(subpattern.Pattern,
                                subpattern.NameColon.Expression,
                                expressions.Append(subpattern.NameColon.Expression).ToArray(),
                                types);

                            if (hasFurtherSubpatterns)
                            {
                                Writer.Write(')');
                            }
                        }
                    }
            
                    if (recursivePatternSyntax.Designation is not null)
                    {
                        WriteDesignation(recursivePatternSyntax.Designation, expressions, types);
                    }
                    break;
                case DeclarationPatternSyntax declarationPatternSyntax:
                    WriteExpression(ident, parameters, bodyBuilder, expressions, types);
                    Writer.Write(" is ");
                    WriteSyntax(ident, declarationPatternSyntax.Type, parameters, bodyBuilder);
                    types[currentExpression] = declarationPatternSyntax.Type;
                    WriteDesignation(declarationPatternSyntax.Designation, expressions, types);
                    break;
                case ConstantPatternSyntax constantPatternSyntax:
                    WriteExpression(ident, parameters, bodyBuilder, expressions, types);
                    Writer.Write(" == ");
                    WriteSyntax(ident, constantPatternSyntax.Expression, parameters, bodyBuilder);
                    break;
                case RelationalPatternSyntax relationalPatternSyntax:
                    WriteExpression(ident, parameters, bodyBuilder, expressions, types);
                    Writer.Write(' ');
                    Writer.Write(relationalPatternSyntax.OperatorToken.Text);
                    Writer.Write(' ');
                    WriteSyntax(ident, relationalPatternSyntax.Expression, parameters, bodyBuilder);
                    break;
                case BinaryPatternSyntax binaryPatternSyntax:
                    WritePattern(binaryPatternSyntax.Left, currentExpression, expressions, types);
                    
                    switch (binaryPatternSyntax.OperatorToken.Text)
                    {
                        case "and":
                            Writer.Write(" && ");
                            break;
                        case "or":
                            Writer.Write(" || ");
                            break;
                    }
                    
                    WritePattern(binaryPatternSyntax.Right, currentExpression, expressions, types);
                    
                    break;
                case ParenthesizedPatternSyntax parenthesizedPatternSyntax:
                    Writer.Write('(');
                    WritePattern(parenthesizedPatternSyntax.Pattern, currentExpression, expressions, types);
                    Writer.Write(')');
                    break;
                case UnaryPatternSyntax unaryPatternSyntax:
                    Writer.Write("!(");
                    WritePattern(unaryPatternSyntax.Pattern, currentExpression, expressions, types);
                    Writer.Write(')');
                    break;
                default:
                    Writer.Write("/* ");
                    Writer.Write(pattern.GetType().Name);
                    Writer.Write(" */");
                    break;
            }

            void WriteDesignation(VariableDesignationSyntax designation,
                IReadOnlyList<ExpressionSyntax> expressionSyntaxes,
                IReadOnlyDictionary<ExpressionSyntax, TypeSyntax> typeSyntaxes)
            {
                if (designation is SingleVariableDesignationSyntax singleVariableSyntax)
                {
                    var lineBuilder = new StringWriter();
                    lineBuilder.Write("declare ");
                    lineBuilder.Write(Standardizer.StandardizeName(singleVariableSyntax.Identifier.Text));

                    lineBuilder.Write(" = ");
                    
                    WriteExpression(ident, parameters,
                        new ManiaScriptBodyBuilder(bodyBuilder.ScriptSymbol, bodyBuilder.SemanticModel, lineBuilder,
                            bodyBuilder.Head, bodyBuilder.Helper), expressionSyntaxes, typeSyntaxes);
                    
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

    private void WriteExpression(int ident, ImmutableArray<ParameterSyntax> parameters,
        ManiaScriptBodyBuilder bodyBuilder, IReadOnlyList<ExpressionSyntax> expressions,
        IReadOnlyDictionary<ExpressionSyntax, TypeSyntax> types)
    {
        foreach (var expression in expressions)
        {
            if (types.ContainsKey(expression))
            {
                bodyBuilder.Writer.Write('(');
            }
        }

        for (var i = 0; i < expressions.Count; i++)
        {
            var expressionSyntax = expressions[i];
                    
            WriteSyntax(ident, expressionSyntax, parameters, bodyBuilder);

            if (types.TryGetValue(expressionSyntax, out var typeSyntax))
            {
                bodyBuilder.Writer.Write(" as ");
                WriteSyntax(ident, typeSyntax, parameters, bodyBuilder);
                bodyBuilder.Writer.Write(')');
            }

            if (i != expressions.Count - 1)
            {
                bodyBuilder.Writer.Write('.');
            }
        }
    }
}