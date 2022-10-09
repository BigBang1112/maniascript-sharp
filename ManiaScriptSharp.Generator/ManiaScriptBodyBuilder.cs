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