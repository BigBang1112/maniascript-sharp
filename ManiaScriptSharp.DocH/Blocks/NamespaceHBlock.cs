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
    
    private static readonly ImmutableArray<Func<SymbolContext?, HGeneral>> hGenerals = ImmutableArray.Create<Func<SymbolContext?, HGeneral>>(
        context => new EnumHBlock(context),
        context => new MethodHInline(context, isStatic: true),
        context => new ConstHInline(context)
    );

    protected internal override Regex? IdentifierRegex => regex;
    protected internal override ImmutableArray<Func<SymbolContext?, HGeneral>> HGenerals => hGenerals;
    
    public NamespaceHBlock(SymbolContext? context = null) : base(context)
    {
        
    }

    protected internal override bool BeforeRead(string line, Match? match, StringBuilder builder)
    {
        if (match is null)
        {
            throw new Exception("Match is null in namespace but it shouldn't be.");
        }

        Name = match.Groups[1].Value;
        
        if (Context?.Symbols.TryGetValue(Name, out ISymbol symbol) == true && symbol.IsStatic)
        {
            ManualSymbol = (INamedTypeSymbol)symbol;

            var atts = symbol.GetAttributes();

            foreach (var att in atts)
            {
                switch (att.AttributeClass?.Name)
                {
                    case "IgnoreGeneratedAttribute":
                        return false;
                }
            }
        }

        builder.Append("public static ");

        if (ManualSymbol is not null)
        {
            builder.Append("partial ");
        }

        builder.Append("class ");
        builder.AppendLine(Name);
        builder.AppendLine("{");

        return true;
    }
}
