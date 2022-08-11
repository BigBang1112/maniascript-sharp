using ManiaScriptSharp.DocH.Inlines;
using System.Text;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp.DocH.Blocks;

public class ClassOrStructHBlock : HBlock
{
    // cached, in .NET 7 pls generate this via source generation
    private static readonly Regex regex = new(@"(class|struct)\s(\w+?)\s*?(:\s*?public\s(\w+?)\s*?)?{", RegexOptions.Compiled);

    private static readonly HashSet<string> ignoredClasses = new(new[]
    {
        "Void",
        "Integer",
        "Real",
        "Boolean",
        "Text",
        "Array",
        "AssociativeArray"
    });

    public string? ClassOrStructName { get; private set; }

    protected override Regex? IdentifierRegex => regex;
    protected override string End => "};";

    protected override bool BeforeRead(string line, Match? match, StringBuilder builder)
    {
        if (match is null)
        {
            throw new Exception("Match is null in class/struct but it shouldn't be.");
        }

        var nameGroup = match.Groups[2];
        ClassOrStructName = nameGroup.Value;

        if (ignoredClasses.Contains(ClassOrStructName))
        {
            return false;
        }

        builder.Append("public class ");
        builder.Append(ClassOrStructName);

        var inheritsNameGroup = match.Groups[4];

        if (inheritsNameGroup.Success)
        {
            builder.Append(" : ");
            builder.Append(inheritsNameGroup.Value);
        }

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

        if (new IndexerHInline().TryRead(line, builder))
        {
            return;
        }

        if (new MethodHInline(isStatic: false).TryRead(line, builder))
        {
            return;
        }

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
