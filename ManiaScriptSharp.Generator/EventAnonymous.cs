using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator;

public class EventAnonymous : EventFunction
{
    public ImmutableArray<ParameterSyntax> Parameters { get; init; }
    public BlockSyntax Block { get; init; }

    public EventAnonymous(ImmutableArray<ParameterSyntax> parameters, BlockSyntax block)
    {
        Parameters = parameters;
        Block = block;
    }

    public static bool TryParse(ExpressionSyntax expressionSyntax, out EventAnonymous value)
    {
        // AnonymousFunctionExpressionSyntax doesn't have ParameterList, that's why it's not used here
        
        switch (expressionSyntax)
        {
            case AnonymousMethodExpressionSyntax anonymousSyntax:
                value = new EventAnonymous(anonymousSyntax.ParameterList?.Parameters.ToImmutableArray()
                                           ?? ImmutableArray<ParameterSyntax>.Empty, anonymousSyntax.Block);
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
                
                value = new EventAnonymous(lambdaSyntax.ParameterList.Parameters.ToImmutableArray(), block);
                
                break;
            default:
                value = default!;
                return false;
        }

        return true;
    }
}