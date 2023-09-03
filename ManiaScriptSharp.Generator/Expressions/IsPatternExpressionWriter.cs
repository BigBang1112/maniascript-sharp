using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class IsPatternExpressionWriter : ExpressionWriter<IsPatternExpressionSyntax>
{
    public override void Write(IsPatternExpressionSyntax expression)
    {
        WritePattern(expression.Pattern,
            expression.Expression,
            new[] {expression.Expression},
            new Dictionary<ExpressionSyntax, TypeSyntax>(), unary: false);

        void WritePattern(PatternSyntax pattern, ExpressionSyntax currentExpression, ExpressionSyntax[] expressions,
            Dictionary<ExpressionSyntax, TypeSyntax> types, bool unary)
        {
            switch (pattern)
            {
                case RecursivePatternSyntax recursivePatternSyntax:
                    
                    if (recursivePatternSyntax.Type is not null)
                    {
                        WriteExpression(BodyBuilder, expressions, types);
                        Writer.Write(" is ");
                        WriteSyntax(recursivePatternSyntax.Type);
                        
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
                                    types, unary);
                                
                                continue;
                            }

                            if (hasFurtherSubpatterns)
                            {
                                Writer.Write('(');
                            }

                            WritePattern(subpattern.Pattern,
                                subpattern.NameColon.Expression,
                                expressions.Append(subpattern.NameColon.Expression).ToArray(),
                                types, unary);

                            if (hasFurtherSubpatterns)
                            {
                                Writer.Write(')');
                            }
                        }
                    }
            
                    if (recursivePatternSyntax.Designation is not null)
                    {
                        WriteDesignation(recursivePatternSyntax.Designation, expressions, types, unary);
                    }
                    break;
                case DeclarationPatternSyntax declarationPatternSyntax:
                    WriteExpression(BodyBuilder, expressions, types);
                    Writer.Write(" is ");
                    WriteSyntax(declarationPatternSyntax.Type);
                    types[currentExpression] = declarationPatternSyntax.Type;
                    WriteDesignation(declarationPatternSyntax.Designation, expressions, types, unary);
                    break;
                case ConstantPatternSyntax constantPatternSyntax:
                    WriteExpression(BodyBuilder, expressions, types);
                    Writer.Write(" == ");
                    WriteSyntax(constantPatternSyntax.Expression);
                    break;
                case RelationalPatternSyntax relationalPatternSyntax:
                    WriteExpression(BodyBuilder, expressions, types);
                    Writer.Write(' ');
                    Writer.Write(relationalPatternSyntax.OperatorToken.Text);
                    Writer.Write(' ');
                    WriteSyntax(relationalPatternSyntax.Expression);
                    break;
                case BinaryPatternSyntax binaryPatternSyntax:
                    WritePattern(binaryPatternSyntax.Left, currentExpression, expressions, types, unary);
                    
                    switch (binaryPatternSyntax.OperatorToken.Text)
                    {
                        case "and":
                            Writer.Write(" && ");
                            break;
                        case "or":
                            Writer.Write(" || ");
                            break;
                    }
                    
                    WritePattern(binaryPatternSyntax.Right, currentExpression, expressions, types, unary);
                    
                    break;
                case ParenthesizedPatternSyntax parenthesizedPatternSyntax:
                    Writer.Write('(');
                    WritePattern(parenthesizedPatternSyntax.Pattern, currentExpression, expressions, types, unary);
                    Writer.Write(')');
                    break;
                case UnaryPatternSyntax unaryPatternSyntax:
                    Writer.Write("!(");
                    WritePattern(unaryPatternSyntax.Pattern, currentExpression, expressions, types, !unary);
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
                IReadOnlyDictionary<ExpressionSyntax, TypeSyntax> typeSyntaxes, bool unary)
            {
                if (designation is SingleVariableDesignationSyntax singleVariableSyntax)
                {
                    var lineBuilder = new StringWriter();
                    lineBuilder.Write("declare ");
                    lineBuilder.Write(Standardizer.StandardizeName(singleVariableSyntax.Identifier.Text));

                    lineBuilder.Write(" = ");
                    
                    WriteExpression(
                        new ManiaScriptBodyBuilder(BodyBuilder.ScriptSymbol, BodyBuilder.SemanticModel, lineBuilder,
                            BodyBuilder.Head, BodyBuilder.Helper), expressionSyntaxes, typeSyntaxes);
                    
                    lineBuilder.Write(';');

                    if (unary)
                    {
                        BodyBuilder.AfterBlockLineQueue.Enqueue(lineBuilder.ToString());
                    }
                    else
                    {
                        BodyBuilder.BlockLineQueue.Enqueue(lineBuilder.ToString());
                    }
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

    private void WriteExpression(ManiaScriptBodyBuilder bodyBuilder, IReadOnlyList<ExpressionSyntax> expressions,
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
                    
            WriteSyntax(expressionSyntax, bodyBuilder);

            if (types.TryGetValue(expressionSyntax, out var typeSyntax))
            {
                bodyBuilder.Writer.Write(" as ");
                WriteSyntax(typeSyntax, bodyBuilder);
                bodyBuilder.Writer.Write(')');
            }

            if (i != expressions.Count - 1)
            {
                bodyBuilder.Writer.Write('.');
            }
        }
    }
}