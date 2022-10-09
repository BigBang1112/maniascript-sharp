using System.Collections.Immutable;
using System.Xml.Linq;
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
        
        var mainMethodSymbol = default(IMethodSymbol);
        var loopMethodSymbol = default(IMethodSymbol);

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
                    if (method.MethodKind != MethodKind.Constructor && method.MethodKind != MethodKind.PropertyGet &&
                        method.MethodKind != MethodKind.PropertySet) customFunctions.Add(method);
                    break;
            }
        }

        _ = mainMethodSymbol ?? throw new Exception("Main method not found");
        _ = loopMethodSymbol ?? throw new Exception("Loop method not found");

        foreach (var customFunctionSymbol in customFunctions)
        {
            var docBuilder = new DocumentationBuilder(this);
            docBuilder.WriteDocumentation(ident: 0, customFunctionSymbol);

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
            WriteFunctionBody(ident: 1, customFunctionSymbol);
            Writer.WriteLine("}");
            Writer.WriteLine();
        }

        var ident = customFunctions.Count == 0 ? 0 : 1;
        
        var mainDocBuilder = new DocumentationBuilder(this);
        mainDocBuilder.WriteDocumentation(0, mainMethodSymbol);
        
        Writer.WriteLine("main() {");

        WriteMain(ident);
        
        var loopDocBuilder = new DocumentationBuilder(this);
        loopDocBuilder.WriteDocumentation(1, loopMethodSymbol);
        
        WriteLoop(ident);
        
        Writer.WriteLine("}");

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
        
        var mainMethodSyntax = ScriptSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .First(x => x.Name == "Main")
            .DeclaringSyntaxReferences[0]
            .GetSyntax() as MethodDeclarationSyntax;
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

        var eventForeachBuilder = new EventForeachBuilder(this);
        eventForeachBuilder.WriteEventForeach(ident);
    }

    public void WriteFunctionBody(int ident, IMethodSymbol methodSymbol)
    {
        if (methodSymbol.DeclaringSyntaxReferences.Length <= 0 ||
            methodSymbol.DeclaringSyntaxReferences[0].GetSyntax() is not MethodDeclarationSyntax methodSyntax)
        {
            return;
        }

        var body = methodSyntax.Body;
        var statements = body?.Statements;

        /*if (statements is not null)
        {
            foreach (var statement in statements)
            {
                Writer.Write(ident + 2, statement.ToFullString());
            }
        }*/
    }
}