using System.Text;

namespace ManiaScriptSharp.DocH.Blocks;

public abstract class MajorHBlock : HBlock
{
    public string? Name { get; protected set; }

    protected override string End => "};";

    protected override void BeforeAttemptToEnd(string line, StreamReader reader, StringBuilder builder)
    {
        if (new CommentHBlock(depth: 1).TryRead(line, reader, builder))
        {
            return;
        }
    }

    protected override void AfterRead(StringBuilder builder)
    {
        builder.AppendLine("}");
        builder.AppendLine();
    }
}
