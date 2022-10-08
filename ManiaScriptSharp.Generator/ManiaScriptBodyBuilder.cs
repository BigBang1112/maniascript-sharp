using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator;

public class ManiaScriptBodyBuilder
{
    public INamedTypeSymbol ScriptSymbol { get; }
    public TextWriter Writer { get; }
    public ManiaScriptHead Head { get; }

    public ManiaScriptBodyBuilder(INamedTypeSymbol scriptSymbol, TextWriter writer, ManiaScriptHead head)
    {
        ScriptSymbol = scriptSymbol;
        Writer = writer;
        Head = head;
    }

    public ManiaScriptBody AnalyzeAndBuild()
    {
        var methods = ScriptSymbol.GetMembers()
            .OfType<IMethodSymbol>();
        
        var customFunctions = new List<IMethodSymbol>();
        
        var mainMethodSyntax = default(MethodDeclarationSyntax);
        var loopMethodSyntax = default(MethodDeclarationSyntax);

        foreach (var method in methods)
        {
            switch (method.Name)
            {
                case "Main":
                    mainMethodSyntax = method.DeclaringSyntaxReferences[0].GetSyntax() as MethodDeclarationSyntax;
                    break;
                case "Loop":
                    loopMethodSyntax = method.DeclaringSyntaxReferences[0].GetSyntax() as MethodDeclarationSyntax;
                    break;
                default:
                    if (method.MethodKind != MethodKind.Constructor && method.MethodKind != MethodKind.PropertyGet &&
                        method.MethodKind != MethodKind.PropertySet && !method.IsOverride) customFunctions.Add(method);
                    break;
            }
        }

        _ = mainMethodSyntax ?? throw new Exception("Main method not found");
        _ = loopMethodSyntax ?? throw new Exception("Loop method not found");

        foreach (var customFunctionSymbol in customFunctions)
        {
            Writer.Write(Standardizer.CSharpTypeToManiaScriptType(customFunctionSymbol.ReturnType.Name));
            Writer.Write(' ');
            Writer.Write(Standardizer.CSharpTypeToManiaScriptType(customFunctionSymbol.Name));
            Writer.Write('(');

            var isFirst = true;
            
            foreach (var parameter in customFunctionSymbol.Parameters)
            {
                if (isFirst)
                {
                    isFirst = false;
                }
                else
                {
                    Writer.Write(", ");
                }

                Writer.Write(Standardizer.CSharpTypeToManiaScriptType(parameter.Type.Name));
                Writer.Write(' ');
                Writer.Write(Standardizer.StandardizeUnderscoreName(parameter.Name));
            }
            
            Writer.WriteLine(") {");
            //Writer.Write(((MethodDeclarationSyntax)customFunctionSymbol.DeclaringSyntaxReferences[0].GetSyntax()).Body);
            Writer.WriteLine("}");
            Writer.WriteLine();
        }

        if (customFunctions.Count == 0)
        {
            WriteMain(ident: 0);
        }
        else
        {
            Writer.WriteLine("main() {");

            WriteMain(ident: 1);

            Writer.WriteLine("}");
        }

        return new();
    }

    private void WriteMain(int ident)
    {
        foreach (var binding in Head.Bindings)
        {
            var manialinkControlAtt = binding.GetAttributes()
                .First(x => x.AttributeClass?.Name == "ManialinkControlAttribute");
            
            var controlId = manialinkControlAtt.ConstructorArguments.Length == 0
                ? binding.Name
                : manialinkControlAtt
                    .ConstructorArguments[0]
                    .Value?
                    .ToString();

            if (controlId is null)
            {
                continue;
            }

            var type = binding switch
            {
                IPropertySymbol prop => prop.Type.Name,
                IFieldSymbol field => field.Type.Name,
                _ => throw new Exception("This should never happen")
            };

            Writer.WriteIdent(ident);
            
            Writer.Write(binding.Name);
            Writer.Write(" = (Page.GetFirstChild(\"");
            Writer.Write(controlId);
            Writer.Write("\") as ");
            Writer.Write(type);
            Writer.WriteLine(");");
        }
        
        Writer.WriteLine();
        
        WriteLoop(ident);
    }
    
    private void WriteLoop(int ident)
    {
        Writer.WriteLine(ident, "while (true) {");
        WriteLoopContent(ident + 1);
        Writer.WriteLine(ident, "}");
    }

    private void WriteLoopContent(int ident)
    {
        Writer.WriteLine(ident, "yield;");

        WriteEventForeach(ident);
    }

    private void WriteEventForeach(int ident)
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
                    .FirstOrDefault(x => x.AttributeClass?.Name == "ManiaScriptEventListAttribute");

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

                Writer.Write(ident, "foreach (Event in ");
                Writer.Write(member.Name);
                Writer.WriteLine(") {");

                WriteEventSwitch(ident + 1, delegates, eventTypeSymbol, eventEnumName, eventHandlerName,
                    possiblyImplementedEventMethods);

                Writer.WriteLine(ident, "}");
            }

            currentSymbol = currentSymbol.BaseType;
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
                .FirstOrDefault(x => x.AttributeClass?.Name == "ActualNameAttribute");

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

    private void WriteEventSwitch(int ident, IEnumerable<INamedTypeSymbol> delegates, ITypeSymbol eventTypeSymbol,
        string eventEnumName, string eventHandlerName,
        ImmutableDictionary<string, IMethodSymbol> possiblyImplementedEventMethods)
    {
        Writer.WriteLine(ident, "switch (Event.Type) {");

        foreach (var delegateSymbol in delegates)
        {
            if (delegateSymbol.Name == eventHandlerName)
            {
                continue;
            }
            
            var delegateMethod = delegateSymbol.DelegateInvokeMethod!;
            var isGeneralEvent = delegateMethod.Parameters.Length == 1 && delegateMethod.Parameters[0].Name == "e";
            var eventName = delegateSymbol.Name.Substring(0, delegateSymbol.Name.Length - 12 + (isGeneralEvent ? 5 : 0));

            if (!possiblyImplementedEventMethods.TryGetValue("On" + eventName, out var eventMethod))
            {
                continue;
            }

            Writer.Write(ident + 1, "case ");
            Writer.Write(eventTypeSymbol.Name);
            Writer.Write("::");
            Writer.Write(eventEnumName);
            Writer.Write("::");
            Writer.Write(eventName);
            Writer.WriteLine(": {");

            foreach (var p in delegateMethod.Parameters)
            {
                var nameOfEventClassMember = p.GetAttributes()
                    .FirstOrDefault(x => x.AttributeClass?.Name == "ActualNameAttribute")?
                    .ConstructorArguments[0].Value?.ToString() ?? char.ToUpper(p.Name[0]) + p.Name.Substring(1);
                
                Writer.WriteLine(ident + 2, "// Event." + nameOfEventClassMember);
            }
                
            Writer.WriteLine(ident + 1, "}");
        }

        Writer.WriteLine(ident, "}");
    }
}