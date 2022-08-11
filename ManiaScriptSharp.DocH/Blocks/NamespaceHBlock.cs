using ManiaScriptSharp.DocH.Inlines;
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
        () => new PropertyHInline() // should be ConstHInline
    );

    protected override Regex? IdentifierRegex => regex;
    protected override ImmutableArray<Func<HGeneral>> HGenerals => hGenerals;

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
}
