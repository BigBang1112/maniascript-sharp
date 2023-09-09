using System.Text;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp.DocH;

public abstract class HInline : HGeneral
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

    public HInline(SymbolContext? context) : base(context)
    {

    }

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

    protected internal abstract void Read(Match match, StringBuilder builder);

    protected static internal string GetTypeBindOrDefault(string type, bool hasOwner = false)
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

        if (type.EndsWith("]"))
        {
            return GetTypeBindOrDefault(type.Substring(0, type.IndexOf('[')), false) + "[]";
        }

        /*if (type.StartsWith("AssociativeArray<"))
        {
            return "Dictionary<" + GetTypeBindOrDefault(type.Substring(18, type.Length - 19), true) + ", " + GetTypeBindOrDefault(type.Substring(18, type.Length - 19), true) + ">";
        }*/

        return type.Replace("::", "."); // Hack
    }
}
