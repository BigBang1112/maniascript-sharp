using Microsoft.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp.DocH.Inlines;

public class PropertyHInline : HInline
{
    // cached, in .NET 7 pls generate this via source generation
    private static readonly Regex regex = new(@"(const\s+)?((\w+)::)?([\w|<|>|:]+)(\[\])?\s+(\w+);", RegexOptions.Compiled);

    public override Regex IdentifierRegex => regex;

    public PropertyHInline(SymbolContext? context = null) : base(context)
    {

    }

    protected internal override void Read(Match match, StringBuilder builder)
    {
        var isReadOnly = match.Groups[1].Success;
        var typeOwnerGroup = match.Groups[3];
        var type = GetTypeBindOrDefault(match.Groups[4].Value, hasOwner: typeOwnerGroup.Success);
        var isArray = match.Groups[5].Success;
        var name = match.Groups[6].Value;

        if (Context?.Symbols.TryGetValue(name, out ISymbol typeSymbol) == true)
        {
            ManualSymbol = typeSymbol;
        }

        builder.Append('\t');

        if (string.Equals(type, name))
        {
            builder.Append("[ActualName(\"");
            builder.Append(name);
            builder.Append("\")] ");
        }

        builder.Append("public ");

        if (isArray)
        {
            builder.Append("IList<");
        }

        if (typeOwnerGroup.Success)
        {
            builder.Append(typeOwnerGroup.Value);
            builder.Append('.');
        }

        builder.Append(type);

        if (isArray)
        {
            builder.Append("> ");
        }
        else
        {
            builder.Append(' ');
        }
        
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
