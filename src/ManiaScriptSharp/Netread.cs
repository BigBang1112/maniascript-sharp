namespace ManiaScriptSharp;

/// <summary>
/// Declares a <c>netread</c> extension variable for the given provider object.
/// Use as <c>Netread&lt;T&gt;.For(provider, out var MyVar);</c> which translates to
/// <c>declare netread T Net_MyVar for provider;</c> in ManiaScript.
/// </summary>
public static class Netread<T> where T : new()
{
    public static void For(INetreadProvider provider, out T variable,
#if NET5_0_OR_GREATER
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(variable))]
#endif
        string variableName = "")
    {
        if (provider.NetworkData.TryGetValue(variableName, out var value))
        {
            variable = (T)value;
        }
        else
        {
            variable = new T();
            // provider.NetworkData[variableName] = variable;
        }
    }
}
