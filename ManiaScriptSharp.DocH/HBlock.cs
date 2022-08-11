using System.Text;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp.DocH;

public abstract class HBlock : HGeneral
{
    protected virtual Regex? IdentifierRegex { get; } = null;
    protected virtual string? Start { get; } = null;
    protected abstract string End { get; }
    protected virtual bool UseEmptyLines { get; }

    public bool TryRead(string line, StreamReader reader, StringBuilder builder)
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

    protected virtual bool BeforeRead(string line, Match? match, StringBuilder builder)
    {
        return true;
    }

    protected virtual void Read(string line, StreamReader reader, StringBuilder builder)
    {
        if (line.EndsWith(End))
        {
            return;
        }
        
        while (!reader.EndOfStream)
        {
            line = reader.ReadLine().Trim();

            if (!UseEmptyLines && string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            BeforeAttemptToEnd(line, reader, builder);

            if (line.EndsWith(End))
            {
                break;
            }

            ReadLine(line, reader, builder);
        }
    }

    protected virtual void BeforeAttemptToEnd(string line, StreamReader reader, StringBuilder builder)
    {

    }

    protected abstract void ReadLine(string line, StreamReader reader, StringBuilder builder);

    protected virtual void AfterRead(StringBuilder builder)
    {

    }
}
