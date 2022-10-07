using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator;

public class ManiaScriptFile : IGeneratedFile
{
    public static ManiaScriptFile Generate(INamedTypeSymbol scriptSymbol, TextWriter writer, GeneratorSettings settings)
    {
        var headBuilder = new ManiaScriptHeadBuilder(scriptSymbol, writer, settings);
        var head = headBuilder.AnalyzeAndBuild();
        
        var bodyBuilder = new ManiaScriptBodyBuilder(scriptSymbol, writer, head);
        var body = bodyBuilder.AnalyzeAndBuild();

        return new ManiaScriptFile();
    }
}