namespace ManiaScriptSharp;

/// <summary>Preserves the official ManiaScript name of an API enum renamed for C#.</summary>
[AttributeUsage(AttributeTargets.Enum)]
public sealed class ManiaScriptNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
