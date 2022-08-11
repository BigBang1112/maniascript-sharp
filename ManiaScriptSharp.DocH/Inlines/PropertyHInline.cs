using System.Text;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp.DocH.Inlines;

public class PropertyHInline : HInline
{
    // cached, in .NET 7 pls generate this via source generation
    private static readonly Regex regex = new(@"(const\s+)?((\w+)::)?([\w|<|>|:]+)\s+(\w+);", RegexOptions.Compiled);

    public override Regex IdentifierRegex => regex;

    protected override void Read(Match match, StringBuilder builder)
    {
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
    }
}
