using System.Text;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp.DocH;

public abstract class HBlock : HGeneral
{
    protected internal virtual Regex? IdentifierRegex => null;
    protected internal virtual string? Start => null;
    protected internal abstract string End { get; }
    protected internal virtual bool UseEmptyLines { get; }

    public HBlock(SymbolContext? context) : base(context)
    {

    }

    public bool TryRead(string line, TextReader reader, StringBuilder builder)
    {
        if (Start is not null && !line.StartsWith(Start))
        {
            return false;
        }

        var match = IdentifierRegex?.Match(line);

        if (match is not null && !match.Success)
        {
            return false;
        }

        if (!BeforeRead(line, match, builder))
        {
            return false;
        }

        Read(line, reader, builder);

        AfterRead(builder);

        return true;
    }

    protected internal virtual bool BeforeRead(string line, Match? match, StringBuilder builder)
    {
        return true;
    }

    protected internal virtual void Read(string line, TextReader reader, StringBuilder builder)
    {
        if (line.EndsWith(End))
        {
            return;
        }
        
        while (true)
        {
            var lineRead = reader.ReadLine();

            if (lineRead is null)
            {
                return;
            }

            line = lineRead.Trim();

            if (!UseEmptyLines && string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            BeforeAttemptToEnd(line, reader, builder);

            if (line.EndsWith(End))
            {
                break;
            }

            _ = ReadLine(line, reader, builder);
        }
    }

    protected internal virtual void BeforeAttemptToEnd(string line, TextReader reader, StringBuilder builder)
    {

    }

    protected internal abstract bool ReadLine(string line, TextReader reader, StringBuilder builder);

    protected internal virtual void AfterRead(StringBuilder builder)
    {

    }
}
