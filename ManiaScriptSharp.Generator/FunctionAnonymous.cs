using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator;

public class FunctionAnonymous : Function
{
    public ImmutableDictionary<string, ParameterSyntax> Parameters { get; init; }
    public BlockSyntax Block { get; init; }

    public FunctionAnonymous(ImmutableDictionary<string, ParameterSyntax> parameters, BlockSyntax block)
    {
        Parameters = parameters;
        Block = block;
    }

    public static bool TryParse(ExpressionSyntax expressionSyntax, out FunctionAnonymous value)
    {
        // AnonymousFunctionExpressionSyntax doesn't have ParameterList, that's why it's not used here
        
        switch (expressionSyntax)
        {
            case AnonymousMethodExpressionSyntax anonymousSyntax:
                value = new FunctionAnonymous(anonymousSyntax.ParameterList?.Parameters
                    .ToImmutableDictionary(x => x.Identifier.Text)
                                              ?? ImmutableDictionary<string, ParameterSyntax>.Empty, anonymousSyntax.Block);
                break;
            case ParenthesizedLambdaExpressionSyntax lambdaSyntax:
                
                BlockSyntax block;

                if (lambdaSyntax.Block is not null)
                {
                    block = lambdaSyntax.Block;
                }
                else if (lambdaSyntax.ExpressionBody is not null)
                {
                    block = SyntaxFactory.Block(SyntaxFactory.ExpressionStatement(lambdaSyntax.ExpressionBody));
                }
                else
                {
                    value = default!;
                    return false;
                }
                
                value = new FunctionAnonymous(lambdaSyntax.ParameterList.Parameters
                    .ToImmutableDictionary(x => x.Identifier.Text), block);
                
                break;
            default:
                value = default!;
                return false;
        }

        return true;
    }
}