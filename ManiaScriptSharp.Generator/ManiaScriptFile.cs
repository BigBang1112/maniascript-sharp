using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator;

public class ManiaScriptFile : IGeneratedFile
{
    public static ManiaScriptFile Generate(INamedTypeSymbol scriptSymbol, TextWriter writer)
    {
        var headBuilder = new ManiaScriptHeadBuilder(scriptSymbol, writer);
        
        headBuilder.AnalyzeAndBuild();

        var methods = scriptSymbol.GetMembers().OfType<IMethodSymbol>();

        foreach (var method in methods)
        {
            switch (method.Name)
            {
                case "Main":
                    var methodSyntax = method.DeclaringSyntaxReferences[0].GetSyntax() is MethodDeclarationSyntax mSyntax
                        ? mSyntax
                        : throw new Exception("Main method not found");
                    break;
            }
        }

        return new ManiaScriptFile();
    }
}