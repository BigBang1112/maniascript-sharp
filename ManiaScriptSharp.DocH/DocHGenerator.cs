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
    private readonly Func<HBlock>[] hBlocks = new Func<HBlock>[]
    {
        () => new CommentHBlock(depth: 0),
        () => new ClassOrStructHBlock(),
        () => new NamespaceHBlock()
    };

    public void Initialize(GeneratorInitializationContext context)
    {
#if DEBUG
        if (!Debugger.IsAttached)
        {
            //Debugger.Launch();
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

        var hashset = new HashSet<string>();

        foreach (var sourceFile in BuildSourceCodeFiles(docHFile))
        {
            if (hashset.Contains(sourceFile.FileName))
            {
                continue;
            }
            
            context.AddSource(sourceFile.FileName, sourceFile.SourceCode.ToString());
            hashset.Add(sourceFile.FileName);
        }
    }

    internal IEnumerable<SourceCodeFile> BuildSourceCodeFiles(string? docHFile)
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

    internal SourceCodeFile? BuildSourceCodeFile(StreamReader reader)
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

            foreach (var func in hBlocks)
            {
                var block = func();

                if (!block.TryRead(line, reader, sourceCodeBuilder))
                {
                    continue;
                }
                
                if (block is MajorHBlock majorBlock)
                {
                    hintName = majorBlock.Name;
                }

                break;
            }

            if (hintName is not null)
            {
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
