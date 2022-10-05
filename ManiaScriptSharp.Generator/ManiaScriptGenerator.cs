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

        var outputDir = @""; // E:\Temp\ManiaScriptSharp

        var scriptSymbols = context.Compilation
            .GlobalNamespace
            .GetNamespaceMembers()
            .SelectMany(x => x.GetTypeMembers()
                .Where(y => y.Interfaces.Any(z => z.Name == "IContext")))
            .ToList();
        
        var fileSystem = new FileSystem();
        
        foreach (var scriptSymbol in scriptSymbols)
        {
            var generatedFile = GenerateScriptFile(scriptSymbol, fileSystem, projectDir, outputDir);
        }
    }

    private static IGeneratedFile GenerateScriptFile(INamedTypeSymbol scriptSymbol, IFileSystem fileSystem, string projectDir, string outputDir)
    {
        var outputFilePath = Path.Combine(outputDir, scriptSymbol.Name);
        var isInMemory = string.IsNullOrWhiteSpace(outputDir);
        var isEmbeddedScript = scriptSymbol.IsSubclassOf(x => x.Name == "CMlScript");

        if (!isEmbeddedScript)
        {
            // All regular scripts go here (.Script.txt)

            if (isInMemory)
            {
                using var scriptInMemoryWriter = new StringWriter();
                
                return ManiaScriptFile.Generate(scriptSymbol, scriptInMemoryWriter);
            }
            
            using var scriptWriter = fileSystem.File.CreateText(outputFilePath + ".Script.txt");
            
            return ManiaScriptFile.Generate(scriptSymbol, scriptWriter);
        }
        
        // Manialink work goes here (.xml)

        _ = projectDir ?? throw new InvalidOperationException("ProjectDirectory must be set for manialink builds.");

        var xml = ReadManialinkXml(scriptSymbol, fileSystem, projectDir);
        
        if (isInMemory)
        {
            using var manialinkInMemoryWriter = new StringWriter();
            
            return ManialinkFile.Generate(xml, scriptSymbol, manialinkInMemoryWriter);
        }

        using var manialinkWriter = fileSystem.File.CreateText(outputFilePath + ".xml");

        return ManialinkFile.Generate(xml, scriptSymbol, manialinkWriter);
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
