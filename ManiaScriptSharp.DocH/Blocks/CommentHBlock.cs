using System.Text;

namespace ManiaScriptSharp.DocH.Blocks;

public class CommentHBlock : HBlock
{
    private readonly List<string> comments = new();
    private readonly int depth;

    protected override string? Start => "/*!";
    protected override string End => "*/";
    protected override bool UseEmptyLines => true;

    public CommentHBlock(int depth)
    {
        this.depth = depth;
    }

    protected override void ReadLine(string line, StreamReader reader, StringBuilder builder)
    {
        if (line == "*")
        {
            comments.Add("");
            return;
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

    protected override void AfterRead(StringBuilder builder)
    {
        if (comments.Count <= 0)
        {
            return;
        }

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
}
