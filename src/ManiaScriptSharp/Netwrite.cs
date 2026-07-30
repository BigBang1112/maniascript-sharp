using System.Runtime.CompilerServices;

namespace ManiaScriptSharp;

/// <summary>
/// Declares a <c>netwrite</c> extension variable for the given provider object.
/// Use as <c>Netwrite&lt;T&gt;.For(provider, out var MyVar);</c> which translates to
/// <c>declare netwrite T Net_MyVar for provider;</c> in ManiaScript.
/// </summary>
public static class Netwrite<T> where T : new()
{
    public static void For(INetwriteProvider provider, out StrongBox<T> variable,
#if NET5_0_OR_GREATER
        [CallerArgumentExpression(nameof(variable))]
#endif
        string variableName = "")
    {
        if (provider.NetworkData.TryGetValue(variableName, out var value))
        {
            variable = (StrongBox<T>)value;
            return;
        }

        variable = new StrongBox<T>(new T());
        provider.NetworkData[variableName] = variable;
    }
}
