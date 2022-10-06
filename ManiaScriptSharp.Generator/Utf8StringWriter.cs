using System.Text;

namespace ManiaScriptSharp.Generator;

public sealed class Utf8StringWriter : StringWriter
{
    public override Encoding Encoding => Encoding.UTF8;
}