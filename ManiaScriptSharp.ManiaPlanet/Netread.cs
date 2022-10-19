namespace ManiaScriptSharp;

/// <summary>
/// Declaration mode for a variable that lives through the network inside an object (read access).
/// </summary>
/// <typeparam name="T">Type of the variable.</typeparam>
[DeclarationMode("netread")]
public class Netread<T>
{
    private readonly T value;
    private readonly object @object;

    private Netread(T value, object @object)
    {
        this.value = value;
        this.@object = @object;
    }

    public T Get()
    {
        return value;
    }

    public static Netread<T> For(CTeam team)
    {
        return new Netread<T>(default!, team);
    }
}