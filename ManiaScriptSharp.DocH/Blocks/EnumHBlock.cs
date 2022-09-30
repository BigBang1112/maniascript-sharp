using Microsoft.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ManiaScriptSharp.DocH.Blocks;

public class EnumHBlock : HBlock
{
    // cached, in .NET 7 pls generate this via source generation
    private static readonly Regex regex = new(@"enum\s+(\w+)\s*?{", RegexOptions.Compiled);

    private static readonly HashSet<char> enumForbiddenChars = new(new[]
    {
        '(', ')', ' ', '*'
    });

    protected internal override Regex? IdentifierRegex => regex;
    protected internal override string End => "};";

    public EnumHBlock(SymbolContext? context = null) : base(context)
    {
        
    }

    protected internal override bool BeforeRead(string line, Match? match, StringBuilder builder)
    {
        if (match is null)
        {
            throw new Exception("Match is null in enum but it shouldn't be.");
        }
        
        var enumName = match.Groups[1].Value;

        if (Context?.Symbols.TryGetValue(enumName, out ISymbol symbol) == true)
        {
            ManualSymbol = symbol;
            return false;
        }

        builder.Append("\tpublic enum ");
        builder.AppendLine(enumName);
        builder.AppendLine("\t{");

        return true;
    }

    protected internal override void ReadLine(string line, StreamReader reader, StringBuilder builder)
    {
        builder.Append("\t\t");

        var validValueLineCharArray = default(char[]);
        
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (enumForbiddenChars.Contains(c))
            {
                validValueLineCharArray ??= line.ToCharArray();
                validValueLineCharArray[i] = '_';
            }
        }

        if (validValueLineCharArray is null)
        {
            builder.AppendLine(line);
            return;
        }

        builder.Append("[ActualName(\"");
        builder.Append(line.Replace(",", ""));
        builder.Append("\")] ");
        builder.AppendLine(new string(validValueLineCharArray));
    }

    protected internal override void AfterRead(StringBuilder builder)
    {
        builder.AppendLine("\t}");
        builder.AppendLine();
    }
}
