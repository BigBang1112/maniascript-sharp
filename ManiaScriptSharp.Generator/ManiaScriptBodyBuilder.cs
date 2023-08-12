using System.Collections.Immutable;
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

        var hasContextSymbol = ScriptSymbol.AllInterfaces.Any(i => i.Name == "IContext");

        var functionsBuilder = ImmutableArray.CreateBuilder<IMethodSymbol>();
        
        var mainMethodSymbol = default(IMethodSymbol);
        var loopMethodSymbol = default(IMethodSymbol);
        var constructorSymbol = default(IMethodSymbol);

        foreach (var method in methods)
        {
            if (hasContextSymbol)
            {
                switch (method.Name)
                {
                    case "Main":
                        mainMethodSymbol = method;
                        break;
                    case "Loop":
                        loopMethodSymbol = method;
                        break;
                }
            }

            if (method.MethodKind == MethodKind.Constructor)
            {
                constructorSymbol = method;
            }
            else if (method.MethodKind is not MethodKind.PropertyGet and not MethodKind.PropertySet)
            {
                functionsBuilder.Add(method);
            }
        }

        var functions = functionsBuilder.ToImmutable();

        _ = constructorSymbol ?? throw new Exception("Constructor not found");

        var constructorAnalysis = ConstructorAnalysis.Analyze(constructorSymbol, SemanticModel, Helper);
        
        WriteFunctions(functions);

        var indent = functions.Length == 0 ? 0 : 1;

        if (hasContextSymbol)
        {
            _ = mainMethodSymbol ?? throw new Exception("Main method not found");
            _ = loopMethodSymbol ?? throw new Exception("Loop method not found");

            var mainDocBuilder = new DocumentationBuilder(this);
            mainDocBuilder.WriteDocumentation(0, mainMethodSymbol);

            Writer.WriteLine("main() {");

            WriteGlobalInitializers(indent);
            WriteBindingInitializers(indent);

            Writer.WriteLine(indent, "Main();");

            var loopDocBuilder = new DocumentationBuilder(this);
            loopDocBuilder.WriteDocumentation(indent, loopMethodSymbol);

            Writer.WriteLine(indent, "while (True) {");
            WriteLoopContents(indent + 1, functions, constructorAnalysis);
            Writer.WriteLine(indent, "}");

            if (mainMethodSymbol is not null)
            {
                Writer.WriteLine("}");
            }
        }

        return new();
    }

    private void WriteFunctions(ImmutableArray<IMethodSymbol> functions)
    {
        foreach (var functionSymbol in functions)
        {
            if (functionSymbol.IsVirtual)
            {
                if (functionSymbol.DeclaringSyntaxReferences.Length <= 0 ||
                    functionSymbol.DeclaringSyntaxReferences[0].GetSyntax() is not MethodDeclarationSyntax methodSyntax)
                {
                    continue;
                }

                if (methodSyntax.Body?.Statements.Count == 0 || methodSyntax.ExpressionBody is not null)
                {
                    continue;
                }
            }

            var docBuilder = new DocumentationBuilder(this);
            docBuilder.WriteDocumentation(indent: 0, functionSymbol);

            if (functionSymbol.IsVirtual)
            {
                Writer.Write("***");
            }
            else
            {
                Writer.Write(Standardizer.CSharpTypeToManiaScriptType(functionSymbol.ReturnType.Name));
                Writer.Write(' ');
            }

            if (functionSymbol.DeclaredAccessibility == Accessibility.Private)
            {
                Writer.Write("Private_");
            }
            
            Writer.Write(Standardizer.CSharpTypeToManiaScriptType(functionSymbol.Name));

            if (functionSymbol.IsVirtual)
            {
                Writer.WriteLine("***");
                Writer.WriteLine("***");
                WriteFunctionBody(indent: 0, new FunctionIdentifier(functionSymbol));
                Writer.WriteLine("***");
                Writer.WriteLine();
                continue;
            }

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

                Writer.Write(Standardizer.CSharpTypeToManiaScriptType((INamedTypeSymbol) parameter.Type));
                Writer.Write(' ');
                Writer.Write(Standardizer.StandardizeUnderscoreName(parameter.Name));
            }

            Writer.WriteLine(") {");
            WriteFunctionBody(indent: 1, new FunctionIdentifier(functionSymbol));
            Writer.WriteLine("}");
            Writer.WriteLine();
        }
    }

    private void WriteGlobalInitializers(int indent)
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

            Writer.WriteIndent(indent);
            Writer.Write(Standardizer.StandardizeGlobalName(global.Name));
            Writer.Write(" = ");
            Writer.Write(Standardizer.StandardizeName(equalsSyntax.Value.ToString()));
            Writer.WriteLine(";");
        }

        Writer.WriteLine();
    }

    private void WriteBindingInitializers(int indent)
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

            Writer.WriteIndent(indent);

            Writer.Write(binding.Name);
            Writer.Write(" = (Page.GetFirstChild(\"");
            Writer.Write(controlId);
            Writer.Write("\") as ");
            Writer.Write(type.Name);
            Writer.WriteLine(");");
        }

        Writer.WriteLine();
    }

    private void WriteLoopContents(int indent, ImmutableArray<IMethodSymbol> functions,
        ConstructorAnalysis constructorAnalysis)
    {
        Writer.WriteLine(indent, "yield;");

        IsBuildingEventHandling = true;
        var eventForeachBuilder = new EventForeachBuilder(this);
        eventForeachBuilder.Write(indent, functions, constructorAnalysis);
        IsBuildingEventHandling = false;
        
        Writer.WriteLine(indent, "Loop();");
    }

    public void WriteFunctionBody(int indent, Function function)
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
            StatementWriter.WriteSyntax(new(indent, statement, parameters, this));
        }
    }
}