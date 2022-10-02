using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Diagnostics;

namespace ManiaScriptSharp.Generator;

[Generator]
public class ManiaScriptGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        if (false)
        {
            Debugger.Launch();
        }
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.projectdir", out string? projectDir))
        {
            throw new Exception("build_property.projectdir not found");
        }

        var scriptSymbols = context.Compilation
            .GlobalNamespace
            .GetNamespaceMembers()
            .SelectMany(x => x.GetTypeMembers()
                .Where(x => x.Interfaces.Any(x => x.Name == "IContext")))
            .ToList();
        
        foreach (var scriptSymbol in scriptSymbols)
        {
            var isEmbeddedScript = scriptSymbol.IsSubclassOf(x => x.Name == "CMlScript");

            if (isEmbeddedScript)
            {
                var xmlPath = Path.Combine(projectDir, scriptSymbol.Name + ".xml");

                if (!File.Exists(xmlPath))
                {
                    continue;
                }

                var xml = File.ReadAllText(xmlPath);
            }

            var methods = scriptSymbol.GetMembers().OfType<IMethodSymbol>();

            foreach (var method in methods)
            {
                switch (method.Name)
                {
                    case "Main":
                        var methodSyntax = method.DeclaringSyntaxReferences[0].GetSyntax() is MethodDeclarationSyntax mSyntax
                            ? mSyntax : throw new Exception("Main method not found");
                        break;
                }
            }            
        }
    }
}
