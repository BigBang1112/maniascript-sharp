using System.Collections.Immutable;
using System.Text;

namespace ManiaScriptSharp.DocH.Blocks;

public abstract class MajorHBlock : HBlock
{
    public string? Name { get; protected set; }

    protected override string End => "};";

    protected abstract ImmutableArray<Func<HGeneral>> HGenerals { get; }

    protected override void BeforeAttemptToEnd(string line, StreamReader reader, StringBuilder builder)
    {
        _ = new CommentHBlock(depth: 1).TryRead(line, reader, builder);
    }

    protected override void ReadLine(string line, StreamReader reader, StringBuilder builder)
    {
        foreach (var func in HGenerals)
        {
            switch (func())
            {
                case HBlock block:
                    block.TryRead(line, reader, builder);
                    break;
                case HInline inline:
                    inline.TryRead(line, builder);
                    break;
            }
        }
    }

    protected override void AfterRead(StringBuilder builder)
    {
        builder.AppendLine("}");
        builder.AppendLine();
    }
}
