using System.Runtime.CompilerServices;

namespace ManiaScriptSharp;

public static class Persistent<T> where T : new()
{
    public static void For(IPersistentProvider provider, out StrongBox<T> variable,
#if NET5_0_OR_GREATER
        [CallerArgumentExpression(nameof(variable))]
#endif
        string variableName = "")
    {
        if (provider.Persistent.TryGetValue(variableName, out var value))
        {
            variable = (StrongBox<T>)value;
            return;
        }

        variable = new StrongBox<T>(new T());
        provider.Persistent[variableName] = variable;
    }
}
