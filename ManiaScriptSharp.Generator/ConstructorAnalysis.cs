using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator;

public class ConstructorAnalysis
{
    public ImmutableDictionary<IdentifierNameSyntax, EventFunction> EventFunctions { get; init; }
    
    public ConstructorAnalysis(ImmutableDictionary<IdentifierNameSyntax, EventFunction> eventFunctions)
    {
        EventFunctions = eventFunctions;
    }
    
    public static ConstructorAnalysis Analyze(IMethodSymbol constructorSymbol, SemanticModel semanticModel,
        GeneratorHelper helper)
    {
        var eventMethodDictBuilder = ImmutableDictionary.CreateBuilder<IdentifierNameSyntax, EventFunction>();

        foreach (var eventSubscriptionSyntax in GetSubscribedEvents(constructorSymbol))
        {
            if (semanticModel.GetSymbolInfo(eventSubscriptionSyntax.Left).Symbol is not IEventSymbol eventSymbol)
            {
                continue;
            }

            var eventAtt = eventSymbol.Type
                .GetAttributes()
                .FirstOrDefault(x => x.AttributeClass?.Name == NameConsts.ManiaScriptEventAttribute);

            if (eventAtt is null)
            {
                var externalEventAtt = eventSymbol.GetAttributes()
                    .FirstOrDefault(x => x.AttributeClass?.Name == "ManiaScriptExternalEventAttribute");

                if (externalEventAtt is null)
                {
                    continue;
                }
            }

            var leftIdentifierSyntax = eventSubscriptionSyntax.Left switch
            {
                MemberAccessExpressionSyntax m => m.Name as IdentifierNameSyntax,
                IdentifierNameSyntax i => i,
                _ => null
            };
            
            if (leftIdentifierSyntax is null)
            {
                continue;
            }

            if (eventSubscriptionSyntax.Right is IdentifierNameSyntax rightIdentifierSyntax)
            {
                if (semanticModel.GetSymbolInfo(rightIdentifierSyntax).Symbol is not IMethodSymbol methodSymbol)
                {
                    continue;
                }
                
                eventMethodDictBuilder.Add(leftIdentifierSyntax, new EventIdentifier(methodSymbol));
            }
            else if (EventAnonymous.TryParse(eventSubscriptionSyntax.Right, out var eventAnonymous))
            {
                eventMethodDictBuilder.Add(leftIdentifierSyntax, eventAnonymous);
            }
        }

        return new(
            eventMethodDictBuilder.ToImmutable());
    }
    
    private static IEnumerable<AssignmentExpressionSyntax> GetSubscribedEvents(IMethodSymbol constructorSymbol)
    {
        if (constructorSymbol.DeclaringSyntaxReferences.Length == 0)
        {
            yield break;
        }

        var constructorSyntax =
            (ConstructorDeclarationSyntax) constructorSymbol.DeclaringSyntaxReferences[0].GetSyntax();

        // Can be null if expression statement
        if (constructorSyntax.Body is null)
        {
            yield break;
        }

        foreach (var statement in constructorSyntax.Body.Statements)
        {
            if (statement is ExpressionStatementSyntax
                {
                    Expression: AssignmentExpressionSyntax {OperatorToken.Text: "+="} assignmentExpressionSyntax
                })
            {
                yield return assignmentExpressionSyntax;
            }
        }
    }
}