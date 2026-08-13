using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>
/// Produces literal strings (numbers, bool, null, default-value placeholders).
/// Kept separate so multiple emitters can re-use the rules consistently.
/// </summary>
internal sealed class LiteralEmitter
{
    public string OfField(IFieldSymbol f)
    {
        if (f.HasConstantValue) return Format(f.ConstantValue);
        if (f.DeclaringSyntaxReferences.Length > 0 &&
            f.DeclaringSyntaxReferences[0].GetSyntax() is VariableDeclaratorSyntax v &&
            v.Initializer is not null)
        {
            return v.Initializer.Value.ToString();
        }
        return Default(f.Type);
    }

    public string Default(ITypeSymbol t) => t.SpecialType switch
    {
        SpecialType.System_Boolean => "False",
        SpecialType.System_String => "\"\"",
        SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal => "0.",
        SpecialType.System_Void => "",
        SpecialType.None when t.Name == "Ident" => "NullId",
        _ => "0",
    };

    public string Format(object? v) => v switch
    {
        null => "Null",
        bool b => b ? "True" : "False",
        string s => "\"" + Escape(s) + "\"",
        char c => "\"" + c + "\"",
        float or double or decimal => FormatReal(v),
        _ => v.ToString()!,
    };

    private static string FormatReal(object v)
    {
        var s = System.Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        return s.IndexOf('.') >= 0 ? s : s + ".";
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
