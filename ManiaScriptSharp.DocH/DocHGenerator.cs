using Microsoft.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

#if DEBUG
using System.Diagnostics;
#endif

namespace ManiaScriptSharp.DocH;

[Generator]
public class DocHGenerator : ISourceGenerator
{
    private readonly HashSet<string> ignoredClasses = new(new[]
    {
        "Void",
        "Integer",
        "Real",
        "Boolean",
        "Text",
        "Array",
        "AssociativeArray"
    });
    
    private readonly Dictionary<string, string> typeBindDictionary = new()
    {
        { "Void", "void" },
        { "Integer", "int" },
        { "Real", "float" },
        { "Boolean", "bool" },
        { "Text", "string" },
    };

    private readonly HashSet<char> enumForbiddenChars = new(new[]
    {
        '(', ')', ' ', '*'
    });

    private string GetTypeBindOrDefault(string type, bool hasOwner = false)
    {
        if (typeBindDictionary.TryGetValue(type, out string typeBind))
        {
            return typeBind;
        }

        if (hasOwner)
        {
            return type;
        }

        if (type.StartsWith("Array<"))
        {
            return "Array<" + GetTypeBindOrDefault(type.Substring(6, type.Length - 7), false) + ">";
        }
        
        /*if (type.StartsWith("AssociativeArray<"))
        {
            return "Dictionary<" + GetTypeBindOrDefault(type.Substring(18, type.Length - 19), true) + ", " + GetTypeBindOrDefault(type.Substring(18, type.Length - 19), true) + ">";
        }*/

        return type.Replace("::", "."); // Hack
    }

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

                if (TryComment(line, reader, sourceCodeBuilder, depth: 0))
                {
                    continue;
                }

                if (TryReadClassOrStruct(line, reader, sourceCodeBuilder, out hintName))
                {
                    break;
                }

                if (TryReadNamespace(line, reader, sourceCodeBuilder, out hintName))
                {
                    break;
                }
            }

            sourceCodeBuilder.Replace("Array<", "IList<");

            if (hintName is null)
            {
                if (reader.EndOfStream)
                {
                    break;
                }
                else
                {
                    throw new Exception("Hint name is missing.");
                }
            }

            yield return new SourceCodeFile($"{hintName}.g.cs", sourceCodeBuilder);
        }
    }

    private bool TryReadNamespace(string line, StreamReader reader, StringBuilder builder, out string? namespaceName)
    {
        var match = Regex.Match(line, @"namespace\s+(\w+?)\s*{");

        if (!match.Success)
        {
            namespaceName = null;
            return false;
        }

        var nameGroup = match.Groups[1];

        namespaceName = nameGroup.Value;

        builder.Append("public static class ");
        builder.Append(namespaceName);
        builder.AppendLine();
        builder.AppendLine("{");

        while (!reader.EndOfStream && !line.EndsWith("};"))
        {
            line = reader.ReadLine().Trim();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (TryComment(line, reader, builder, depth: 1))
            {
                continue;
            }

            if (line.EndsWith("};"))
            {
                break;
            }

            if (TryReadEnum(line, reader, builder))
            {
                continue;
            }

            if (TryReadMethod(line, builder, isStatic: true))
            {
                continue;
            }

            if (TryReadProperty(line, builder))
            {
                continue;
            }
        }

        builder.AppendLine();
        builder.AppendLine("}");
        builder.AppendLine();

        return true;
    }

    private bool TryReadClassOrStruct(string line, StreamReader reader, StringBuilder builder, out string? classOrStructName)
    {
        var match = Regex.Match(line, @"(class|struct)\s(\w+?)\s*?(:\s*?public\s(\w+?)\s*?)?{");

        if (!match.Success)
        {
            classOrStructName = null;
            return false;
        }
        
        var nameGroup = match.Groups[2];

        if (!nameGroup.Success || ignoredClasses.Contains(nameGroup.Value))
        {
            classOrStructName = null;
            return false;
        }

        classOrStructName = nameGroup.Value;

        builder.Append("public class ");
        builder.Append(classOrStructName);

        var inheritsNameGroup = match.Groups[4];

        if (inheritsNameGroup.Success)
        {
            builder.Append(" : ");
            builder.Append(inheritsNameGroup.Value);
        }

        builder.AppendLine();
        builder.AppendLine("{");

        while (!reader.EndOfStream && !line.EndsWith("};"))
        {
            line = reader.ReadLine().Trim();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (TryComment(line, reader, builder, depth: 1))
            {
                continue;
            }

            if (line.EndsWith("};"))
            {
                break;
            }

            if (TryReadEnum(line, reader, builder))
            {
                continue;
            }

            if (TryReadIndexer(line, builder))
            {
                continue;
            }

            if (TryReadMethod(line, builder, isStatic: false))
            {
                continue;
            }

            if (TryReadProperty(line, builder))
            {
                continue;
            }
        }

        builder.AppendLine();
        builder.AppendLine("}");
        builder.AppendLine();

        return true;
    }

    private bool TryReadProperty(string line, StringBuilder builder)
    {        
        var match = Regex.Match(line, @"(const\s+)?((\w+)::)?([\w|<|>|:]+)\s+(\w+);");

        if (!match.Success)
        {
            return false;
        }
        
        var isReadOnly = match.Groups[1].Success;
        var typeOwnerGroup = match.Groups[3];
        var type = GetTypeBindOrDefault(match.Groups[4].Value, hasOwner: typeOwnerGroup.Success);
        var name = match.Groups[5].Value;

        builder.Append('\t');

        if (string.Equals(type, name))
        {
            builder.Append("[ActualName(\"");
            builder.Append(name);
            builder.Append("\")] ");
        }
        
        builder.Append("public ");

        if (typeOwnerGroup.Success)
        {
            builder.Append(typeOwnerGroup.Value);
            builder.Append('.');
        }

        builder.Append(type);
        builder.Append(' ');
        builder.Append(name);

        if (string.Equals(type, name))
        {
            builder.Append('E');
        }
        
        builder.Append(" { get; ");

        if (!isReadOnly)
        {
            builder.Append("set; ");
        }
        
        builder.AppendLine("}");

        return true;
    }

    private bool TryReadIndexer(string line, StringBuilder builder)
    {        
        var match = Regex.Match(line, @"(\w+?)\soperator\s*?\[\s*?\]\s*?\(\s*(\w+?)\s(\w+?)\s*\);");

        if (!match.Success)
        {
            return false;
        }

        var returnType = GetTypeBindOrDefault(match.Groups[1].Value);
        var paramType = GetTypeBindOrDefault(match.Groups[2].Value);
        var paramName = match.Groups[3].Value;

        builder.Append("\tpublic ");
        builder.Append(returnType);
        builder.Append(" this[");
        builder.Append(paramType);
        builder.Append(' ');
        builder.Append(paramName);
        builder.AppendLine("] { get; set; }");

        return true;
    }

    private bool TryReadMethod(string line, StringBuilder builder, bool isStatic)
    {
        var match = Regex.Match(line, @"((\w+)::)?(\w+)\s+(\w+)\s*\((.*)\)\s*;");

        if (!match.Success)
        {
            return false;
        }

        var typeOwnerGroup = match.Groups[2];
        var returnType = GetTypeBindOrDefault(match.Groups[3].Value);
        var name = GetTypeBindOrDefault(match.Groups[4].Value);
        var parameters = match.Groups[5].Value;

        // Weird stuff at Nadeo side
        if (returnType == "void" && (name == "ItemList_Begin" || name == "ActionList_Begin"))
        {
            builder.Append("// ");
        }

        builder.Append("\tpublic ");

        if (isStatic)
        {
            builder.Append("static ");
        }

        if (typeOwnerGroup.Success)
        {
            builder.Append(typeOwnerGroup.Value);
            builder.Append('.');
        }

        builder.Append(returnType);
        builder.Append(' ');
        builder.Append(name);
        
        builder.Append('(');

        var paramMatches = Regex.Matches(parameters, @"((\w+)::)?(\w+?)\s+(\w+),?");

        var alreadyUsedNames = new List<string>();

        for (var i = 0; i < paramMatches.Count; i++)
        {
            var paramMatch = paramMatches[i];
            var paramTypeOwnerGroup = paramMatch.Groups[2];
            var paramType = GetTypeBindOrDefault(paramMatch.Groups[3].Value);
            var paramName = paramMatch.Groups[4].Value;

            var alreadyUsed = alreadyUsedNames.Contains(paramName);

            if (alreadyUsed)
            {
                builder.Append("[ActualName(\"");
                builder.Append(paramName);
                builder.Append("\")] ");
            }

            if (paramTypeOwnerGroup.Success)
            {
                builder.Append(paramTypeOwnerGroup.Value);
                builder.Append('.');
            }

            builder.Append(paramType);
            builder.Append(' ');
            builder.Append(paramName);

            if (alreadyUsed)
            {
                builder.Append(i + 1);
            }
            else
            {
                alreadyUsedNames.Add(paramName);
            }

            if (i < paramMatches.Count - 1)
            {
                builder.Append(", ");
            }
        }
        
        builder.Append(") { ");

        if (returnType != "void")
        {
            builder.Append("return default; ");
        }

        builder.AppendLine("}");

        return true;
    }

    private bool TryReadEnum(string line, StreamReader reader, StringBuilder builder)
    {        
        var match = Regex.Match(line, @"enum\s+(\w+)\s*?{");

        if (!match.Success)
        {
            return false;
        }

        var enumName = match.Groups[1].Value;

        builder.Append("\tpublic enum ");
        builder.AppendLine(enumName);
        builder.AppendLine("\t{");

        while (!reader.EndOfStream && !line.EndsWith("};"))
        {
            line = reader.ReadLine().Trim();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.EndsWith("};"))
            {
                break;
            }

            builder.Append("\t\t");

            var validValueLineCharArray = default(char[]);

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                
                if (enumForbiddenChars.Contains(c))
                {
                    validValueLineCharArray ??= line.ToCharArray();
                    validValueLineCharArray[i] = '_';
                }
            }

            if (validValueLineCharArray is null)
            {
                builder.AppendLine(line);
                continue;
            }
            
            builder.Append("[ActualName(\"");
            builder.Append(line.Replace(",", ""));
            builder.Append("\")] ");
            builder.AppendLine(new string(validValueLineCharArray));
        }
        
        builder.AppendLine("\t}");
        builder.AppendLine();

        return true;
    }

    private bool TryComment(string line, StreamReader reader, StringBuilder builder, int depth)
    {
        if (!line.StartsWith("/*!"))
        {
            return false;
        }

        var comments = new List<string>();

        while (!reader.EndOfStream && !line.EndsWith("*/"))
        {
            line = reader.ReadLine().Trim();
            
            if (line.EndsWith("*/"))
            {
                break;
            }

            if (line == "*")
            {
                comments.Add("");
                continue;
            }

            if (line.StartsWith("* \\brief"))
            {
                line = line.Substring(8);
            }
            else if (line.StartsWith("*"))
            {
                line = line.Substring(2);
            }

            line = line.TrimStart();

            if (comments.Count > 0 || line != "")
            {
                comments.Add(line);
            }
        }

        if (comments.Count > 0)
        {
            for (var i = 0; i < depth; i++)
            {
                builder.Append("\t");
            }

            builder.AppendLine("/// <summary>");

            foreach (var comment in comments)
            {
                for (var i = 0; i < depth; i++)
                {
                    builder.Append("\t");
                }
                
                builder.Append("/// ");
                builder.AppendLine(comment);
            }

            for (var i = 0; i < depth; i++)
            {
                builder.Append("\t");
            }
            
            builder.AppendLine("/// </summary>");
        }

        return true;
    }
}
