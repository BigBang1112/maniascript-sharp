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
            .OfType<IMethodSymbol>()
            .Where(m => m.DeclaredAccessibility == Accessibility.Public);
        
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
            Writer.WriteLine("Void Something() { }");
            Writer.WriteLine();
        }

        if (customFunctions.Count == 0)
        {
            WriteMain();
            WriteLoop();
        }
        else
        {
            Writer.WriteLine("main() {");

            WriteMain();
            WriteLoop();

            Writer.WriteLine();
            Writer.WriteLine("}");
        }

        return new();
    }

    private void WriteMain()
    {
        foreach (var binding in Head.Bindings)
        {
            var controlId = binding.GetAttributes()
                .First(x => x.AttributeClass?.Name == "ManialinkControlAttribute")
                .ConstructorArguments[0]
                .Value?
                .ToString();

            if (controlId is null) continue;

            Writer.Write(binding.Name);
            Writer.Write(" = (Page.GetFirstChild(\"");
            Writer.Write(controlId);
            Writer.Write("\") as ");
            Writer.Write(binding.Type.Name);
            Writer.WriteLine(");");
        }
        
        Writer.WriteLine();
    }
    
    private void WriteLoop()
    {
        Writer.WriteLine("while (true) {");
        Writer.WriteLine("    yield;");
        Writer.WriteLine("}");
    }
}