using ManiaScriptSharp.DocH.Inlines;
using System.Text;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp.DocH.Blocks;

public class NamespaceHBlock : MajorHBlock
{
    // cached, in .NET 7 pls generate this via source generation
    private static readonly Regex regex = new(@"namespace\s+(\w+?)\s*{", RegexOptions.Compiled);

    protected override Regex? IdentifierRegex => regex;

    protected override bool BeforeRead(string line, Match? match, StringBuilder builder)
    {
        if (match is null)
        {
            throw new Exception("Match is null in namespace but it shouldn't be.");
        }

        var nameGroup = match.Groups[1];

        Name = nameGroup.Value;

        builder.Append("public static class ");
        builder.Append(Name);
        builder.AppendLine();
        builder.AppendLine("{");

        return true;
    }

    protected override void ReadLine(string line, StreamReader reader, StringBuilder builder)
    {
        if (new EnumHBlock().TryRead(line, reader, builder))
        {
            return;
        }

        if (new MethodHInline(isStatic: true).TryRead(line, builder))
        {
            return;
        }

        // should be ConstHInline here
        if (new PropertyHInline().TryRead(line, builder))
        {
            return;
        }
    }
}
