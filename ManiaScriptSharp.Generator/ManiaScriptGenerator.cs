using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace ManiaScriptSharp.Generator;

[Generator]
public class ManiaScriptGenerator : ISourceGenerator
{
    private const bool Debug = false;
    
    public void Initialize(GeneratorInitializationContext context)
    {
        if (Debug && !Debugger.IsAttached)
        {
            Debugger.Launch();
        }
        
        context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.projectdir", out string? projectDir))
        {
            throw new Exception("build_property.projectdir not found");
        }
        
        var outputDir = Path.Combine(projectDir, "out");

        var fileSystem = new FileSystem();
        //fileSystem.Directory.Delete(outputDir, true);
        fileSystem.Directory.CreateDirectory(outputDir);
        
        var xmlSchemaXsd = context.AdditionalFiles
            .FirstOrDefault(x => x.Path.EndsWith(".xsd"))?
            .GetText()?
            .ToString();
        
        var xmlSchema = xmlSchemaXsd is null ? null : XmlSchema.Read(new StringReader(xmlSchemaXsd), (sender, args) =>
        {
            // HANDLE VALIDATION FAILED
        });

        var helper = new GeneratorHelper(context, fileSystem, projectDir, outputDir, xmlSchema);
        
        var receiver = (SyntaxReceiver)context.SyntaxReceiver!;

        if (receiver.ClassName is null)
        {
            var scriptSymbols = context.Compilation
                .GlobalNamespace
                .GetNamespaceMembers()
                .Flatten(x => x.GetNamespaceMembers())
                .SelectMany(x => x.GetTypeMembers()
                    .Where(y => y.Interfaces.Any(z => z.Name == "IContext")));

            foreach (var scriptSymbol in scriptSymbols)
            {
                ProcessScriptSymbol(context, scriptSymbol, helper);
            }
        }
        else
        {
            var allTypeSymbols = context.Compilation
                .GlobalNamespace
                .GetNamespaceMembers()
                .Flatten(x => x.GetNamespaceMembers())
                .SelectMany(x => x.GetTypeMembers());
            
            foreach (var typeSymbol in allTypeSymbols)
            {
                if (typeSymbol.Name == receiver.ClassName)
                {
                    ProcessScriptSymbol(context, typeSymbol, helper);
                }
            }
        }
    }

    private static void ProcessScriptSymbol(GeneratorExecutionContext context, INamedTypeSymbol scriptSymbol,
        GeneratorHelper helper)
    {
        try
        {
            var generatedFile = GenerateScriptFile(scriptSymbol, helper);
        }
        catch (XmlSchemaException)
        {
        }
        catch (Exception e)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor("MSSG003", "File generation failed",
                    $"{e.GetType().Name} ({scriptSymbol.Name}): {e.Message}", "ManiaScriptSharp", DiagnosticSeverity.Error,
                    true), Location.None));

            if (Debug)
            {
                throw;
            }
        }
    }

    private static IGeneratedFile GenerateScriptFile(INamedTypeSymbol scriptSymbol, GeneratorHelper helper)
    {
        var isEmbeddedScript = scriptSymbol.IsSubclassOf(x => x.Name == "CMlScript");

        var content = isEmbeddedScript ? "<!-- This generated file is being processed -->" : "// This generated file is being processed";

        using var writer = new Utf8StringWriter();
        
        try
        {
            try
            {
                return GenerateScriptFile(scriptSymbol, writer, isEmbeddedScript, helper);
            }
            finally
            {
                content = writer.ToString();
            }
        }
        catch (Exception ex)
        {
            if (isEmbeddedScript)
            {
                var failedDoc = new XmlDocument();

                var mainElement = failedDoc.CreateElement("manialink");
                mainElement.SetAttribute("version", "3");
                failedDoc.AppendChild(mainElement);
                
                var comment = failedDoc.CreateComment($"This generated file failed to be processed: {ex}");
                mainElement.AppendChild(comment);
                
                using var stringWriter = new Utf8StringWriter();
                using var xmlWriter = new XmlTextWriter(stringWriter)
                {
                    Formatting = Formatting.Indented,
                    Indentation = 4
                };
        
                failedDoc.Save(xmlWriter);
                content = stringWriter.ToString();
            }
            else
            {
                content = "/*\n" +
                          "\t" + ex + "\n" +
                          "*/";
            }
            
            throw;
        }
        finally
        {
            var pathList = CreateFilePathFromScriptSymbol(helper.OutputDir, scriptSymbol, isEmbeddedScript).ToArray();
            var path = Path.Combine(pathList);
            var dirPath = Path.GetDirectoryName(path)!;
            
            helper.FileSystem.Directory.CreateDirectory(dirPath);
            helper.FileSystem.File.WriteAllText(path, content);
        }
    }

    private static IEnumerable<string> CreateFilePathFromScriptSymbol(string outputDir, ISymbol scriptSymbol, bool isEmbeddedScript)
    {
        yield return outputDir;
        
        var namespaces = GenericExtensions.Flatten(scriptSymbol.ContainingNamespace, symbol => symbol.ContainingNamespace);

        foreach (var namespaceSymbol in namespaces.Where(x => !x.IsGlobalNamespace).Reverse())
        {
            yield return namespaceSymbol.Name;
        }
        
        yield return scriptSymbol.ContainingNamespace.Name;
        
        yield return scriptSymbol.Name + (isEmbeddedScript ? ".xml" : ".Script.txt");
    }

    private static IGeneratedFile GenerateScriptFile(INamedTypeSymbol scriptSymbol,
                                                     TextWriter writer,
                                                     bool isEmbeddedScript,
                                                     GeneratorHelper helper)
    {
        if (scriptSymbol.DeclaringSyntaxReferences.Length == 0)
        {
            throw new Exception("No syntax references found");
        }
        
        var semanticModel = helper.Context
            .Compilation
            .GetSemanticModel(scriptSymbol.DeclaringSyntaxReferences[0].SyntaxTree);
        
        if (!isEmbeddedScript)
        {
            // All regular scripts go here (.Script.txt)
            return ManiaScriptFile.Generate(scriptSymbol, semanticModel, writer, helper);
        }
        
        // Manialink work goes here (.xml)

        _ = helper.ProjectDir ?? throw new InvalidOperationException("ProjectDirectory must be set for manialink builds.");

        using var xmlStream = OpenManialinkXmlStream(scriptSymbol, helper);
        
        return ManialinkFile.Generate(xmlStream, scriptSymbol, semanticModel, writer, helper);
    }

    private static Stream OpenManialinkXmlStream(ISymbol scriptSymbol, GeneratorHelper helper)
    {
        var xmlPath = Path.Combine(helper.ProjectDir, scriptSymbol.Name + ".xml");

        if (!File.Exists(xmlPath))
        {
            throw new Exception("XML is missing for " + scriptSymbol.Name);
        }

        return helper.FileSystem.File.OpenRead(xmlPath);
    }
}