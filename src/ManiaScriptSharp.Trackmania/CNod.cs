#if NET6_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif

namespace ManiaScriptSharp;

public partial class CNod
{
    private readonly Dictionary<string, object?> changeValues = [];

    protected void OnChange<T>(T value, ChangeCallback<T> callback,
#if NET6_0_OR_GREATER
        [CallerArgumentExpression(nameof(value))]
#endif
    string valueExpression = "")
    {
        if (string.IsNullOrEmpty(valueExpression))
        {
            throw new ArgumentException("Expression cannot be null or empty", nameof(valueExpression));
        }

        if (!changeValues.TryGetValue(valueExpression, out var oldValue))
        {
            changeValues[valueExpression] = value;
            return;
        }

        if (Equals(value, oldValue))
        {
            return;
        }

        callback((T)oldValue!);
        changeValues[valueExpression] = value;
    }
}
