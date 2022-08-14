using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;

namespace ManiaScriptSharp.DocH.Blocks;

public abstract class MajorHBlock : HBlock
{
    public string? Name { get; protected set; }

    protected internal override string End => "};";

    protected internal abstract ImmutableArray<Func<SymbolContext?, HGeneral>> HGenerals { get; }

    protected internal new INamedTypeSymbol? ManualSymbol
    {
        get => base.ManualSymbol as INamedTypeSymbol;
        set => base.ManualSymbol = value;
    }

    public MajorHBlock(SymbolContext? context = null) : base(context)
    {

    }

    protected internal override void BeforeAttemptToEnd(string line, StreamReader reader, StringBuilder builder)
    {
        _ = new CommentHBlock(Context, depth: 1).TryRead(line, reader, builder);
    }

    protected internal override void ReadLine(string line, StreamReader reader, StringBuilder builder)
    {
        var context = default(SymbolContext);

        if (ManualSymbol is not null)
        {
            context = new(ManualSymbol.GetMembers().ToDictionary(x => x.Name));
        }

        foreach (var func in HGenerals)
        {
            switch (func(context))
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

    protected internal override void AfterRead(StringBuilder builder)
    {
        builder.AppendLine("}");
        builder.AppendLine();
    }
}
