using ManiaScriptSharp.DocH.Inlines;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp.DocH.Blocks;

public class NamespaceHBlock : MajorHBlock
{
    // cached, in .NET 7 pls generate this via source generation
    private static readonly Regex regex = new(@"namespace\s+(\w+?)\s*{", RegexOptions.Compiled);
    
    private static readonly ImmutableArray<Func<HGeneral>> hGenerals = ImmutableArray.Create<Func<HGeneral>>(
        () => new EnumHBlock(),
        () => new MethodHInline(isStatic: true),
        () => new ConstHInline()
    );

    protected internal override Regex? IdentifierRegex => regex;
    protected internal override ImmutableArray<Func<HGeneral>> HGenerals => hGenerals;

    public NamespaceHBlock(CodeContext? context = null) : base(context)
    {
        
    }

    protected internal override bool BeforeRead(string line, Match? match, StringBuilder builder)
    {
        if (match is null)
        {
            throw new Exception("Match is null in namespace but it shouldn't be.");
        }

        Name = match.Groups[1].Value;
        
        if (Context?.Types.TryGetValue(Name, out INamedTypeSymbol typeSymbol) == true)
        {
            ManualTypeSymbol = typeSymbol;

            var atts = typeSymbol.GetAttributes();

            foreach (var att in atts)
            {
                switch (att.AttributeClass?.Name)
                {
                    case "IgnoreGeneratedAttribute":
                        return false;
                }
            }
        }

        builder.Append("public static class ");
        builder.Append(Name);
        builder.AppendLine();
        builder.AppendLine("{");

        return true;
    }
}
