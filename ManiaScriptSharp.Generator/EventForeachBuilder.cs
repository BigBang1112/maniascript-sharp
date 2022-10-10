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
        
    public void WriteEventForeach(int ident,
        Dictionary<string, Dictionary<IMethodSymbol, (ParameterListSyntax?, BlockSyntax)>> eventBlocks,
        Dictionary<string, Dictionary<IMethodSymbol, IMethodSymbol>> eventFunctions)
    {
        var possiblyImplementedEventMethods = ScriptSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(x => x.IsOverride && x.Name.StartsWith("On"))
            .ToImmutableDictionary(x => x.Name);

        var currentSymbol = ScriptSymbol;

        while (currentSymbol is not null)
        {
            foreach (var member in currentSymbol.GetMembers().OfType<IPropertySymbol>())
            {
                var eventListAtt = member.GetAttributes()
                    .FirstOrDefault(x => x.AttributeClass?.Name == NameConsts.ManiaScriptEventListAttribute);

                if (eventListAtt is null)
                {
                    continue;
                }

                var eventHandlerName = (string) eventListAtt.ConstructorArguments[0].Value!;

                var delegates = currentSymbol.GetMembers()
                    .OfType<INamedTypeSymbol>()
                    .Where(x => x.DelegateInvokeMethod is not null)
                    .ToImmutableArray();

                var delegateGeneralEventSymbol = delegates.FirstOrDefault(x => x.Name == eventHandlerName) ?? throw new Exception();
                var eventTypeSymbol = GetEventTypeSymbol(delegateGeneralEventSymbol, eventHandlerName);
                var eventEnumName = GetEventEnumName(eventTypeSymbol);
                var eventMethods = GetEventDelegateMethodArray(
                    delegates, 
                    eventHandlerName, 
                    possiblyImplementedEventMethods,
                    out var defaultEventMethod);
                
                if (eventMethods.Length == 0
                    && defaultEventMethod is null
                    && !eventBlocks.ContainsKey(member.Name)
                    && !eventFunctions.ContainsKey(member.Name))
                {
                    continue;
                }

                Writer.Write(ident, "foreach (Event in ");
                Writer.Write(member.Name);
                Writer.WriteLine(") {");

                var defaultEventBlock = default((ParameterListSyntax?, BlockSyntax)?);
                var hasEventDelegates = eventBlocks.TryGetValue(member.Name, out var eventDelegateBlockList);
                var hasGeneralEventDelegate = false;
                
                if (hasEventDelegates && eventDelegateBlockList.TryGetValue(delegateGeneralEventSymbol.DelegateInvokeMethod!, out var defaultEventB))
                {
                    defaultEventBlock = defaultEventB;
                    hasGeneralEventDelegate = true;
                    
                    Writer.WriteLine(ident + 1, "// DELEGATED CALL //");
                }

                var defaultEventFunction = default(IMethodSymbol?);
                var hasEventFunctions = eventFunctions.TryGetValue(member.Name, out var eventFunctionList);
                var hasGeneralEventFunction = false;
                
                if (hasEventFunctions && eventFunctionList.TryGetValue(delegateGeneralEventSymbol.DelegateInvokeMethod!, out defaultEventFunction))
                {
                    hasGeneralEventFunction = true;

                    Writer.Write(ident + 1, defaultEventFunction!.Name);
                    Writer.WriteLine("(Event);");
                }

                if (defaultEventMethod.HasValue)
                {
                    var (delegateSymbol, methodSymbol) = defaultEventMethod.Value;

                    Writer.Write(ident + 1, methodSymbol.Name);
                    Writer.WriteLine("(Event);");
                    
                    BodyBuilder.WriteFunctionBody(ident + 1, methodSymbol);
                }

                var hasSpecificEvents = eventMethods.Length > 0
                    || (hasEventDelegates && (eventDelegateBlockList.Count > 1 || !hasGeneralEventDelegate))
                    || (hasEventFunctions && (eventFunctionList.Count > 1 || !hasGeneralEventFunction));

                if (hasSpecificEvents)
                {
                    WriteEventSwitch(ident + 1, eventMethods, eventTypeSymbol, eventEnumName, eventDelegateBlockList, eventFunctionList);
                }

                Writer.WriteLine(ident, "}");
            }

            currentSymbol = currentSymbol.BaseType;
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
                Writer.WriteLine(ident + 2, "// FUNCTION CALL //");
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