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
}