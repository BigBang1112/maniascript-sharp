using ManiaScriptSharp.DocH.Inlines;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp.DocH.Blocks;

public class ClassOrStructHBlock : MajorHBlock
{
    // cached, in .NET 7 pls generate this via source generation
    private static readonly Regex regex = new(@"(class|struct)\s(\w+?)\s*?(:\s*?public\s(\w+?)\s*?)?{", RegexOptions.Compiled);

    private static readonly ImmutableArray<Func<SymbolContext?, HGeneral>> hGenerals = ImmutableArray.Create<Func<SymbolContext?, HGeneral>>(
        context => new EnumHBlock(context),
        context => new IndexerHInline(context),
        context => new MethodHInline(context, isStatic: false),
        context => new PropertyHInline(context)
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
    protected internal override ImmutableArray<Func<SymbolContext?, HGeneral>> HGenerals => hGenerals;

    public ClassOrStructHBlock(SymbolContext? context = null) : base(context)
    {

    }

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

        if (Context?.Symbols.TryGetValue(Name, out ISymbol symbol) == true)
        {
            ManualSymbol = (INamedTypeSymbol)symbol;
        }

        builder.Append("public ");

        if (ManualSymbol is not null)
        {
            builder.Append("partial ");
        }
        
        builder.Append("class ");
        builder.Append(Name);

        var inheritsNameGroup = match.Groups[4];

        if (inheritsNameGroup.Success)
        {
            var inheritsName = inheritsNameGroup.Value;

            if (string.Equals(Name, inheritsName))
            {
                // show warning
            }
            else
            {
                builder.Append(" : ");
                builder.Append(inheritsNameGroup.Value);
            }
        }

        builder.AppendLine();
        builder.AppendLine("{");
        builder.Append("\tprotected internal ");
        builder.Append(Name);
        builder.AppendLine("() { }");
        builder.AppendLine();

        return true;
    }
}
