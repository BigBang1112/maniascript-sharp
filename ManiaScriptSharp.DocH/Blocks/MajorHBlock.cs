using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp.DocH.Blocks;

public abstract class MajorHBlock : HBlock
{
    public string? Name { get; protected set; }

    protected internal override string End => "};";

    protected internal abstract ImmutableArray<Func<SymbolContext?, HGeneral>> HGenerals { get; }
    
    protected internal SymbolContext? InnerContext { get; private set; }

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

    protected internal override bool BeforeRead(string line, Match? match, StringBuilder builder)
    {
        if (ManualSymbol is not null)
        {
            InnerContext = new(ManualSymbol.GetMembers().ToDictionary(x => x.Name));
        }

        return true;
    }

    protected internal override bool ReadLine(string line, StreamReader reader, StringBuilder builder)
    {
        foreach (var func in HGenerals)
        {
            switch (func(InnerContext))
            {
                case HBlock block:
                    if (block.TryRead(line, reader, builder)) return true;
                    break;
                case HInline inline:
                    if (inline.TryRead(line, builder)) return true;
                    break;
            }
        }

        return false;
    }

    protected internal override void AfterRead(StringBuilder builder)
    {
        builder.AppendLine("}");
        builder.AppendLine();
    }
}
