namespace ManiaScriptSharp;

/// <summary>
/// Declaration mode for a variable that lives inside an object for its lifetime.
/// </summary>
/// <typeparam name="T">Type of the variable.</typeparam>
[DeclarationMode("")]
public class Local<T>
{
    private T value;
    private readonly object @object;

    private Local(T value, object @object)
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

    public static Local<T> For(CUser user)
    {
        return new Local<T>(default!, user);
    }

    public static Local<T> For(CTeam team)
    {
        return new Local<T>(default!, team);
    }
}
