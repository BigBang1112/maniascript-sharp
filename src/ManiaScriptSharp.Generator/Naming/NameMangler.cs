using System.Text;
using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator.Naming;

/// <summary>
/// Centralised name-mangling rules for C# → ManiaScript identifiers.
/// All prefix / casing decisions live here so individual emitters never invent names.
/// </summary>
internal static class NameMangler
{
    /// <summary>Strip leading underscores and PascalCase the first letter.</summary>
    public static string PascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var i = 0;
        while (i < name.Length && name[i] == '_') i++;
        if (i >= name.Length) return name;
        var rest = name.Substring(i);
        return char.ToUpperInvariant(rest[0]) + rest.Substring(1);
    }

    public static string Const(IFieldSymbol f) => "C_" + PascalCase(f.Name);

    public static string Setting(IFieldSymbol f) => "S_" + PascalCase(f.Name);

    public static string Net(IFieldSymbol f) => "Net_" + PascalCase(f.Name);

    public static string Persistent(ISymbol s) => "Persistent_" + PascalCase(s.Name);

    /// <summary>Fields emitted as globals always use the <c>G_</c> prefix.</summary>
    public static string Global(IFieldSymbol f) => "G_" + PascalCase(f.Name);

    /// <summary>Property backing variables are emitted as globals and use the <c>G_</c> prefix.</summary>
    public static string Global(IPropertySymbol p) => "G_" + PascalCase(p.Name);

    /// <summary>
    /// Returns a generated struct field's name, respecting a
    /// <c>[JsonPropertyName(...)]</c> annotation on the field before falling back to a
    /// <c>[StructFieldNaming(...)]</c> annotation on its containing struct.
    /// </summary>
    public static string StructField(IFieldSymbol f)
    {
        var jsonName = f.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonPropertyNameAttribute")
            ?.ConstructorArguments.FirstOrDefault().Value as string;
        if (jsonName is not null) return jsonName;

        var style = f.ContainingType?.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass?.ToDisplayString() == "ManiaScriptSharp.StructFieldNamingAttribute")
            ?.ConstructorArguments.FirstOrDefault().Value;

        return style switch
        {
            1 => CamelCase(f.Name),
            2 => SnakeCase(f.Name),
            _ => PascalCase(f.Name),
        };
    }

    public static string Parameter(IParameterSymbol p) => "_" + PascalCase(p.Name);

    /// <summary>Locals (and `var` declarations) are PascalCased per ManiaScript convention.</summary>
    public static string Local(string name) => PascalCase(name);

    private static string CamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var i = 0;
        while (i < name.Length && name[i] == '_') i++;
        if (i >= name.Length) return name;
        var rest = name.Substring(i);
        return char.ToLowerInvariant(rest[0]) + rest.Substring(1);
    }

    private static string SnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var start = 0;
        while (start < name.Length && name[start] == '_') start++;
        if (start >= name.Length) return name;

        var result = new StringBuilder(name.Length + 4);
        for (var i = start; i < name.Length; i++)
        {
            var current = name[i];
            if (current == '_')
            {
                if (result.Length > 0 && result[result.Length - 1] != '_') result.Append('_');
                continue;
            }

            if (char.IsUpper(current) && result.Length > 0)
            {
                var previous = name[i - 1];
                var nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);
                if (char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && nextIsLower))
                    result.Append('_');
            }
            result.Append(char.ToLowerInvariant(current));
        }
        return result.ToString();
    }

    /// <summary>Private methods get a <c>Private_</c> prefix; public/internal don't.</summary>
    public static string Method(IMethodSymbol m)
    {
        var name = m.Name;
        return m.DeclaredAccessibility == Accessibility.Private ? "Private_" + name : name;
    }

    /// <summary>Returns the ManiaScript getter function name for a C# property: <c>Get{Name}</c> or <c>Private_Get{Name}</c>.</summary>
    public static string Getter(IPropertySymbol p)
    {
        var name = "Get" + PascalCase(p.Name);
        return p.DeclaredAccessibility == Accessibility.Private ? "Private_" + name : name;
    }

    /// <summary>Returns the ManiaScript setter function name for a C# property: <c>Set{Name}</c> or <c>Private_Set{Name}</c>.</summary>
    public static string Setter(IPropertySymbol p)
    {
        var name = "Set" + PascalCase(p.Name);
        return p.DeclaredAccessibility == Accessibility.Private ? "Private_" + name : name;
    }
}
