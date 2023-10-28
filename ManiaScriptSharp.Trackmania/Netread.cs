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

    public static Netread<T> For(CScore score)
    {
        return new Netread<T>(default!, score);
    }

    public static Netread<T> For(CUIConfig ui)
    {
        return new Netread<T>(default!, ui);
    }

    public static Netread<T> For(CTmPlayer player)
    {
        return new Netread<T>(default!, player);
    }

    public static Netread<T> For(CTmMlPlayer player)
    {
        return new Netread<T>(default!, player);
    }
}