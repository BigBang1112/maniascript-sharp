﻿using System.Text;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp.DocH.Inlines;

public class ConstHInline : HInline
{
    // cached, in .NET 7 pls generate this via source generation
    private static readonly Regex regex = new(@"const\s+(\w+)\s+(\w+)\s*=\s*(.*?)\s*;", RegexOptions.Compiled);

    public override Regex IdentifierRegex => regex;

    protected internal override void Read(Match match, StringBuilder builder)
    {
        var type = GetTypeBindOrDefault(match.Groups[1].Value);
        var name = match.Groups[2].Value;
        var value = match.Groups[3].Value;

        builder.Append('\t');

        if (string.Equals(type, name))
        {
            builder.Append("[ActualName(\"");
            builder.Append(name);
            builder.Append("\")] ");
        }

        builder.Append("public const ");
        builder.Append(type);
        builder.Append(' ');
        builder.Append(name);

        if (string.Equals(type, name))
        {
            builder.Append('E');
        }

        builder.Append(" = ");
        
        AppendValueAsCorrectCSharpString(builder, type, value);
        
        builder.AppendLine(";");
    }

    internal static void AppendValueAsCorrectCSharpString(StringBuilder builder, string type, string value)
    {
        if (type == "float")
        {
            builder.Append(value);
            builder.Append('f');
            return;
        }
        
        throw new Exception($"{type} not supported for const");
    }
}
