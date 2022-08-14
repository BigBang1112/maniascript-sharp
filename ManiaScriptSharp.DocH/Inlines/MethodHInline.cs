using Microsoft.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp.DocH.Inlines;

internal class MethodHInline : HInline
{
    // cached, in .NET 7 pls generate this via source generation
    private static readonly Regex regex = new(@"((\w+)::)?(\w+)\s+(\w+)\s*\((.*)\)\s*;", RegexOptions.Compiled);

    private readonly bool isStatic;

    public override Regex IdentifierRegex => regex;

    public MethodHInline(SymbolContext? context = null, bool isStatic = false) : base(context)
    {
        this.isStatic = isStatic;
    }

    protected internal override void Read(Match match, StringBuilder builder)
    {
        var typeOwnerGroup = match.Groups[2];
        var returnType = GetTypeBindOrDefault(match.Groups[3].Value);
        var name = GetTypeBindOrDefault(match.Groups[4].Value);
        var parameters = match.Groups[5].Value;

        if (Context?.Symbols.TryGetValue(name, out ISymbol typeSymbol) == true)
        {
            if (typeSymbol is not IMethodSymbol methodSymbol)
            {
                throw new Exception($"Manual symbol '{typeSymbol.Name}' is not a method.");
            }

            ManualSymbol = methodSymbol;
        }

        builder.Append('\t');

        // Weird stuff at Nadeo side
        if (returnType == "void" && (name == "ItemList_Begin" || name == "ActionList_Begin"))
        {
            builder.Append("// ");
        }

        builder.Append("public ");

        if (isStatic)
        {
            builder.Append("static ");
        }

        if (ManualSymbol is not null)
        {
            builder.Append("partial ");
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

        if (ManualSymbol is null)
        {
            builder.Append(") { ");

            if (returnType != "void")
            {
                builder.Append("return default; ");
            }

            builder.AppendLine("}");
        }
        else
        {
            builder.AppendLine(");");
        }
    }
}
