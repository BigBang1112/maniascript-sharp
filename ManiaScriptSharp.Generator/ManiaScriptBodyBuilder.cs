using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator;

public class ManiaScriptBodyBuilder
{
    public INamedTypeSymbol ScriptSymbol { get; }
    public TextWriter Writer { get; }
    public ManiaScriptHead Head { get; }
    
    private Dictionary<string, Dictionary<IMethodSymbol, (ParameterListSyntax?, BlockSyntax)>> eventBlocks = new();
    private Dictionary<string, Dictionary<IMethodSymbol, IMethodSymbol>> eventFunctions = new();

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
        
        var functions = new List<IMethodSymbol>();
        
        var mainMethodSymbol = default(IMethodSymbol);
        var loopMethodSymbol = default(IMethodSymbol);
        var constructorSymbol = default(IMethodSymbol);

        foreach (var method in methods)
        {
            switch (method.Name)
            {
                case "Main":
                    mainMethodSymbol = method;
                    break;
                case "Loop":
                    loopMethodSymbol = method;
                    break;
                default:
                    
                    if (method.MethodKind == MethodKind.Constructor)
                    {
                        constructorSymbol = method;
                    }
                    else if(method.MethodKind != MethodKind.PropertyGet &&
                            method.MethodKind != MethodKind.PropertySet)
                    {
                        functions.Add(method);
                    }
                    
                    break;
            }
        }

        _ = mainMethodSymbol ?? throw new Exception("Main method not found");
        _ = loopMethodSymbol ?? throw new Exception("Loop method not found");
        _ = constructorSymbol ?? throw new Exception("Constructor not found");

        if (constructorSymbol.DeclaringSyntaxReferences.Length > 0)
        {
            var constructorSyntax = (ConstructorDeclarationSyntax)constructorSymbol.DeclaringSyntaxReferences[0].GetSyntax();

            // Can be null if expression statement
            if (constructorSyntax.Body is not null)
            {
                foreach (var statement in constructorSyntax.Body.Statements)
                {
                    if (statement is not ExpressionStatementSyntax
                        {
                            Expression: AssignmentExpressionSyntax {OperatorToken.Text: "+="} assignmentExpressionSyntax
                        })
                    {
                        continue;
                    }
                    
                    var identifierSyntax = assignmentExpressionSyntax.Left as IdentifierNameSyntax;
                    var memberAccessSyntax = assignmentExpressionSyntax.Left as MemberAccessExpressionSyntax;

                    while (memberAccessSyntax is not null)
                    {
                        if (memberAccessSyntax.Expression is not MemberAccessExpressionSyntax syntax)
                        {
                            if (memberAccessSyntax.Expression is IdentifierNameSyntax identifierNameSyntax)
                            {
                                identifierSyntax = identifierNameSyntax;
                            }
                            
                            break;
                        }

                        memberAccessSyntax = syntax;
                    }

                    if (identifierSyntax is null)
                    {
                        continue;
                    }

                    var scriptSymbol = ScriptSymbol.BaseType; // In this case the base types are important
                    var parent = identifierSyntax.Parent;
                    var name = identifierSyntax.Identifier.Text;
                    var member = default(ISymbol);
                    var eventSymbol = default(IEventSymbol);
                    
                    while (scriptSymbol is not null)
                    {
                        member = scriptSymbol.GetMembers(name).FirstOrDefault();
                        eventSymbol = member as IEventSymbol;
                        
                        if (member is not null)
                        {
                            break;
                        }
                        
                        scriptSymbol = scriptSymbol.BaseType;
                    }

                    if (member is null)
                    {
                        continue;
                    }

                    var eventListName = "";
                    
                    while (parent is MemberAccessExpressionSyntax parentSyntax && member is IPropertySymbol propertyMember)
                    {
                        if (parentSyntax.Name is not IdentifierNameSyntax nameSyntax)
                        {
                            throw new Exception("Parent syntax name is not IdentifierNameSyntax");
                        }
                        
                        eventListName += name + ".";
                        
                        name = nameSyntax.Identifier.Text;

                        var typeSymbol = propertyMember.Type;
                        
                        while (typeSymbol is not null)
                        {
                            member = typeSymbol.GetMembers(name).FirstOrDefault();
                            eventSymbol = member as IEventSymbol;
                            
                            if (eventSymbol is not null)
                            {
                                break;
                            }
                        
                            typeSymbol = typeSymbol.BaseType;
                        }

                        if (member is null)
                        {
                            break;
                        }
                        
                        parent = parentSyntax.Parent;
                    }
                    
                    if (eventSymbol is null)
                    {
                        continue;
                    }

                    var eventAtt = eventSymbol.Type
                        .GetAttributes()
                        .FirstOrDefault(x => x.AttributeClass?.Name == "ManiaScriptEventAttribute");

                    if (eventAtt is null)
                    {
                        continue;
                    }
                    
                    eventListName += (string)eventAtt.ConstructorArguments[0].Value!;
                    
                    var eventName = memberAccessSyntax is null ? name : memberAccessSyntax.Name.Identifier.Text;

                    (ParameterListSyntax?, BlockSyntax)? block = assignmentExpressionSyntax.Right switch
                    {
                        // AnonymousFunctionExpressionSyntax is weird
                        AnonymousMethodExpressionSyntax a => (a.ParameterList, a.Block),
                        ParenthesizedLambdaExpressionSyntax l => (l.ParameterList, l.Block ?? SyntaxFactory.Block(SyntaxFactory.ExpressionStatement(l.ExpressionBody ?? throw new NotSupportedException("Lambda body not supported")))),
                        IdentifierNameSyntax => null,
                        _ => throw new NotSupportedException($"{assignmentExpressionSyntax.Right.GetType().Name} not supported")
                    };

                    var functionCall = default(IMethodSymbol);

                    if (assignmentExpressionSyntax.Right is IdentifierNameSyntax i)
                    {
                        functionCall = ScriptSymbol.GetMembers(i.Identifier.Text)
                            .OfType<IMethodSymbol>()
                            .FirstOrDefault();
                    }

                    var eventDelegateInvoke = (eventSymbol.Type as INamedTypeSymbol)?.DelegateInvokeMethod ?? throw new Exception("This should not happen");

                    if (block is null)
                    {
                        if (functionCall is null)
                        {
                            continue;
                        }

                        if (eventFunctions.TryGetValue(eventListName, out var eventFunctionList))
                        {
                            if (!eventFunctionList.ContainsKey(eventDelegateInvoke))
                            {
                                eventFunctionList.Add(eventDelegateInvoke, functionCall);
                            }
                        }
                        else
                        {
                            eventFunctions.Add(eventListName, new(SymbolEqualityComparer.Default)
                            {
                                {eventDelegateInvoke, functionCall}
                            });
                        }

                        continue;
                    }

                    if (eventBlocks.TryGetValue(eventListName, out var eventList))
                    {
                        if (!eventList.ContainsKey(eventDelegateInvoke))
                        {
                            eventList.Add(eventDelegateInvoke, block.Value);
                        }
                    }
                    else
                    {
                        eventBlocks.Add(eventListName, new(SymbolEqualityComparer.Default)
                        {
                            {eventDelegateInvoke, block.Value}
                        });
                    }
                }
            }
        }
        
        foreach (var functionSymbol in functions)
        {
            var docBuilder = new DocumentationBuilder(this);
            docBuilder.WriteDocumentation(ident: 0, functionSymbol);

            Writer.Write(Standardizer.CSharpTypeToManiaScriptType(functionSymbol.ReturnType.Name));
            Writer.Write(' ');
            Writer.Write(Standardizer.CSharpTypeToManiaScriptType(functionSymbol.Name));
            Writer.Write('(');

            var isFirst = true;
            
            foreach (var parameter in functionSymbol.Parameters)
            {
                if (isFirst)
                {
                    isFirst = false;
                }
                else
                {
                    Writer.Write(", ");
                }

                Writer.Write(Standardizer.CSharpTypeToManiaScriptType((INamedTypeSymbol)parameter.Type));
                Writer.Write(' ');
                Writer.Write(Standardizer.StandardizeUnderscoreName(parameter.Name));
            }
            
            Writer.WriteLine(") {");
            WriteFunctionBody(ident: 1, functionSymbol);
            Writer.WriteLine("}");
            Writer.WriteLine();
        }

        var ident = functions.Count == 0 ? 0 : 1;
        
        var mainDocBuilder = new DocumentationBuilder(this);
        mainDocBuilder.WriteDocumentation(0, mainMethodSymbol);

        if (functions.Count > 0)
        {
            Writer.WriteLine("main() {");
        }

        WriteMain(ident);
        
        var loopDocBuilder = new DocumentationBuilder(this);
        loopDocBuilder.WriteDocumentation(ident, loopMethodSymbol);
        
        Writer.WriteLine(ident, "while (true) {");
        WriteLoop(ident + 1);
        Writer.WriteLine(ident, "}");

        if (functions.Count > 0)
        {
            Writer.WriteLine("}");
        }

        return new();
    }
    
    private void WriteMain(int ident)
    {
        WriteGlobalInitializers(ident);
        WriteBindingInitializers(ident);

        var mainMethodSyntax = ScriptSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .First(x => x.Name == "Main")
            .DeclaringSyntaxReferences[0]
            .GetSyntax() as MethodDeclarationSyntax;
    }

    private void WriteGlobalInitializers(int ident)
    {
        if (Head.Globals.Length <= 0)
        {
            return;
        }

        foreach (var global in Head.Globals)
        {
            var equalsSyntax = global.DeclaringSyntaxReferences[0].GetSyntax() switch
            {
                PropertyDeclarationSyntax propertyDeclarationSyntax => propertyDeclarationSyntax.Initializer,
                VariableDeclaratorSyntax variableDeclaratorSyntax => variableDeclaratorSyntax.Initializer,
                _ => throw new NotSupportedException("Unknown global declaration syntax")
            };

            if (equalsSyntax is null)
            {
                continue;
            }

            Writer.WriteIdent(ident);
            Writer.Write(Standardizer.StandardizeGlobalName(global.Name));
            Writer.Write(" = ");
            Writer.Write(equalsSyntax.Value);
            Writer.WriteLine(";");
        }

        Writer.WriteLine();
    }

    private void WriteBindingInitializers(int ident)
    {
        if (Head.Bindings.Length <= 0)
        {
            return;
        }

        foreach (var binding in Head.Bindings)
        {
            var manialinkControlAtt = binding.GetAttributes()
                .First(x => x.AttributeClass?.Name == NameConsts.ManialinkControlAttribute);

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
                IPropertySymbol prop => prop.Type,
                IFieldSymbol field => field.Type,
                _ => throw new Exception("This should never happen")
            };

            Writer.WriteIdent(ident);

            Writer.Write(binding.Name);
            Writer.Write(" = (Page.GetFirstChild(\"");
            Writer.Write(controlId);
            Writer.Write("\") as ");
            Writer.Write(type.Name);
            Writer.WriteLine(");");
        }

        Writer.WriteLine();
    }

    private void WriteLoop(int ident)
    {
        Writer.WriteLine(ident, "yield;");

        var eventForeachBuilder = new EventForeachBuilder(this);
        eventForeachBuilder.WriteEventForeach(ident, eventBlocks, eventFunctions);
    }

    public void WriteFunctionBody(int ident, IMethodSymbol methodSymbol)
    {
        if (methodSymbol.DeclaringSyntaxReferences.Length <= 0 ||
            methodSymbol.DeclaringSyntaxReferences[0].GetSyntax() is not MethodDeclarationSyntax methodSyntax)
        {
            return;
        }

        var body = methodSyntax.Body;

        if (body is null)
        {
            return;
        }
        
        var statements = body.Statements;

        /*if (statements is not null)
        {
            foreach (var statement in statements)
            {
                Writer.Write(ident + 2, statement.ToFullString());
            }
        }*/
    }
}