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
                .SelectMany(x => x.GetTypeMembers()
                    .Where(y => y.Interfaces.Any(z => z.Name == "IContext")));

            foreach (var scriptSymbol in scriptSymbols)
            {
                ProcessScriptSymbol(context, scriptSymbol, helper);
            }
        }
        else
        {
            foreach (var namespaceSymbol in context.Compilation.GlobalNamespace.GetNamespaceMembers())
            {
                foreach (var typeSymbol in namespaceSymbol.GetTypeMembers())
                {
                    if (typeSymbol.Name == receiver.ClassName)
                    {
                        ProcessScriptSymbol(context, typeSymbol, helper);
                    }
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
        var outputFilePath = Path.Combine(helper.OutputDir, scriptSymbol.Name);

        outputFilePath += isEmbeddedScript ? ".xml" : ".Script.txt";

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
                content = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                          "<!--\n" +
                          ex + "\n" +
                          "-->";
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
            helper.FileSystem.File.WriteAllText(outputFilePath, content);
        }
    }

    private static IGeneratedFile GenerateScriptFile(INamedTypeSymbol scriptSymbol,
                                                     TextWriter writer,
                                                     bool isEmbeddedScript,
                                                     GeneratorHelper helper)
    {
        if (!isEmbeddedScript)
        {
            // All regular scripts go here (.Script.txt)
            return ManiaScriptFile.Generate(scriptSymbol, writer, helper);
        }
        
        // Manialink work goes here (.xml)

        _ = helper.ProjectDir ?? throw new InvalidOperationException("ProjectDirectory must be set for manialink builds.");

        using var xmlStream = OpenManialinkXmlStream(scriptSymbol, helper);
        
        return ManialinkFile.Generate(xmlStream, scriptSymbol, writer, helper);
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