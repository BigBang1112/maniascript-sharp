using System.Runtime.CompilerServices;

namespace ManiaScriptSharp;

/// <summary>
/// Declares a metadata extension variable for the given provider object.
/// Use as <c>Metadata&lt;T&gt;.For(provider, out var MyVar);</c> which translates to
/// <c>declare metadata T Metadata_MyVar for provider;</c> in ManiaScript. Pass
/// <c>name:</c> to declare it under an explicit object-side name instead, with the out
/// variable as the script-side alias: <c>For(provider, out var MyVar, name: "MyLib_MyVar")</c>
/// translates to <c>declare metadata T MyLib_MyVar as MyVar for provider;</c>.
/// </summary>
public static class Metadata<T> where T : new()
{
    public static void For(IMetadataProvider provider, out StrongBox<T> variable,
#if NET5_0_OR_GREATER
        [CallerArgumentExpression(nameof(variable))]
#endif
        string name = "")
    {
        if (provider.Metadata.TryGetValue(name, out var value))
        {
            variable = (StrongBox<T>)value;
        }
        else
        {
            variable = new StrongBox<T>(new T());
            provider.Metadata[name] = variable;
        }
    }
}
