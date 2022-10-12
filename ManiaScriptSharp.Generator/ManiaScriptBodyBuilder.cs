using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator;

public class ManiaScriptBodyBuilder
{
    private readonly GeneratorHelper _helper;
    
    public INamedTypeSymbol ScriptSymbol { get; }
    public SemanticModel SemanticModel { get; }
    public TextWriter Writer { get; }
    public ManiaScriptHead Head { get; }

    public ManiaScriptBodyBuilder(
        INamedTypeSymbol scriptSymbol,
        SemanticModel semanticModel,
        TextWriter writer,
        ManiaScriptHead head,
        GeneratorHelper helper)
    {
        ScriptSymbol = scriptSymbol;
        SemanticModel = semanticModel;
        Writer = writer;
        Head = head;
        
        _helper = helper;
    }

    public ManiaScriptBody AnalyzeAndBuild()
    {
        var methods = ScriptSymbol.GetMembers()
            .OfType<IMethodSymbol>();

        var functionsBuilder = ImmutableArray.CreateBuilder<IMethodSymbol>();
        
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
                        functionsBuilder.Add(method);
                    }
                    
                    break;
            }
        }

        var functions = functionsBuilder.ToImmutable();

        _ = mainMethodSymbol ?? throw new Exception("Main method not found");
        _ = loopMethodSymbol ?? throw new Exception("Loop method not found");
        _ = constructorSymbol ?? throw new Exception("Constructor not found");

        var constructorAnalysis = ConstructorAnalysis.Analyze(constructorSymbol, SemanticModel, _helper);
        
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

        var ident = functions.Length == 0 ? 0 : 1;
        
        var mainDocBuilder = new DocumentationBuilder(this);
        mainDocBuilder.WriteDocumentation(0, mainMethodSymbol);

        if (functions.Length > 0)
        {
            Writer.WriteLine("main() {");
        }

        WriteMainContents(ident);
        
        var loopDocBuilder = new DocumentationBuilder(this);
        loopDocBuilder.WriteDocumentation(ident, loopMethodSymbol);
        
        Writer.WriteLine(ident, "while (True) {");
        WriteLoopContents(ident + 1, functions, constructorAnalysis);
        Writer.WriteLine(ident, "}");

        if (functions.Length > 0)
        {
            Writer.WriteLine("}");
        }

        return new();
    }
    
    private void WriteMainContents(int ident)
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

    private void WriteLoopContents(int ident, ImmutableArray<IMethodSymbol> functions,
        ConstructorAnalysis constructorAnalysis)
    {
        Writer.WriteLine(ident, "yield;");

        var eventForeachBuilder = new EventForeachBuilder(this);
        eventForeachBuilder.WriteEventForeach(ident, functions, constructorAnalysis);
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