using System.Runtime.CompilerServices;

namespace ManiaScriptSharp;

public static class Metadata<T> where T : new()
{
    public static void For(IMetadataProvider provider, out StrongBox<T> variable,
#if NET5_0_OR_GREATER
        [CallerArgumentExpression(nameof(variable))]
#endif
        string variableName = "")
    {
        if (provider.Metadata.TryGetValue(variableName, out var value))
        {
            variable = (StrongBox<T>)value;
        }
        else
        {
            variable = new StrongBox<T>(new T());
            provider.Metadata[variableName] = variable;
        }
    }
}
