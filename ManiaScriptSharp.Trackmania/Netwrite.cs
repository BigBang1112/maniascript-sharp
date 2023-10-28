namespace ManiaScriptSharp;

/// <summary>
/// Declaration mode for a variable that lives through the network inside an object (write access).
/// </summary>
/// <typeparam name="T">Type of the variable.</typeparam>
[DeclarationMode("netwrite")]
public class Netwrite<T>
{
    private readonly object @object;

    private Netwrite(object @object)
    {
        this.@object = @object;
    }

    public void Set(T value)
    {
        
    }

    public static Netwrite<T> For(CTeam team)
    {
        return new Netwrite<T>(team);
    }
}