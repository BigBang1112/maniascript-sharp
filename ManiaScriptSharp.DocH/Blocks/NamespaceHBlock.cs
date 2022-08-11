using ManiaScriptSharp.DocH.Inlines;
using System.Text;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp.DocH.Blocks;

public class NamespaceHBlock : HBlock
{
    // cached, in .NET 7 pls generate this via source generation
    private static readonly Regex regex = new(@"namespace\s+(\w+?)\s*{", RegexOptions.Compiled);

    public string? NamespaceName { get; private set; }

    protected override Regex? IdentifierRegex => regex;
    protected override string End => "};";

    protected override bool BeforeRead(string line, Match? match, StringBuilder builder)
    {
        if (match is null)
        {
            throw new Exception("Match is null in namespace but it shouldn't be.");
        }

        var nameGroup = match.Groups[1];

        NamespaceName = nameGroup.Value;

        builder.Append("public static class ");
        builder.Append(NamespaceName);
        builder.AppendLine();
        builder.AppendLine("{");

        return true;
    }

    protected override void BeforeAttemptToEnd(string line, StreamReader reader, StringBuilder builder)
    {
        if (new CommentHBlock(depth: 1).TryRead(line, reader, builder))
        {
            return;
        }
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

    protected override void AfterRead(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("}");
        builder.AppendLine();
    }
}
