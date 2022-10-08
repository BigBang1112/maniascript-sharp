namespace ManiaScriptSharp.Generator;

public static class TextWriterExtensions
{
    public static void WriteIdent(this TextWriter writer, int ident = 1)
    {
        for (var i = 0; i < ident; i++)
        {
            writer.Write('\t');
        }
    }
    
    public static void WriteLine(this TextWriter writer, int ident, string value)
    {
        writer.WriteIdent(ident);
        writer.WriteLine(value);
    }
    
    public static void Write(this TextWriter writer, int ident, char value)
    {
        writer.WriteIdent(ident);
        writer.Write(value);
    }
    
    public static void Write(this TextWriter writer, int ident, string value)
    {
        writer.WriteIdent(ident);
        writer.Write(value);
    }
}