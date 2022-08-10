namespace ManiaScriptSharp;

[AttributeUsage(AttributeTargets.All)]
public class ActualNameAttribute : Attribute
{
    public string Name { get; }

    public ActualNameAttribute(string name)
    {
        Name = name;
    }
}
