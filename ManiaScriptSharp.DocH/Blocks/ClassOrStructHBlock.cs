using ManiaScriptSharp.DocH.Inlines;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp.DocH.Blocks;

public class ClassOrStructHBlock : MajorHBlock
{
    // cached, in .NET 7 pls generate this via source generation
    private static readonly Regex regex = new(@"(class|struct)\s(\w+?)\s*?(:\s*?public\s(\w+?)\s*?)?{", RegexOptions.Compiled);

    private static readonly ImmutableArray<Func<HGeneral>> hGenerals = ImmutableArray.Create<Func<HGeneral>>(
        () => new EnumHBlock(),
        () => new IndexerHInline(),
        () => new MethodHInline(isStatic: false),
        () => new PropertyHInline()
    );
    
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

    protected internal override Regex? IdentifierRegex => regex;
    protected internal override ImmutableArray<Func<HGeneral>> HGenerals => hGenerals;

    protected internal override bool BeforeRead(string line, Match? match, StringBuilder builder)
    {
        if (match is null)
        {
            throw new Exception("Match is null in class/struct but it shouldn't be.");
        }

        var nameGroup = match.Groups[2];
        Name = nameGroup.Value;

        if (ignoredClasses.Contains(Name))
        {
            return false;
        }

        builder.Append("public class ");
        builder.Append(Name);

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
}
