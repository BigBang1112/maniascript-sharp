using System.Text;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp.DocH.Inlines;

public class IndexerHInline : HInline
{
    // cached, in .NET 7 pls generate this via source generation
    private static readonly Regex regex = new(@"(\w+?)\soperator\s*?\[\s*?\]\s*?\(\s*(\w+?)\s(\w+?)\s*\);", RegexOptions.Compiled);

    public override Regex IdentifierRegex => regex;

    protected internal override void Read(Match match, StringBuilder builder)
    {
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
    }
}
