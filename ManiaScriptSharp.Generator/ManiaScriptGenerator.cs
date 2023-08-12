using Microsoft.CodeAnalysis;
using System.Diagnostics;
using System.IO.Abstractions;
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
        
        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.rootnamespace", out string? rootNamespace))
        {
            throw new Exception("build_property.rootNamespace not found");
        }
        
        var wat = context.Compilation.GetCompilationNamespace(context.Compilation.GlobalNamespace);
        
        var xmlSchemaXsd = default(string);
        var buildSettings = default(BuildSettings);

        foreach (var additionalFile in context.AdditionalFiles)
        {
            if (additionalFile.Path.EndsWith(".xsd"))
            {
                xmlSchemaXsd = additionalFile.GetText()?.ToString();
            }

            if (additionalFile.Path.EndsWith("buildsettings.yml") || additionalFile.Path.EndsWith("buildsettings.yaml"))
            {
                var contents = additionalFile.GetText()?.ToString();

                if (contents is null)
                {
                    continue;
                }

                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();
                buildSettings = deserializer.Deserialize<BuildSettings>(contents);
            }
        }
        
        var outputDir = buildSettings?.OutputDir ?? Path.Combine(projectDir, "out");

        var fileSystem = new FileSystem();
        //fileSystem.Directory.Delete(outputDir, true);
        fileSystem.Directory.CreateDirectory(outputDir);
        
        var xmlSchema = xmlSchemaXsd is null ? null : XmlSchema.Read(new StringReader(xmlSchemaXsd), (sender, args) =>
        {
            // HANDLE VALIDATION FAILED
        });

        var helper = new GeneratorHelper(context, fileSystem, projectDir, outputDir, rootNamespace, xmlSchema, buildSettings);
        
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
            var pathList = CreateFilePathFromScriptSymbolInReverse(scriptSymbol, isEmbeddedScript, helper);

            if (helper.BuildSettings?.Packed == true)
            {
                pathList = pathList.Append(helper.RootNamespace);
            }
            
            var path = Path.Combine(pathList.Append(helper.OutputDir).Reverse().ToArray());
            var dirPath = Path.GetDirectoryName(path)!;
            
            helper.FileSystem.Directory.CreateDirectory(dirPath);
            helper.FileSystem.File.WriteAllText(path, content);
        }
    }

    private static IEnumerable<string> CreateFilePathFromScriptSymbolInReverse(ISymbol scriptSymbol, bool isEmbeddedScript, GeneratorHelper helper)
    {
        var namespaces = GenericExtensions.Flatten(scriptSymbol.ContainingNamespace, symbol => symbol.ContainingNamespace);
        var namespaceFolderSymbols = namespaces.Where(x => !x.IsGlobalNamespace)
            .Prepend(scriptSymbol.ContainingNamespace);

        yield return scriptSymbol.Name.TrimStart('_') + (isEmbeddedScript ? ".xml" : ".Script.txt");
        
        foreach (var namespaceSymbol in namespaceFolderSymbols)
        {
            if (namespaceSymbol.ToDisplayString() == helper.RootNamespace)
            {
                yield break;
            }
            
            yield return namespaceSymbol.Name;
        }
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
        var pathList = CreateFilePathFromScriptSymbolInReverse(scriptSymbol, isEmbeddedScript: true, helper)
            .Append(helper.ProjectDir)
            .Reverse()
            .ToArray();
        var xmlPath = Path.Combine(pathList);

        if (!File.Exists(xmlPath))
        {
            throw new Exception("XML is missing for " + scriptSymbol.Name);
        }

        return helper.FileSystem.File.OpenRead(xmlPath);
    }
}