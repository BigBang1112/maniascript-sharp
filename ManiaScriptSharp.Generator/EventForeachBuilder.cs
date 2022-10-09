using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

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
        
    public void WriteEventForeach(int ident)
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

                var eventTypeSymbol = GetEventTypeSymbol(delegates, eventHandlerName);
                var eventEnumName = GetEventEnumName(eventTypeSymbol);
                var eventMethods = GetEventDelegateMethodArray(
                    delegates, 
                    eventHandlerName, 
                    possiblyImplementedEventMethods,
                    out var defaultEventMethod);
                
                if (eventMethods.Length == 0 && defaultEventMethod is null)
                {
                    continue;
                }

                Writer.Write(ident, "foreach (Event in ");
                Writer.Write(member.Name);
                Writer.WriteLine(") {");

                if (defaultEventMethod.HasValue)
                {
                    var (delegateSymbol, methodSymbol) = defaultEventMethod.Value;
                    
                    Writer.Write(ident + 1, methodSymbol.Name);
                    Writer.Write('(');
                    Writer.Write("Event");
                    Writer.WriteLine(");");
                    
                    BodyBuilder.WriteFunctionBody(ident + 1, methodSymbol);
                }

                if (eventMethods.Length > 0)
                {
                    WriteEventSwitch(ident + 1, eventMethods, eventTypeSymbol, eventEnumName);
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

    private static ITypeSymbol GetEventTypeSymbol(ImmutableArray<INamedTypeSymbol> delegates, string eventHandlerName)
    {
        var delegateGeneralEventSymbol = delegates.FirstOrDefault(x => x.Name == eventHandlerName);

        if (delegateGeneralEventSymbol is null)
        {
            throw new Exception();
        }

        var method = delegateGeneralEventSymbol.DelegateInvokeMethod!;
        var isGeneralEvent = method.Parameters.Length == 1 && method.Parameters[0].Name == "e";

        if (!isGeneralEvent)
        {
            throw new Exception();
        }

        return method.Parameters[0].Type;
    }

    private void WriteEventSwitch(int ident, ImmutableArray<(IMethodSymbol, IMethodSymbol)> eventMethods, ITypeSymbol eventTypeSymbol,
        string eventEnumName)
    {
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

        Writer.WriteLine(ident, "}");
    }
}