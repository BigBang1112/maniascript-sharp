using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator;

public class EventForeachBuilder
{
    private ManiaScriptBodyBuilder BodyBuilder { get; }

    private ITypeSymbol ScriptSymbol => BodyBuilder.ScriptSymbol;
    private TextWriter Writer => BodyBuilder.Writer;

    public EventForeachBuilder(ManiaScriptBodyBuilder bodyBuilder)
    {
        BodyBuilder = bodyBuilder;
    }

    public void WriteEventForeach(int ident, ImmutableArray<IMethodSymbol> functions,
        ConstructorAnalysis constructorAnalysis)
    {
        var overridenEventFunctions = GetOverridenEventFunctions(functions).ToImmutableArray();

        if (overridenEventFunctions.IsEmpty && constructorAnalysis.EventFunctions.IsEmpty)
        {
            return;
        }

        var delegateDict = GetAllDelegates(ScriptSymbol).ToImmutableDictionary(x => x.Name);
        var eventListSymbolDict = new Dictionary<string, IPropertySymbol>();
        var eventListDelegates = new HashSet<(string, INamedTypeSymbol)>();
        var delegateEventFunctions = new List<(INamedTypeSymbol, EventFunction)>();
        var externalEvents = new List<(INamedTypeSymbol, string, string)>();
        var externalDelegateEventFunctions = new List<(string, EventFunction)>();

        foreach (var (identifier, function) in constructorAnalysis.EventFunctions)
        {
            var eventPreMembers = "";

            if (identifier.Parent is MemberAccessExpressionSyntax memberAccessSyntax)
            {
                while (memberAccessSyntax.Expression is MemberAccessExpressionSyntax memberAccessSyntax2)
                {
                    memberAccessSyntax = memberAccessSyntax2;

                    eventPreMembers = memberAccessSyntax.Name.Identifier + "." + eventPreMembers;
                }

                if (memberAccessSyntax.Expression is IdentifierNameSyntax identifierNameSyntax)
                {
                    eventPreMembers = identifierNameSyntax.Identifier + "." + eventPreMembers;
                }
            }

            if (BodyBuilder.SemanticModel.GetSymbolInfo(identifier).Symbol is not IEventSymbol eventSymbol)
            {
                continue;
            }

            var externalEventAtt = eventSymbol.GetAttributes()
                .FirstOrDefault(x => x.AttributeClass?.Name == NameConsts.ManiaScriptExternalEventAttribute);

            if (externalEventAtt is null)
            {
                if (eventSymbol.Type is not INamedTypeSymbol delegateSymbol)
                {
                    continue;
                }

                var eventAtt = delegateSymbol.GetAttributes()
                    .FirstOrDefault(x => x.AttributeClass?.Name == NameConsts.ManiaScriptEventAttribute);

                var eventListName = eventAtt?.ConstructorArguments[0].Value?.ToString();

                if (eventListName is null)
                {
                    continue;
                }

                var fullEventList = eventPreMembers + eventListName;

                eventListDelegates.Add((fullEventList, delegateSymbol));
                delegateEventFunctions.Add((delegateSymbol, function));

                if (eventListSymbolDict.ContainsKey(fullEventList))
                {
                    continue;
                }

                var eventListSymbol = (delegateSymbol.ContainingSymbol as ITypeSymbol)?.GetMembers(eventListName)
                    .OfType<IPropertySymbol>()
                    .FirstOrDefault();

                if (eventListSymbol is null)
                {
                    continue;
                }

                eventListSymbolDict.Add(fullEventList, eventListSymbol);
            }
            else
            {
                // External event

                // (1) Verify 'eventContext'
                // External events shouldn't be applied external PendingEvents for now

                var eventContext = externalEventAtt.ConstructorArguments[0].Value?.ToString();
                var eventList = externalEventAtt.ConstructorArguments[1].Value?.ToString();
                var eventKind = externalEventAtt.ConstructorArguments[2].Value?.ToString();
                var identifierInEvent = externalEventAtt.ConstructorArguments[3].Value?.ToString();

                if (eventContext is null || eventList is null || eventKind is null || identifierInEvent is null)
                {
                    continue;
                }

                if (GenericExtensions.Flatten(ScriptSymbol, x => x.BaseType).All(x => x.Name != eventContext))
                {
                    continue;
                }

                // (2) Verify 'eventList'
                if (!eventListSymbolDict.TryGetValue(eventList, out var eventListSymbol))
                {
                    eventListSymbol = GenericExtensions.Flatten(ScriptSymbol, x => x.BaseType)
                        .SelectMany(x => x.GetMembers(eventList).OfType<IPropertySymbol>())
                        .FirstOrDefault();

                    if (eventListSymbol is null)
                    {
                        continue;
                    }

                    eventListSymbolDict.Add(eventList, eventListSymbol);
                }

                // (3) Verify 'eventKind'
                var delegateSymbol = GenericExtensions.Flatten(ScriptSymbol, x => x.BaseType)
                    .Select(x => x.GetTypeMembers(eventKind + "EventHandler").FirstOrDefault())
                    .FirstOrDefault(x => x is not null);
                
                if (delegateSymbol is null)
                {
                    continue;
                }

                // Bound manialink control for example
                // Selector will make this vary in the future
                var objectIdentifierName = ((identifier.Parent as MemberAccessExpressionSyntax)?
                    .Expression as IdentifierNameSyntax)?.Identifier.Value?.ToString();

                if (objectIdentifierName is null)
                {
                    continue;
                }

                eventListDelegates.Add((eventList, delegateSymbol));
                externalDelegateEventFunctions.Add((objectIdentifierName, function));

                externalEvents.Add((delegateSymbol, identifierInEvent, objectIdentifierName));
            }
        }

        foreach (var (eventFunction, delegateName) in overridenEventFunctions)
        {
            var delegateSymbol = delegateDict[delegateName];

            var eventAtt = delegateSymbol.GetAttributes()
                .FirstOrDefault(x => x.AttributeClass?.Name == NameConsts.ManiaScriptEventAttribute);

            var eventListName = eventAtt?.ConstructorArguments[0].Value?.ToString();

            if (eventListName is null)
            {
                continue;
            }

            eventListDelegates.Add((eventListName, delegateSymbol));
            delegateEventFunctions.Add((delegateSymbol, new EventIdentifier(eventFunction)));

            if (eventListSymbolDict.ContainsKey(eventListName))
            {
                continue;
            }

            var eventListSymbol = (delegateSymbol.ContainingSymbol as ITypeSymbol)?.GetMembers(eventListName)
                .OfType<IPropertySymbol>()
                .FirstOrDefault();

            if (eventListSymbol is null)
            {
                continue;
            }

            eventListSymbolDict.Add(eventListName, eventListSymbol);
        }

        var delegateLookup = eventListDelegates.ToLookup(x => x.Item1, x => x.Item2);
        var eventFunctionLookup =
            delegateEventFunctions.ToLookup(x => x.Item1, x => x.Item2, SymbolEqualityComparer.Default);
        var externalEventFunctionLookup =
            externalDelegateEventFunctions.ToLookup(x => x.Item1, x => x.Item2);
        var externalEventLookup = externalEvents.ToLookup(x => x.Item1, x => (x.Item2, x.Item3), SymbolEqualityComparer.Default);
        
        foreach (var pair in eventListSymbolDict)
        {
            var usedEventListName = pair.Key;
            var usedEventListSymbol = pair.Value;
            var usedEventTypeSymbol = (usedEventListSymbol.Type as INamedTypeSymbol)?.TypeArguments[0];

            if (usedEventTypeSymbol is null)
            {
                continue;
            }

            var eventEnumName = GetEventEnumName(usedEventTypeSymbol);

            Writer.Write(ident, "foreach (Event in ");
            Writer.Write(usedEventListName);
            Writer.WriteLine(") {");

            var generalEventDelegate = default(INamedTypeSymbol);
            var hasSpecificEventDelegate = false;

            foreach (var eventDelegate in delegateLookup[usedEventListName])
            {
                if (IsGeneralEvent(eventDelegate))
                {
                    generalEventDelegate = eventDelegate;
                }
                else
                {
                    hasSpecificEventDelegate = true;
                }

                if (generalEventDelegate is not null && hasSpecificEventDelegate)
                {
                    break;
                }
            }

            if (generalEventDelegate is not null)
            {
                WriteEventContents(ident + 1, eventFunctionLookup!, generalEventDelegate, isGeneralEvent: true);
            }

            if (delegateLookup[usedEventListName].Any(x => !IsGeneralEvent(x)))
            {
                Writer.WriteLine(ident + 1, "switch (Event.Type) {");

                foreach (var delegateSymbol in delegateLookup[usedEventListName])
                {
                    if (delegateSymbol.Equals(generalEventDelegate, SymbolEqualityComparer.Default))
                    {
                        continue;
                    }

                    // make it into dictionary above
                    var actualEventName = delegateSymbol.GetAttributes()
                        .FirstOrDefault(x => x.AttributeClass?.Name == NameConsts.ActualEventNameAttribute)?
                        .ConstructorArguments[0].Value as string;

                    var eventName = actualEventName ?? delegateSymbol.Name
                        .Substring(0, delegateSymbol.Name.Length - 12);

                    Writer.Write(ident + 2, "case ");
                    Writer.Write(usedEventTypeSymbol.Name);
                    Writer.Write("::");
                    Writer.Write(eventEnumName);
                    Writer.Write("::");
                    Writer.Write(eventName);
                    Writer.WriteLine(": {");

                    // external event handle
                    if (externalEventLookup.Contains(delegateSymbol))
                    {
                        var externalEvent = externalEventLookup[delegateSymbol];
                        
                        foreach (var objectIdentifiers in externalEventLookup[delegateSymbol].ToLookup(x => x.Item1, x => x.Item2))
                        {
                            Writer.Write(ident + 3, "switch (Event.");
                            Writer.Write(objectIdentifiers.Key);
                            Writer.WriteLine(") {");
                            
                            foreach (var objectIdentifierName in objectIdentifiers.Distinct())
                            {
                                Writer.Write(ident + 4, "case ");
                                Writer.Write(objectIdentifierName);
                                Writer.WriteLine(": {");

                                foreach (var eventFunction in externalEventFunctionLookup[objectIdentifierName])
                                {
                                    WriteEventContents(ident + 5, eventFunction,
                                        ImmutableArray<IParameterSymbol>.Empty, isGeneralEvent: false);
                                }
                                
                                Writer.WriteLine(ident + 4, "}");
                            }
                            
                            Writer.WriteLine(ident + 3, "}");
                        }
                    }
                    
                    WriteEventContents(ident + 3, eventFunctionLookup!, delegateSymbol, isGeneralEvent: false);

                    Writer.WriteLine(ident + 2, "}");
                }

                Writer.WriteLine(ident + 1, "}");
            }

            Writer.WriteLine(ident, "}");
        }
    }

    private void WriteEventContents(int ident, ILookup<ISymbol, EventFunction> eventFunctionLookup,
        INamedTypeSymbol eventDelegate,
        bool isGeneralEvent)
    {
        var delegateMethod = eventDelegate.DelegateInvokeMethod!;

        foreach (var eventFunction in eventFunctionLookup[eventDelegate])
        {
            WriteEventContents(ident, eventFunction, delegateMethod.Parameters, isGeneralEvent);
        }
    }

    private void WriteEventContents(int ident, EventFunction eventFunction, ImmutableArray<IParameterSymbol> parameters,
        bool isGeneralEvent)
    {
        switch (eventFunction)
        {
            case EventIdentifier eventIdentifier:
                Writer.Write(ident, eventIdentifier.Method.Name);

                if (isGeneralEvent)
                {
                    Writer.WriteLine("(Event);");
                    return;
                }

                Writer.Write('(');

                for (var i = 0; i < parameters.Length; i++)
                {
                    var parameter = parameters[i];

                    if (i != 0)
                    {
                        Writer.Write(", ");
                    }

                    Writer.Write("Event.");

                    var actualNameAtt = parameter.GetAttributes()
                        .FirstOrDefault(x => x.AttributeClass?.Name == NameConsts.ActualNameAttribute);

                    if (actualNameAtt is not null)
                    {
                        Writer.Write(actualNameAtt.ConstructorArguments[0].Value);
                        continue;
                    }

                    if (parameter.Name.Length <= 0)
                    {
                        continue;
                    }

                    if (char.IsLower(parameter.Name[0]))
                    {
                        var charArray = parameter.Name.ToCharArray();
                        charArray[0] = char.ToUpper(charArray[0]);
                        Writer.Write(new string(charArray));
                    }
                    else
                    {
                        Writer.Write(parameter.Name);
                    }
                }

                Writer.WriteLine(");");
                break;
            case EventAnonymous eventAnonymous:
                Writer.WriteLine(ident, "// anonymous contents");
                break;
        }
    }

    private static bool IsGeneralEvent(INamedTypeSymbol delegateSymbol)
    {
        var delegateMethod = delegateSymbol.DelegateInvokeMethod!;
        var isGeneralEvent = delegateMethod.Parameters.Length == 1 &&
                             delegateMethod.Parameters[0].Name == "e";
        return isGeneralEvent;
    }

    private static IEnumerable<INamedTypeSymbol> GetAllDelegates(ITypeSymbol? symbol)
    {
        while (symbol is not null)
        {
            foreach (var member in symbol.GetTypeMembers().Where(member => member.DelegateInvokeMethod is not null))
            {
                yield return member;
            }

            symbol = symbol.BaseType;
        }
    }

    private static IEnumerable<(IMethodSymbol, string)> GetOverridenEventFunctions(
        ImmutableArray<IMethodSymbol> functions)
    {
        foreach (var f in functions)
        {
            if (!f.IsOverride || f.OverriddenMethod is null)
            {
                continue;
            }

            var eventMethodAttribute = f.OverriddenMethod.GetAttributes().FirstOrDefault(x =>
                x.AttributeClass?.Name == NameConsts.ManiaScriptEventMethodAttribute);

            var eventHandlerName = eventMethodAttribute?.ConstructorArguments[0].Value?.ToString();

            if (eventHandlerName is null)
            {
                continue;
            }

            yield return (f, eventHandlerName);
        }
    }

    private static string GetEventEnumName(ITypeSymbol eventTypeSymbol)
    {
        foreach (var propSymbol in eventTypeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (propSymbol.Name == "Type")
            {
                return propSymbol.Type.Name;
            }

            var actualNameAtt = propSymbol.GetAttributes()
                .FirstOrDefault(x => x.AttributeClass?.Name == NameConsts.ActualNameAttribute);

            if (actualNameAtt is not null && (string) actualNameAtt.ConstructorArguments[0].Value! == "Type")
            {
                return propSymbol.Type.Name;
            }
        }

        throw new Exception();
    }
}