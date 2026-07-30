using System.Runtime.CompilerServices;

namespace ManiaScriptSharp;

public static class Local<T> where T : new()
{
    public static void For(ILocalProvider provider, out StrongBox<T> variable,
#if NET5_0_OR_GREATER
        [CallerArgumentExpression(nameof(variable))]
#endif
        string variableName = "")
    {
        if (provider.Local.TryGetValue(variableName, out var value))
        {
            variable = (StrongBox<T>)value;
            return;
        }

        variable = new StrongBox<T>(new T());
        provider.Local[variableName] = variable;
    }
}
