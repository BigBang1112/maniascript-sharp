﻿using System.Text;

namespace ManiaScriptSharp.DocH.Blocks;

public class CommentHBlock : HBlock
{
    private readonly int depth;
    
    internal List<string> Comments { get; } = new();

    protected internal override string? Start => "/*!";
    protected internal override string End => "*/";
    protected internal override bool UseEmptyLines => true;

    public CommentHBlock(SymbolContext? context = null, int depth = 0) : base(context)
    {
        this.depth = depth;
    }

    protected internal override bool ReadLine(string line, TextReader reader, StringBuilder builder)
    {
        if (line == "*")
        {
            Comments.Add("");
            return true;
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

        if (Comments.Count > 0 || (line != "" && line != "(undocumented)"))
        {
            Comments.Add(line);
        }
        
        return true;
    }

    protected internal override void AfterRead(StringBuilder builder)
    {
        if (Comments.Count <= 0)
        {
            return;
        }

        for (var i = 0; i < depth; i++)
        {
            builder.Append("\t");
        }

        builder.AppendLine("/// <summary>");

        foreach (var comment in Comments)
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
