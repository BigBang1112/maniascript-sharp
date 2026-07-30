using System.Text;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>
/// Indented text buffer shared by every emitter. Indent character and size are configurable.
/// </summary>
internal sealed class IndentedWriter
{
    private readonly StringBuilder _sb = new();
    private readonly char _indentChar;
    private readonly int _indentSize;
    private int _indent;

    public IndentedWriter(bool useSpaces = false, int indentSize = 1)
    {
        _indentChar = useSpaces ? ' ' : '\t';
        _indentSize = indentSize > 0 ? indentSize : 1;
    }

    public int Indent => _indent;
    public void Push() => _indent++;
    public void Pop() { if (_indent > 0) _indent--; }

    /// <summary>Append a line with current indentation. Blank lines are written without leading whitespace.</summary>
    public void Line(string text = "")
    {
        if (text.Length > 0)
            _sb.Append(_indentChar, _indent * _indentSize);
        _sb.AppendLine(text);
    }

    /// <summary>Append text without trailing newline (rarely needed).</summary>
    public void Raw(string text) => _sb.Append(text);

    public override string ToString() => _sb.ToString();
}
