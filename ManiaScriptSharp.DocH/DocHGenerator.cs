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
    
    private readonly char[] enumForbiddenChars = new[]
    {
        '(', ')'
    };

    private string GetTypeBindOrDefault(string type)
    {
        return typeBindDictionary.TryGetValue(type, out string typeBind) ? typeBind : type;
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

        using var reader = File.OpenText(docHFile);

        var sourceCodeBuilder = new StringBuilder("namespace ManiaScriptSharp;\n\n");

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine().Trim();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (TryReadClassOrStruct(line, reader, sourceCodeBuilder))
            {
                continue;
            }
        }

        context.AddSource($"DocH.g.cs", sourceCodeBuilder.ToString());
    }

    private bool TryReadClassOrStruct(string line, StreamReader reader, StringBuilder builder)
    {
        var match = Regex.Match(line, @"(class|struct)\s(\w+?)\s*?(:\s*?public\s(\w+?)\s*?)?{");

        if (!match.Success)
        {
            return false;
        }
        
        var nameGroup = match.Groups[2];

        if (!nameGroup.Success || ignoredClasses.Contains(nameGroup.Value))
        {
            return false;
        }

        builder.Append($"public class ");
        builder.Append(nameGroup.Value);

        var inheritsNameGroup = match.Groups[4];

        if (inheritsNameGroup.Success)
        {
            builder.Append($" : ");
            builder.Append(inheritsNameGroup.Value);
        }

        builder.AppendLine();
        builder.Append('{');
        builder.AppendLine();

        var memberLine = line;

        while (!reader.EndOfStream && memberLine.EndsWith("};") == false)
        {
            memberLine = reader.ReadLine().Trim();

            if (string.IsNullOrWhiteSpace(memberLine))
            {
                continue;
            }

            if (memberLine.EndsWith("};"))
            {
                break;
            }

            if (TryReadEnum(memberLine, reader, builder))
            {
                continue;
            }

            if (TryReadIndexer(memberLine, builder))
            {
                continue;
            }

            if (TryReadMethod(memberLine, builder))
            {
                continue;
            }

            if (TryReadProperty(memberLine, builder))
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
        var match = Regex.Match(line, @"(const\s+)?((\w+)::)?(\w+)\s+(\w+);");

        if (!match.Success)
        {
            return false;
        }
        
        var isReadOnly = match.Groups[1].Success;
        var typeOwnerGroup = match.Groups[3];
        var type = GetTypeBindOrDefault(match.Groups[4].Value);
        var name = match.Groups[5].Value;
        
        builder.Append($"\tpublic ");

        if (typeOwnerGroup.Success)
        {
            builder.Append(typeOwnerGroup.Value);
            builder.Append('.');
        }

        builder.Append(type);
        builder.Append(' ');
        builder.Append(name);
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

        builder.Append($"\tpublic ");
        builder.Append(returnType);
        builder.Append(" this[");
        builder.Append(paramType);
        builder.Append(' ');
        builder.Append(paramName);
        builder.AppendLine("] { get; set; }");

        return true;
    }

    private bool TryReadMethod(string line, StringBuilder builder)
    {
        var match = Regex.Match(line, @"(\w+)\s+(\w+)\s*\((.*)\)\s*;");

        if (!match.Success)
        {
            return false;
        }

        var returnType = GetTypeBindOrDefault(match.Groups[1].Value);
        var name = GetTypeBindOrDefault(match.Groups[2].Value);
        var parameters = match.Groups[3].Value;

        builder.Append($"\tpublic ");
        builder.Append(returnType);
        builder.Append(' ');
        builder.Append(name);
        
        builder.Append('(');

        var paramMatches = Regex.Matches(parameters, @"((\w+)::)?(\w+?)\s+(\w+),?");

        for (var i = 0; i < paramMatches.Count; i++)
        {
            var paramMatch = paramMatches[i];
            var paramTypeOwnerGroup = paramMatch.Groups[2];
            var paramType = GetTypeBindOrDefault(paramMatch.Groups[3].Value);
            var paramName = paramMatch.Groups[4].Value;

            if (paramTypeOwnerGroup.Success)
            {
                builder.Append(paramTypeOwnerGroup.Value);
                builder.Append('.');
            }

            builder.Append(paramType);
            builder.Append(' ');
            builder.Append(paramName);

            if (i < paramMatches.Count - 1)
            {
                builder.Append(", ");
            }
        }
        
        builder.AppendLine(") { }");

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

        var valueLine = line;

        while (!reader.EndOfStream && valueLine.Trim().EndsWith("};") == false)
        {
            valueLine = reader.ReadLine().Trim();

            if (string.IsNullOrWhiteSpace(valueLine))
            {
                continue;
            }

            if (valueLine.EndsWith("};"))
            {
                break;
            }

            builder.Append("\t\t");

            var validValueLineCharArray = default(char[]);

            for (var i = 0; i < valueLine.Length; i++)
            {
                var c = valueLine[i];
                
                if (enumForbiddenChars.Contains(c))
                {
                    validValueLineCharArray ??= valueLine.ToCharArray();
                    validValueLineCharArray[i] = '_';
                }
            }

            if (validValueLineCharArray is null)
            {
                builder.AppendLine(valueLine);
                continue;
            }
            
            builder.Append("[ActualName(\"");
            builder.Append(valueLine.Replace(",", ""));
            builder.Append("\")] ");
            builder.AppendLine(new string(validValueLineCharArray));
        }
        
        builder.AppendLine("\t}");
        builder.AppendLine();

        return true;
    }
}
