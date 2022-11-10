namespace ManiaScriptSharp.Generator;

public static class TextWriterExtensions
{
    public static void WriteIndent(this TextWriter writer, int indent = 1)
    {
        for (var i = 0; i < indent; i++)
        {
            writer.Write('\t');
        }
    }
    
    public static void WriteLine(this TextWriter writer, int indent, string value)
    {
        writer.WriteIndent(indent);
        writer.WriteLine(value);
    }
    
    public static void Write(this TextWriter writer, int indent, char value)
    {
        writer.WriteIndent(indent);
        writer.Write(value);
    }
    
    public static void Write(this TextWriter writer, int indent, string value)
    {
        writer.WriteIndent(indent);
        writer.Write(value);
    }
}