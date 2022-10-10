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
        
    public void WriteEventForeach(int ident, Dictionary<string, Dictionary<IMethodSymbol, BlockSyntax>> eventBlocks)
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
                
                if (eventMethods.Length == 0 && defaultEventMethod is null && !eventBlocks.ContainsKey(member.Name))
                {
                    continue;
                }

                Writer.Write(ident, "foreach (Event in ");
                Writer.Write(member.Name);
                Writer.WriteLine(") {");

                var hasEventDelegates = eventBlocks.TryGetValue(member.Name, out var eventDelegateBlockList);
                var hasGeneralEventDelegate = eventDelegateBlockList.TryGetValue(delegateGeneralEventSymbol.DelegateInvokeMethod!, out var defaultEventBlock);

                if (hasGeneralEventDelegate)
                {
                    Writer.WriteLine(ident + 1, "// DELEGATED CALL //");
                }

                if (defaultEventMethod.HasValue)
                {
                    var (delegateSymbol, methodSymbol) = defaultEventMethod.Value;

                    Writer.Write(ident + 1, methodSymbol.Name);
                    Writer.Write('(');
                    Writer.Write("Event");
                    Writer.WriteLine(");");
                    
                    BodyBuilder.WriteFunctionBody(ident + 1, methodSymbol);
                }

                if (eventMethods.Length > 0 || (hasEventDelegates && (eventDelegateBlockList.Count > 1 || !hasGeneralEventDelegate)))
                {
                    WriteEventSwitch(ident + 1, eventMethods, eventTypeSymbol, eventEnumName, eventDelegateBlockList);
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
        IReadOnlyDictionary<IMethodSymbol, BlockSyntax>? eventDelegateBlockList)
    {
        var eventDelegateUsedDict = new Dictionary<IMethodSymbol, BlockSyntax>(SymbolEqualityComparer.Default);
        
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
            Writer.WriteLine(ident + 1, "}");
        }
        
        if (eventDelegateBlockList is not null)
        {
            foreach (var pair in eventDelegateBlockList)
            {
                var delegateSymbol = pair.Key;

                if (eventDelegateUsedDict.ContainsKey(delegateSymbol))
                {
                    continue;
                }

                var delegateType = (INamedTypeSymbol)delegateSymbol.ReceiverType!;
                var delegateMethod = delegateType.DelegateInvokeMethod!;
                var isGeneralEvent = delegateMethod.Parameters.Length == 1 &&
                                     delegateMethod.Parameters[0].Name == "e";

                if (isGeneralEvent)
                {
                    continue;
                }

                var eventDelegateBlock = pair.Value;
                
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
                
                // method is not overriden and event delegate is used
                // write event delegate block or call its function
                Writer.WriteLine(ident + 2, "// DELEGATED CALL //");
                
                Writer.WriteLine(ident + 1, "}");
            }
        }

        Writer.WriteLine(ident, "}");
    }
}