namespace ManiaScriptSharp;

/// <summary>
/// Declaration mode for a variable that lives in a persistent storage (64kB max).
/// </summary>
/// <typeparam name="T">Type of the variable.</typeparam>
[DeclarationMode("persistent")]
public class Persistent<T>
{
    private T value;
    private readonly object @object;

    private Persistent(T value, object @object)
    {
        this.value = value;
        this.@object = @object;
    }

    public T Get()
    {
        return value;
    }

    public void Set(T value)
    {
        this.value = value;
    }

    public static Persistent<T> For(CUser user)
    {
        return new Persistent<T>(default!, user);
    }
}