using System.Text;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp.DocH;

public abstract class HInline
{
    private static readonly Dictionary<string, string> typeBindDictionary = new()
    {
        { "Void", "void" },
        { "Integer", "int" },
        { "Real", "float" },
        { "Boolean", "bool" },
        { "Text", "string" },
    };

    public abstract Regex IdentifierRegex { get; }

    public virtual bool TryRead(string line, StringBuilder builder)
    {
        var match = IdentifierRegex.Match(line);

        if (!match.Success)
        {
            return false;
        }

        Read(match, builder);

        return true;
    }

    protected abstract void Read(Match match, StringBuilder builder);

    protected string GetTypeBindOrDefault(string type, bool hasOwner = false)
    {
        if (typeBindDictionary.TryGetValue(type, out string typeBind))
        {
            return typeBind;
        }

        if (hasOwner)
        {
            return type;
        }

        if (type.StartsWith("Array<"))
        {
            return "Array<" + GetTypeBindOrDefault(type.Substring(6, type.Length - 7), false) + ">";
        }

        /*if (type.StartsWith("AssociativeArray<"))
        {
            return "Dictionary<" + GetTypeBindOrDefault(type.Substring(18, type.Length - 19), true) + ", " + GetTypeBindOrDefault(type.Substring(18, type.Length - 19), true) + ">";
        }*/

        return type.Replace("::", "."); // Hack
    }
}
