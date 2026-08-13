using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator.Emission;

internal static class SymbolExtensions
{
    public static AttributeData? GetAttr(this ISymbol s, string name)
        => s.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == name);

    public static bool HasAttr(this ISymbol s, string name) => s.GetAttr(name) is not null;

    /// <summary>Read a named argument from an attribute, with a default when absent.</summary>
    public static T? Named<T>(this AttributeData a, string name, T? def = default)
    {
        var arg = a.NamedArguments.FirstOrDefault(x => x.Key == name);
        if (arg.Key is null) return def;
        if (arg.Value.Value is T t) return t;
        return def;
    }

    public static T? Ctor<T>(this AttributeData a, int index, T? def = default)
    {
        if (a.ConstructorArguments.Length <= index) return def;
        if (a.ConstructorArguments[index].Value is T t) return t;
        return def;
    }

    /// <summary>Returns <see langword="true"/> when the field's type implements <c>ILib</c> (a ManiaScript library).</summary>
    public static bool IsLibImplementation(this IFieldSymbol f)
    {
        if (f.Type is not INamedTypeSymbol namedType) return false;
        return namedType.AllInterfaces.Any(static i =>
            i.Name == "ILib"
            && i.ContainingNamespace?.ToDisplayString() == "ManiaScriptSharp");
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="p"/> is the <c>Context</c> property
    /// from <c>ILib&lt;T&gt;</c>, either defined on the interface itself or as an implementation on a class.
    /// </summary>
    public static bool IsLibContextProperty(this IPropertySymbol p)
    {
        if (p.Name != "Context") return false;
        var ct = p.ContainingType;
        if (ct is { IsGenericType: true, Name: "ILib" }
            && ct.ContainingNamespace?.ToDisplayString() == "ManiaScriptSharp")
            return true;
        return ct.AllInterfaces.Any(static i =>
            i.IsGenericType && i.Name == "ILib"
            && i.ContainingNamespace?.ToDisplayString() == "ManiaScriptSharp");
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="type"/> is derived from <c>CNod</c>
    /// (an API-generated class that cannot be instantiated in ManiaScript).
    /// </summary>
    public static bool IsCNodeDerived(this INamedTypeSymbol type)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            if (current.Name == "CNod") return true;
            current = current.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="m"/> implements <c>IContext.Main()</c>
    /// or <c>IContext.Loop()</c> — the two entry points invoked only by the generated
    /// <c>main()</c> wrapper, never by user code.
    /// </summary>
    public static bool IsIContextEntryPoint(this IMethodSymbol? m)
    {
        if (m is null || m.Name is not ("Main" or "Loop") || m.Parameters.Length != 0) return false;
        var iface = m.ContainingType?.AllInterfaces.FirstOrDefault(static i =>
            i.Name == "IContext" && i.ContainingNamespace?.ToDisplayString() == "ManiaScriptSharp");
        var ifaceMethod = iface?.GetMembers(m.Name).OfType<IMethodSymbol>().FirstOrDefault();
        if (ifaceMethod is null) return false;
        return SymbolEqualityComparer.Default.Equals(
            m.ContainingType!.FindImplementationForInterfaceMember(ifaceMethod), m);
    }
}
