using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Text;
using System.Xml.Schema;

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
        
        var xmlSchemaXsd = context.AdditionalFiles.FirstOrDefault(x => x.Path.EndsWith(".xsd"))?.GetText()?.ToString();
        
        var xmlSchema = xmlSchemaXsd is null ? null : XmlSchema.Read(new StringReader(xmlSchemaXsd), (sender, args) =>
        {
            // HANDLE VALIDATION FAILED
        });

        var settings = new GeneratorSettings(context, fileSystem, projectDir, outputDir, xmlSchema);

        var scriptSymbols = context.Compilation
            .GlobalNamespace
            .GetNamespaceMembers()
            .SelectMany(x => x.GetTypeMembers()
                .Where(y => y.Interfaces.Any(z => z.Name == "IContext")));
        
        foreach (var scriptSymbol in scriptSymbols)
        {
            try
            {
                var generatedFile = GenerateScriptFile(scriptSymbol, settings);
            }
            catch (Exception e)
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("MSSG003", "File generation failed", $"{e.GetType().Name} ({scriptSymbol.Name}): {e.Message}", "ManiaScriptSharp", DiagnosticSeverity.Error, true), Location.None));
            }
        }
    }

    private static IGeneratedFile GenerateScriptFile(INamedTypeSymbol scriptSymbol, GeneratorSettings settings)
    {
        var isEmbeddedScript = scriptSymbol.IsSubclassOf(x => x.Name == "CMlScript");
        var outputFilePath = Path.Combine(settings.OutputDir, scriptSymbol.Name);

        outputFilePath += isEmbeddedScript ? ".xml" : ".Script.txt";

        using var writer = settings.FileSystem.File.CreateText(outputFilePath);
        
        return GenerateScriptFile(scriptSymbol, writer, isEmbeddedScript, settings);
    }

    private static IGeneratedFile GenerateScriptFile(INamedTypeSymbol scriptSymbol,
                                                     TextWriter writer,
                                                     bool isEmbeddedScript,
                                                     GeneratorSettings settings)
    {
        

        if (!isEmbeddedScript)
        {
            // All regular scripts go here (.Script.txt)
            return ManiaScriptFile.Generate(scriptSymbol, writer);
        }
        
        // Manialink work goes here (.xml)

        _ = settings.ProjectDir ?? throw new InvalidOperationException("ProjectDirectory must be set for manialink builds.");

        var xml = ReadManialinkXml(scriptSymbol, settings);
        
        return ManialinkFile.Generate(xml, scriptSymbol, writer, settings);
    }

    private static string ReadManialinkXml(ISymbol scriptSymbol, GeneratorSettings settings)
    {
        var xmlPath = Path.Combine(settings.ProjectDir, scriptSymbol.Name + ".xml");

        if (!File.Exists(xmlPath))
        {
            throw new Exception("XML is missing for " + scriptSymbol.Name);
        }

        return settings.FileSystem.File.ReadAllText(xmlPath);
    }
}
