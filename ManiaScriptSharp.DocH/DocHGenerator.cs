using ManiaScriptSharp.DocH.Blocks;
using Microsoft.CodeAnalysis;
using System.Text;

#if DEBUG
using System.Diagnostics;
#endif

namespace ManiaScriptSharp.DocH;

[Generator]
public class DocHGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
#if DEBUG
        if (!Debugger.IsAttached)
        {
            Debugger.Launch();
        }
#endif
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.projectdir", out string? projectDir))
        {
            throw new Exception("Project dir not found.");
        }

        var docHFile = Directory.EnumerateFiles(projectDir, "doc.h", SearchOption.AllDirectories).FirstOrDefault();

        if (docHFile is null)
        {
            return;
        }

        foreach (var sourceFile in BuildSourceCodeFiles(docHFile))
        {
            context.AddSource(sourceFile.FileName, sourceFile.SourceCode.ToString());
        }
    }

    private IEnumerable<SourceCodeFile> BuildSourceCodeFiles(string? docHFile)
    {
        using var reader = File.OpenText(docHFile);

        while (!reader.EndOfStream)
        {
            var sourceCodeFile = BuildSourceCodeFile(reader);

            if (sourceCodeFile is not null)
            {
                yield return sourceCodeFile;
            }
        }
    }

    private SourceCodeFile? BuildSourceCodeFile(StreamReader reader)
    {
        var sourceCodeBuilder = new StringBuilder();
        sourceCodeBuilder.AppendLine("using System.Collections.Generic;");
        sourceCodeBuilder.AppendLine();
        sourceCodeBuilder.AppendLine("namespace ManiaScriptSharp;");
        sourceCodeBuilder.AppendLine();

        var hintName = default(string);

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine().Trim();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            
            if (new CommentHBlock(depth: 0).TryRead(line, reader, sourceCodeBuilder))
            {
                continue;
            }

            var classOrStructHBlock = new ClassOrStructHBlock();

            if (classOrStructHBlock.TryRead(line, reader, sourceCodeBuilder))
            {
                hintName = classOrStructHBlock.ClassOrStructName;
                break;
            }

            var namespaceHBlock = new NamespaceHBlock();

            if (namespaceHBlock.TryRead(line, reader, sourceCodeBuilder))
            {
                hintName = namespaceHBlock.NamespaceName;
                break;
            }
        }

        sourceCodeBuilder.Replace("Array<", "IList<");

        if (hintName is not null)
        {
            return new SourceCodeFile($"{hintName}.g.cs", sourceCodeBuilder);
        }
        
        if (reader.EndOfStream)
        {
            return null;
        }
        
        throw new Exception("Hint name is missing.");
    }
}
