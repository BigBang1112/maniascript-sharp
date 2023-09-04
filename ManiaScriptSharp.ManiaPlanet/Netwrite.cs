namespace ManiaScriptSharp;

/// <summary>
/// Declaration mode for a variable that lives through the network inside an object (write access).
/// </summary>
/// <typeparam name="T">Type of the variable.</typeparam>
[DeclarationMode("netwrite")]
public class Netwrite<T>
{
    private T value;
    private readonly object @object;

    private Netwrite(object @object)
    {
        this.@object = @object;
    }

    public void Set(T value)
    {
        this.value = value;
    }

    public T Get()
    {
        return value;
    }

    public static Netwrite<T> For(CTeam team)
    {
        return new Netwrite<T>(team);
    }

    public static Netwrite<T> For(CScore score)
    {
        return new Netwrite<T>(score);
    }

    public static Netwrite<T> For(CUIConfig ui)
    {
        return new Netwrite<T>(ui);
    }

    public static Netwrite<T> For(CTmPlayer player)
    {
        return new Netwrite<T>(player);
    }

    public static implicit operator T(Netwrite<T> netwrite)
    {
        return default!;
    }

    public static Netwrite<T> operator -(Netwrite<T> netwrite, T value)
    {
        return default!;
    }
}