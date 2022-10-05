using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator;

public class ManialinkFile : IGeneratedFile
{
    public static ManialinkFile Generate(string xml, INamedTypeSymbol scriptSymbol, TextWriter writer)
    {
        writer.WriteLine(xml);
        
        var headBuilder = new ManiaScriptHeadBuilder(scriptSymbol, writer, isEmbeddedInManialink: true);
        
        headBuilder.AnalyzeAndBuild();

        return new ManialinkFile();
    }
}