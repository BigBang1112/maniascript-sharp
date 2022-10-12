using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator;

public class ManiaScriptFile : IGeneratedFile
{
    public static ManiaScriptFile Generate(INamedTypeSymbol scriptSymbol, SemanticModel semanticModel,
        TextWriter writer, GeneratorHelper helper)
    {
        var headBuilder = new ManiaScriptHeadBuilder(scriptSymbol, semanticModel, writer, helper);
        var head = headBuilder.AnalyzeAndBuild();
        
        var bodyBuilder = new ManiaScriptBodyBuilder(scriptSymbol, semanticModel, writer, head, helper);
        var body = bodyBuilder.AnalyzeAndBuild();

        return new ManiaScriptFile();
    }
}