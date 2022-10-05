using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Text;

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
        
        var outputDir = Path.Combine(projectDir, "out");

        var fileSystem = new FileSystem();
        fileSystem.Directory.CreateDirectory(outputDir);

        var scriptSymbols = context.Compilation
            .GlobalNamespace
            .GetNamespaceMembers()
            .SelectMany(x => x.GetTypeMembers()
                .Where(y => y.Interfaces.Any(z => z.Name == "IContext")));
        
        foreach (var scriptSymbol in scriptSymbols)
        {
            var generatedFile = GenerateScriptFile(scriptSymbol, fileSystem, projectDir, outputDir);
        }
    }

    private static IGeneratedFile GenerateScriptFile(INamedTypeSymbol scriptSymbol, IFileSystem fileSystem, string projectDir, string outputDir)
    {
        var isEmbeddedScript = scriptSymbol.IsSubclassOf(x => x.Name == "CMlScript");
        var outputFilePath = Path.Combine(outputDir, scriptSymbol.Name);

        outputFilePath += isEmbeddedScript ? ".xml" : ".Script.txt";

        using var writer = fileSystem.File.CreateText(outputFilePath);

        return GenerateScriptFile(scriptSymbol, fileSystem, projectDir, writer, isEmbeddedScript);
    }

    private static IGeneratedFile GenerateScriptFile(INamedTypeSymbol scriptSymbol,
                                                     IFileSystem fileSystem,
                                                     string projectDir,
                                                     TextWriter writer,
                                                     bool isEmbeddedScript)
    {
        

        if (!isEmbeddedScript)
        {
            // All regular scripts go here (.Script.txt)
            return ManiaScriptFile.Generate(scriptSymbol, writer);
        }
        
        // Manialink work goes here (.xml)

        _ = projectDir ?? throw new InvalidOperationException("ProjectDirectory must be set for manialink builds.");

        var xml = ReadManialinkXml(scriptSymbol, fileSystem, projectDir);

        return ManialinkFile.Generate(xml, scriptSymbol, writer);
    }

    private static string ReadManialinkXml(INamedTypeSymbol scriptSymbol, IFileSystem fileSystem, string projectDir)
    {
        var xmlPath = Path.Combine(projectDir, scriptSymbol.Name + ".xml");

        if (!File.Exists(xmlPath))
        {
            throw new Exception("XML is missing for " + scriptSymbol.Name);
        }

        return fileSystem.File.ReadAllText(xmlPath);
    }
}
