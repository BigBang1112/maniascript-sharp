using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator;

public class EventForeachBuilder
{
    private readonly ManiaScriptBodyBuilder _bodyBuilder;

    private ITypeSymbol ScriptSymbol => _bodyBuilder.ScriptSymbol;
    private TextWriter Writer => _bodyBuilder.Writer;

    public EventForeachBuilder(ManiaScriptBodyBuilder bodyBuilder)
    {
        _bodyBuilder = bodyBuilder;
    }

    public void Write(int indent, ImmutableArray<IMethodSymbol> functions,
        ConstructorAnalysis constructorAnalysis)
    {
        var overridenEventFunctions = GetOverridenEventFunctions(functions).ToImmutableArray();
        var pluginCustomEventFunctions = GetPluginCustomEventFunctions(functions).ToImmutableArray();
        var xmlRpcEventFunctions = GetXmlRpcEventFunctions(functions).ToImmutableArray();

        if (overridenEventFunctions.IsEmpty
            && constructorAnalysis.EventFunctions.IsEmpty
            && pluginCustomEventFunctions.IsEmpty
            && xmlRpcEventFunctions.IsEmpty)
        {
            return;
        }

        var delegateDict = GetAllDelegates(ScriptSymbol).ToImmutableDictionary(x => x.Name);
        var eventListSymbolDict = new Dictionary<string, IPropertySymbol>();
        var eventListDelegates = new HashSet<(string, INamedTypeSymbol)>();
        var delegateEventFunctions = new List<(INamedTypeSymbol, Function)>();
        var externalEvents = new List<(INamedTypeSymbol, string, string)>();
        var externalDelegateEventFunctions = new List<((string, string), Function)>();
        var xmlRpcEvents = new List<(INamedTypeSymbol, string, IMethodSymbol)>();

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

            if (_bodyBuilder.SemanticModel.GetSymbolInfo(identifier).Symbol is not IEventSymbol eventSymbol)
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
                externalDelegateEventFunctions.Add(((objectIdentifierName, delegateSymbol.Name), function));

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
            delegateEventFunctions.Add((delegateSymbol, new FunctionIdentifier(eventFunction)));

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

        foreach (var _ in pluginCustomEventFunctions)
        {
            if (eventListSymbolDict.ContainsKey(NameConsts.PendingEvents))
            {
                continue;
            }
            
            var eventListSymbol = GenericExtensions.Flatten(ScriptSymbol, x => x.BaseType)
                    .Select(x => x.GetMembers(NameConsts.PendingEvents).OfType<IPropertySymbol>().FirstOrDefault())
                    .FirstOrDefault(x => x is not null);

            if (eventListSymbol is null)
            {
                continue;
            }

            var contextSymbol = eventListSymbol.ContainingType;
            
            if (contextSymbol.Name != "CMlScript")
            {
                throw new NotSupportedException("PluginCustomEvent attribute is not supported for non-CMlScript types.");
            }
            
            var pluginCustomEventHandlerSymbol = contextSymbol.GetTypeMembers("PluginCustomEventEventHandler")
                .FirstOrDefault();

            if (pluginCustomEventHandlerSymbol is null)
            {
                continue;
            }
            
            eventListDelegates.Add((NameConsts.PendingEvents, pluginCustomEventHandlerSymbol));
            eventListSymbolDict.Add(NameConsts.PendingEvents, eventListSymbol);
        }

        foreach (var (methodSymbol, xmlRpcMethod) in xmlRpcEventFunctions)
        {
            var eventContextSymbol = GenericExtensions.Flatten(ScriptSymbol, x => x.BaseType)
                .Select(x => x.GetMembers("XmlRpc").OfType<IPropertySymbol>().FirstOrDefault())
                .FirstOrDefault(x => x is not null);

            if (eventContextSymbol is null)
            {
                continue;
            }
            
            var eventListSymbol = eventContextSymbol.Type.GetMembers(NameConsts.PendingEvents)
                .OfType<IPropertySymbol>()
                .FirstOrDefault();

            var isArray = methodSymbol.Parameters.Length > 1;
            
            var xmlRpcEventHandlerSymbol = eventContextSymbol.Type.GetTypeMembers(isArray
                ? "CallbackArrayEventHandler" : "CallbackEventHandler").FirstOrDefault();

            if (xmlRpcEventHandlerSymbol is null)
            {
                continue;
            }
            
            eventListDelegates.Add(("XmlRpc.PendingEvents", xmlRpcEventHandlerSymbol));
            xmlRpcEvents.Add((xmlRpcEventHandlerSymbol, xmlRpcMethod, methodSymbol));
            
            if (eventListSymbolDict.ContainsKey("XmlRpc.PendingEvents"))
            {
                continue;
            }
            
            eventListSymbolDict.Add("XmlRpc.PendingEvents", eventListSymbol);
        }

        var delegateLookup = eventListDelegates.ToLookup(x => x.Item1, x => x.Item2);
        var eventFunctionLookup =
            delegateEventFunctions.ToLookup(x => x.Item1, x => x.Item2, SymbolEqualityComparer.Default);
        var externalEventFunctionLookup =
            externalDelegateEventFunctions.ToLookup(x => x.Item1, x => x.Item2);
        var externalEventLookup = externalEvents.ToLookup(x => x.Item1, x => (x.Item2, x.Item3), SymbolEqualityComparer.Default);
        var xmlRpcEventLookup = xmlRpcEvents.ToLookup(x => x.Item1, x => (x.Item2, x.Item3), SymbolEqualityComparer.Default);
        
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

            Writer.Write(indent, "foreach (Event in ");
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
                WriteEventContents(indent + 1, eventFunctionLookup!, generalEventDelegate, isGeneralEvent: true);
            }

            if (delegateLookup[usedEventListName].Any(x => !IsGeneralEvent(x)))
            {
                Writer.WriteLine(indent + 1, "switch (Event.Type) {");

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

                    Writer.Write(indent + 2, "case ");
                    Writer.Write(usedEventTypeSymbol.Name);
                    Writer.Write("::");
                    Writer.Write(eventEnumName);
                    Writer.Write("::");
                    Writer.Write(eventName);
                    Writer.WriteLine(": {");

                    WriteExternalEvent(indent + 3, externalEventLookup, delegateSymbol, externalEventFunctionLookup);
                    
                    WritePluginCustomEvents(indent + 3, pluginCustomEventFunctions);

                    if (xmlRpcEventLookup.Contains(delegateSymbol))
                    {
                        var isArray = delegateSymbol.Name == "CallbackArrayEventHandler";
                        WriteXmlRpcEvents(indent + 3, xmlRpcEventLookup[delegateSymbol], isArray);
                    }

                    WriteEventContents(indent + 3, eventFunctionLookup!, delegateSymbol, isGeneralEvent: false);

                    Writer.WriteLine(indent + 2, "}");
                }

                Writer.WriteLine(indent + 1, "}");
            }

            Writer.WriteLine(indent, "}");
        }
    }

    private void WriteXmlRpcEvents(int indent, IEnumerable<(string, IMethodSymbol)> xmlRpcEvents, bool isArray)
    {
        Writer.Write(indent, "switch (Event.");
        Writer.Write(isArray ? "ParamArray1" : "Param1");
        Writer.WriteLine(") {");
        
        foreach (var (xmlRpcMethod, methodSymbol) in xmlRpcEvents)
        {
            Writer.Write(indent + 1, "case \"");
            Writer.Write(xmlRpcMethod);
            Writer.WriteLine("\": {");
            
            Writer.Write(indent + 2, methodSymbol.Name);
            Writer.Write('(');

            for (var i = 0; i < methodSymbol.Parameters.Length; i++)
            {
                var parameter = methodSymbol.Parameters[i]; // TODO: Add conversion based on type
                
                if (parameter.Type.SpecialType != SpecialType.System_String)
                {
                    throw new NotSupportedException("Unsupported parameter type for XmlRpcCallback attribute.");
                }
                
                if (isArray)
                {
                    if (i != 0)
                    {
                        Writer.Write(", ");
                    }

                    Writer.Write("Event.ParamArray2[");
                    Writer.Write(i);
                    Writer.Write(']');
                }
                else
                {
                    Writer.Write("Event.Param2");
                }
            }

            Writer.WriteLine(");");
            
            Writer.WriteLine(indent + 1, "}");
        }
        
        Writer.WriteLine(indent, "}");
    }

    private void WritePluginCustomEvents(int indent, ImmutableArray<(IMethodSymbol, string)> pluginCustomEventFunctions)
    {
        if (pluginCustomEventFunctions.IsDefaultOrEmpty)
        {
            return;
        }
        
        Writer.WriteLine(indent, "switch (Event.CustomEventType) {");
        
        foreach (var (methodSymbol, eventName) in pluginCustomEventFunctions)
        {
            Writer.Write(indent + 1, "case \"");
            Writer.Write(eventName);
            Writer.WriteLine("\": {");
            
            Writer.Write(indent + 2, methodSymbol.Name);
            Writer.Write('(');

            for (var i = 0; i < methodSymbol.Parameters.Length; i++)
            {
                var parameter = methodSymbol.Parameters[i]; // TODO: Add conversion based on type
                
                if (parameter.Type.SpecialType != SpecialType.System_String)
                {
                    throw new NotSupportedException("Unsupported parameter type for PluginCustomEvent attribute.");
                }
                
                if (i != 0)
                {
                    Writer.Write(", ");
                }
                
                Writer.Write("Event.CustomEventData[");
                Writer.Write(i);
                Writer.Write(']');
            }

            Writer.WriteLine(");");
            
            Writer.WriteLine(indent + 1, "}");
        }
        
        Writer.WriteLine(indent, "}");
    }

    private void WriteExternalEvent(int indent, ILookup<ISymbol?, (string, string)> externalEventLookup, INamedTypeSymbol delegateSymbol,
        ILookup<(string, string), Function> externalEventFunctionLookup)
    {
        if (!externalEventLookup.Contains(delegateSymbol))
        {
            return;
        }

        foreach (var objectIdentifiers in externalEventLookup[delegateSymbol].ToLookup(x => x.Item1, x => x.Item2))
        {
            Writer.Write(indent, "switch (Event.");
            Writer.Write(objectIdentifiers.Key);
            Writer.WriteLine(") {");

            foreach (var objectIdentifierName in objectIdentifiers.Distinct())
            {
                Writer.Write(indent + 1, "case ");
                Writer.Write(objectIdentifierName);
                Writer.WriteLine(": {");

                foreach (var eventFunction in externalEventFunctionLookup[(objectIdentifierName, delegateSymbol.Name)])
                {
                    WriteEventContents(indent + 2, eventFunction,
                        ImmutableArray<IParameterSymbol>.Empty, isGeneralEvent: false);
                }

                Writer.WriteLine(indent + 1, "}");
            }

            Writer.WriteLine(indent, "}");
        }
    }

    private void WriteEventContents(int indent, ILookup<ISymbol, Function> eventFunctionLookup,
        INamedTypeSymbol eventDelegate,
        bool isGeneralEvent)
    {
        var delegateMethod = eventDelegate.DelegateInvokeMethod!;

        foreach (var eventFunction in eventFunctionLookup[eventDelegate])
        {
            WriteEventContents(indent, eventFunction, delegateMethod.Parameters, isGeneralEvent);
        }
    }

    private void WriteEventContents(int indent, Function eventFunction, ImmutableArray<IParameterSymbol> parameters,
        bool isGeneralEvent)
    {
        switch (eventFunction)
        {
            case FunctionIdentifier eventIdentifier:
                
                var actualNameDict = new Dictionary<IParameterSymbol, string>(SymbolEqualityComparer.Default);
                var arrayParamSet = new Dictionary<IParameterSymbol, string>(SymbolEqualityComparer.Default);
                
                // if array, copy the data over
                foreach (var parameter in parameters)
                {
                    var actualName = GetActualNameFromParameter(parameter);
                    
                    if (string.IsNullOrWhiteSpace(actualName))
                    {
                        continue;
                    }
                    
                    actualNameDict.Add(parameter, actualName);

                    var standardizedType = Standardizer.CSharpTypeToManiaScriptType((INamedTypeSymbol)parameter.Type, knownStructNames: null);
                    
                    if (!standardizedType.EndsWith("[]"))
                    {
                        continue;
                    }

                    var standName = Standardizer.StandardizeName(parameter.Name);
                    arrayParamSet.Add(parameter, standName);
                    
                    WriteApiArrayTranslation(indent, standardizedType, standName, actualName);
                }

                Writer.WriteIndent(indent);

                if (eventIdentifier.Method.DeclaredAccessibility == Accessibility.Private)
                {
                    Writer.Write("Private_");
                }

                Writer.Write(eventIdentifier.Method.Name);

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

                    if (arrayParamSet.TryGetValue(parameter, out var paramName))
                    {
                        Writer.Write(paramName);
                    }
                    else
                    {
                        Writer.Write("Event.");

                        if (actualNameDict.TryGetValue(parameter, out var actualName))
                        {
                            Writer.Write(actualName);
                        }
                    }
                }

                Writer.WriteLine(");");
                break;
            case FunctionAnonymous eventAnonymous:
                Writer.WriteLine(indent, "// Start of anonymous function");

                var originalParams = eventAnonymous.DelegateInvokeSymbol.Parameters;

                for (var i = 0; i < originalParams.Length; i++)
                {
                    var originalParam = originalParams[i];
                    var parameter = eventAnonymous.Parameters[i];
                    var actualNameAtt = originalParam.GetAttributes()
                        .FirstOrDefault(x => x.AttributeClass?.Name == NameConsts.ActualNameAttribute);
                    var actualName = actualNameAtt?.ConstructorArguments[0].Value?.ToString() ??
                                     Standardizer.StandardizeName(originalParam.Name);
                    var standName = Standardizer.StandardizeName(parameter.Identifier.Text);
                    
                    var standardizedType = Standardizer.CSharpTypeToManiaScriptType((INamedTypeSymbol)originalParam.Type, knownStructNames: null);
                    
                    if (standardizedType.EndsWith("[]"))
                    {
                        WriteApiArrayTranslation(indent, standardizedType, standName, actualName);
                        continue;
                    }

                    Writer.Write(indent, "declare ");
                    Writer.Write(standName);

                    if (isGeneralEvent)
                    {
                        Writer.Write(" <=> Event");
                    }
                    else
                    {
                        Writer.Write(" = Event.");
                        Writer.Write(actualName);
                    }

                    Writer.WriteLine(";");
                }

                _bodyBuilder.WriteFunctionBody(indent, eventAnonymous);
                Writer.WriteLine(indent, "// End of anonymous function");
                break;
        }
    }

    private void WriteApiArrayTranslation(int indent, string standardizedType, string standName, string actualName)
    {
        Writer.Write(indent, "declare ");
        Writer.Write(standardizedType);
        Writer.Write(' ');
        Writer.Write(standName);
        Writer.WriteLine(";");
        Writer.Write(indent, "foreach (Element in Event.");
        Writer.Write(actualName);
        Writer.WriteLine(") {");
        Writer.Write(indent + 1, standName);
        Writer.WriteLine(".add(Element);");
        Writer.WriteLine(indent, "}");
    }

    private static string GetActualNameFromParameter(IParameterSymbol parameter)
    {
        var actualNameAtt = parameter.GetAttributes()
            .FirstOrDefault(x => x.AttributeClass?.Name == NameConsts.ActualNameAttribute);

        if (actualNameAtt is not null)
        {
            return actualNameAtt.ConstructorArguments[0].Value?.ToString() ?? "";
        }

        if (parameter.Name.Length <= 0)
        {
            return "";
        }

        if (char.IsLower(parameter.Name[0]))
        {
            var charArray = parameter.Name.ToCharArray();
            charArray[0] = char.ToUpper(charArray[0]);
            return new string(charArray);
        }
        else
        {
            return parameter.Name;
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

    private static IEnumerable<(IMethodSymbol, string)> GetPluginCustomEventFunctions(ImmutableArray<IMethodSymbol> functions)
    {
        foreach (var f in functions)
        {
            if (f.IsVirtual)
            {
                continue;
            }

            var pluginCustomEventAttribute = f.GetAttributes().FirstOrDefault(x =>
                x.AttributeClass?.Name == "PluginCustomEventAttribute");

            var type = pluginCustomEventAttribute?.ConstructorArguments[0].Value?.ToString();

            if (type is null)
            {
                continue;
            }

            yield return (f, type);
        }
    }

    private static IEnumerable<(IMethodSymbol, string)> GetXmlRpcEventFunctions(ImmutableArray<IMethodSymbol> functions)
    {
        foreach (var f in functions)
        {
            if (f.IsVirtual)
            {
                continue;
            }

            var xmlRpcEventAttribute = f.GetAttributes().FirstOrDefault(x =>
                x.AttributeClass?.Name == "XmlRpcCallbackAttribute");

            var method = xmlRpcEventAttribute?.ConstructorArguments[0].Value?.ToString();

            if (method is null)
            {
                continue;
            }

            yield return (f, method);
        }
    }

    private static string GetEventEnumName(ITypeSymbol eventTypeSymbol)
    {
        var type = eventTypeSymbol;

        while (type is not null)
        {
            foreach (var propSymbol in type.GetMembers().OfType<IPropertySymbol>())
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

            type = type.BaseType;
        }

        throw new Exception();
    }
}