using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator;

public class FunctionAnonymous : Function
{
    public ImmutableArray<ParameterSyntax> Parameters { get; init; }
    public BlockSyntax Block { get; init; }
    public IMethodSymbol DelegateInvokeSymbol { get; init; }

    public FunctionAnonymous(ImmutableArray<ParameterSyntax> parameters, BlockSyntax block,
        IMethodSymbol delegateInvokeSymbol)
    {
        Parameters = parameters;
        Block = block;
        DelegateInvokeSymbol = delegateInvokeSymbol;
    }

    public static bool TryParse(ExpressionSyntax expressionSyntax, INamedTypeSymbol eventType, out FunctionAnonymous value)
    {
        // AnonymousFunctionExpressionSyntax doesn't have ParameterList, that's why it's not used here
        
        switch (expressionSyntax)
        {
            case AnonymousMethodExpressionSyntax anonymousSyntax:
                value = new FunctionAnonymous(anonymousSyntax.ParameterList?.Parameters.ToImmutableArray()
                    ?? ImmutableArray<ParameterSyntax>.Empty, anonymousSyntax.Block, eventType.DelegateInvokeMethod!);
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
                
                value = new FunctionAnonymous(lambdaSyntax.ParameterList.Parameters.ToImmutableArray(),
                    block, eventType.DelegateInvokeMethod!);
                
                break;
            default:
                value = default!;
                return false;
        }

        return true;
    }
}