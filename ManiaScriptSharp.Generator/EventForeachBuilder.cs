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
                .FirstOrDefault(x => x.AttributeClass?.Name == "ManiaScriptExternalEventAttribute");

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
                // external event
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
        var eventFunctionLookup = delegateEventFunctions.ToLookup(x => x.Item1, x => x.Item2, SymbolEqualityComparer.Default);
        
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
                    
                    var eventName = delegateSymbol.Name
                        .Substring(0, delegateSymbol.Name.Length - 12);
                    
                    Writer.Write(ident + 2, "case ");
                    Writer.Write(usedEventTypeSymbol.Name);
                    Writer.Write("::");
                    Writer.Write(eventEnumName);
                    Writer.Write("::");
                    Writer.Write(eventName);
                    Writer.WriteLine(": {");

                    WriteEventContents(ident + 3, eventFunctionLookup!, delegateSymbol, isGeneralEvent: false);
                    
                    Writer.WriteLine(ident + 2, "}");
                }
                
                Writer.WriteLine(ident + 1, "}");
            }
            
            Writer.WriteLine(ident, "}");
        }
    }

    private void WriteEventContents(int ident, ILookup<ISymbol, EventFunction> eventFunctionLookup, INamedTypeSymbol eventDelegate,
        bool isGeneralEvent)
    {
        var delegateMethod = eventDelegate.DelegateInvokeMethod!;
        
        foreach (var eventFunction in eventFunctionLookup[eventDelegate])
        {
            switch (eventFunction)
            {
                case EventIdentifier eventIdentifier:
                    Writer.Write(ident, eventIdentifier.Method.Name);

                    if (isGeneralEvent)
                    {
                        Writer.WriteLine("(Event);");
                        continue;
                    }
                    
                    Writer.Write('(');

                    for (var i = 0; i < delegateMethod.Parameters.Length; i++)
                    {
                        var parameter = delegateMethod.Parameters[i];
                        
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
    }

    private static bool IsGeneralEvent(INamedTypeSymbol delegateSymbol)
    {
        var delegateMethod = delegateSymbol.DelegateInvokeMethod!;
        var isGeneralEvent = delegateMethod.Parameters.Length == 1 &&
                             delegateMethod.Parameters[0].Name == "e";
        return isGeneralEvent;
    }

    private IEnumerable<INamedTypeSymbol> GetAllDelegates(ITypeSymbol? symbol)
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

    private static IEnumerable<(IMethodSymbol, string)> GetOverridenEventFunctions(ImmutableArray<IMethodSymbol> functions)
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

    private static ImmutableArray<(IMethodSymbol, IMethodSymbol)> GetEventDelegateMethodArray(ImmutableArray<INamedTypeSymbol> delegates, string eventHandlerName,
        ImmutableDictionary<string, IMethodSymbol> possiblyImplementedEventMethods, out (IMethodSymbol, IMethodSymbol)? defaultEventMethod)
    {
        defaultEventMethod = null;
        
        var builder = ImmutableArray.CreateBuilder<(IMethodSymbol, IMethodSymbol)>();
            
        foreach (var delegateSymbol in delegates)
        {
            var delegateMethod = delegateSymbol.DelegateInvokeMethod!;
            var isGeneralEvent = delegateMethod.Parameters.Length == 1 &&
                                 delegateMethod.Parameters[0].Name == "e";
            var eventName = delegateSymbol.Name
                .Substring(0, delegateSymbol.Name.Length - 12 + (isGeneralEvent ? 5 : 0));

            if (!possiblyImplementedEventMethods.TryGetValue("On" + eventName, out var eventMethod))
            {
                continue;
            }

            if (delegateSymbol.Name == eventHandlerName)
            {
                defaultEventMethod = (delegateMethod, eventMethod);
            }
            else
            {
                builder.Add((delegateMethod, eventMethod));
            }
        }
            
        return builder.ToImmutable();
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

            if (actualNameAtt is not null && (string)actualNameAtt.ConstructorArguments[0].Value! == "Type")
            {
                return propSymbol.Type.Name;
            }
        }

        throw new Exception();
    }

    private static ITypeSymbol GetEventTypeSymbol(INamedTypeSymbol delegateGeneralEventSymbol, string eventHandlerName)
    {
        var method = delegateGeneralEventSymbol.DelegateInvokeMethod!;
        var isGeneralEvent = method.Parameters.Length == 1 && method.Parameters[0].Name == "e";

        if (!isGeneralEvent)
        {
            throw new Exception();
        }

        return method.Parameters[0].Type;
    }

    private void WriteEventSwitch(int ident, ImmutableArray<(IMethodSymbol, IMethodSymbol)> eventMethods,
        ITypeSymbol eventTypeSymbol, string eventEnumName,
        IReadOnlyDictionary<IMethodSymbol, (ParameterListSyntax?, BlockSyntax)>? eventDelegateBlockList,
        IReadOnlyDictionary<IMethodSymbol, IMethodSymbol>? eventFunctionList)
    {
        var eventDelegateUsedDict = new Dictionary<IMethodSymbol, (ParameterListSyntax?, BlockSyntax)>(SymbolEqualityComparer.Default);
        var eventFunctionUsedDict = new Dictionary<IMethodSymbol, IMethodSymbol>(SymbolEqualityComparer.Default);

        Writer.WriteLine(ident, "switch (Event.Type) {");

        foreach (var (delegateSymbol, methodSymbol) in eventMethods)
        {
            var actualEventName = delegateSymbol.ReceiverType?.GetAttributes()
                .FirstOrDefault(x => x.AttributeClass?.Name == NameConsts.ActualEventNameAttribute)?
                .ConstructorArguments[0].Value as string;

            var eventName = actualEventName ?? methodSymbol.Name.Substring(2);

            Writer.Write(ident + 1, "case ");
            Writer.Write(eventTypeSymbol.Name);
            Writer.Write("::");
            Writer.Write(eventEnumName);
            Writer.Write("::");
            Writer.Write(eventName);
            Writer.WriteLine(": {");

            if (eventDelegateBlockList?.TryGetValue(delegateSymbol, out var eventDelegateBlock) == true)
            {
                eventDelegateUsedDict.Add(delegateSymbol, eventDelegateBlock);
                // method is overriden and event delegate is used
                // write event delegate block or call its function
                Writer.WriteLine(ident + 2, "// DELEGATED CALL //");
            }

            if (eventFunctionList?.TryGetValue(delegateSymbol, out var eventFunction) == true)
            {
                eventFunctionUsedDict.Add(delegateSymbol, eventFunction);
                WriteEventFunctionCall(ident, delegateSymbol, eventFunction);
            }

            WriteEventFunctionCall(ident, delegateSymbol, methodSymbol);
            Writer.WriteLine(ident + 1, "}");
        }

        if (eventDelegateBlockList is not null)
        {
            foreach (var pair in eventDelegateBlockList)
            {
                var delegateSymbol = pair.Key;
                var (paramList, block) = pair.Value;

                if (eventDelegateUsedDict.ContainsKey(delegateSymbol))
                {
                    continue;
                }

                if (!WriteEventSwitchCaseBegin(ident, delegateSymbol, eventTypeSymbol, eventEnumName))
                {
                    continue;
                }
                
                // method is not overriden and event delegate is used
                // write event delegate block or call its function
                Writer.WriteLine(ident + 2, "// DELEGATED CALL //");

                Writer.WriteLine(ident + 1, "}");
            }
        }

        if (eventFunctionList is not null)
        {
            foreach (var pair in eventFunctionList)
            {
                var delegateSymbol = pair.Key;
                var methodSymbol = pair.Value;

                if (eventFunctionUsedDict.ContainsKey(delegateSymbol))
                {
                    continue;
                }

                if (!WriteEventSwitchCaseBegin(ident, delegateSymbol, eventTypeSymbol, eventEnumName))
                {
                    continue;
                }

                WriteEventFunctionCall(ident, delegateSymbol, methodSymbol);

                Writer.WriteLine(ident + 1, "}");
            }
        }

        Writer.WriteLine(ident, "}");
    }

    private void WriteEventFunctionCall(int ident, IMethodSymbol delegateSymbol, IMethodSymbol methodSymbol)
    {
        Writer.Write(ident + 2, methodSymbol.Name);
        Writer.Write('(');

        var isFirst = true;

        foreach (var p in delegateSymbol.Parameters)
        {
            if (isFirst)
            {
                isFirst = false;
            }
            else
            {
                Writer.Write(", ");
            }

            var nameOfEventClassMember = p.GetAttributes()
                .FirstOrDefault(x => x.AttributeClass?.Name == NameConsts.ActualNameAttribute)?
                .ConstructorArguments[0].Value?.ToString() ?? char.ToUpper(p.Name[0]) + p.Name.Substring(1);

            Writer.Write("Event.");
            Writer.Write(nameOfEventClassMember);
        }

        Writer.WriteLine(");");
    }

    private bool WriteEventSwitchCaseBegin(int ident, IMethodSymbol delegateSymbol, ITypeSymbol eventTypeSymbol, string eventEnumName)
    {
        var delegateType = (INamedTypeSymbol)delegateSymbol.ReceiverType!;
        var delegateMethod = delegateType.DelegateInvokeMethod!;
        var isGeneralEvent = delegateMethod.Parameters.Length == 1 &&
                             delegateMethod.Parameters[0].Name == "e";

        if (isGeneralEvent)
        {
            return false;
        }

        var actualEventName = delegateSymbol.ReceiverType?.GetAttributes()
            .FirstOrDefault(x => x.AttributeClass?.Name == NameConsts.ActualEventNameAttribute)?
            .ConstructorArguments[0].Value as string;

        var eventName = actualEventName ?? delegateType.Name
                .Substring(0, delegateType.Name.Length - 12 + (isGeneralEvent ? 5 : 0));

        Writer.Write(ident + 1, "case ");
        Writer.Write(eventTypeSymbol.Name);
        Writer.Write("::");
        Writer.Write(eventEnumName);
        Writer.Write("::");
        Writer.Write(eventName);
        Writer.WriteLine(": {");

        return true;
    }
}