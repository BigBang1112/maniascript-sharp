using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using ManiaScriptSharp.Generator.Statements;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator;

public class ManiaScriptBodyBuilder
{
    public INamedTypeSymbol ScriptSymbol { get; }
    public SemanticModel SemanticModel { get; }
    public TextWriter Writer { get; }
    public ManiaScriptHead Head { get; }
    public GeneratorHelper Helper { get; }

    public bool IsBuildingEventHandling { get; private set; }
    public Queue<string> BlockLineQueue { get; } = new();

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
        Helper = helper;
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

        var constructorAnalysis = ConstructorAnalysis.Analyze(constructorSymbol, SemanticModel, Helper);
        
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
            WriteFunctionBody(ident: 1, new FunctionIdentifier(functionSymbol));
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

        WriteMainContents(ident, mainMethodSymbol);
        
        var loopDocBuilder = new DocumentationBuilder(this);
        loopDocBuilder.WriteDocumentation(ident, loopMethodSymbol);
        
        Writer.WriteLine(ident, "while (True) {");
        WriteLoopContents(ident + 1, functions, constructorAnalysis, loopMethodSymbol);
        Writer.WriteLine(ident, "}");

        if (functions.Length > 0)
        {
            Writer.WriteLine("}");
        }

        return new();
    }
    
    private void WriteMainContents(int ident, IMethodSymbol mainMethodSymbol)
    {
        WriteGlobalInitializers(ident);
        WriteBindingInitializers(ident);
        
        WriteFunctionBody(ident, new FunctionIdentifier(mainMethodSymbol));
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
        ConstructorAnalysis constructorAnalysis, IMethodSymbol loopMethodSymbol)
    {
        Writer.WriteLine(ident, "yield;");

        IsBuildingEventHandling = true;
        var eventForeachBuilder = new EventForeachBuilder(this);
        eventForeachBuilder.Write(ident, functions, constructorAnalysis);
        IsBuildingEventHandling = false;
        
        WriteFunctionBody(ident, new FunctionIdentifier(loopMethodSymbol));
    }

    public void WriteFunctionBody(int ident, Function function)
    {
        BlockSyntax block;
        ImmutableArray<ParameterSyntax> parameters;
        
        switch (function)
        {
            case FunctionIdentifier functionIdentifier:
            {
                if (functionIdentifier.Method.DeclaringSyntaxReferences.Length <= 0 ||
                    functionIdentifier.Method.DeclaringSyntaxReferences[0].GetSyntax() is not MethodDeclarationSyntax methodSyntax)
                {
                    return;
                }
            
                // get block from methodSyntax.ExpressionBody
                if (methodSyntax.Body is not null)
                {
                    block = methodSyntax.Body;
                }
                else if (methodSyntax.ExpressionBody is not null)
                {
                    block = SyntaxFactory.Block(SyntaxFactory.ExpressionStatement(methodSyntax.ExpressionBody.Expression));
                }
                else
                {
                    return;
                }
                
                parameters = methodSyntax.ParameterList.Parameters.ToImmutableArray();
                break;
            }
            case FunctionAnonymous functionAnonymous:
                block = functionAnonymous.Block;
                parameters = functionAnonymous.Parameters;
                break;
            default:
                throw new NotSupportedException("Unknown function type");
        }

        foreach (var statement in block.Statements)
        {
            StatementBuilder.WriteSyntax(ident, statement, parameters, this);
        }
    }
}