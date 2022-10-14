namespace ManiaScriptSharp.Generator;

public static class GenericExtensions
{
    public static IEnumerable<T> Flatten<T>(T obj, Func<T, T?> func)
    {
        var current = func(obj);

        while (current is not null)
        {
            yield return current;
            current = func(current);
        }
    }
}