namespace ManiaScriptSharp;

/// <summary>
/// Declaration mode for a variable that lives inside a Gbx file as a contextual attribute.
/// </summary>
/// <typeparam name="T">Type of the variable.</typeparam>
[DeclarationMode("metadata")]
public class Metadata<T>
{
    private T value;
    private readonly object @object;

    private Metadata(T value, object @object)
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

    public static Metadata<T> For(CMap map)
    {
        return new Metadata<T>(default!, map);
    }
    
    public static Metadata<T> For(CMacroblockModel macroblock)
    {
        return new Metadata<T>(default!, macroblock);
    }
}