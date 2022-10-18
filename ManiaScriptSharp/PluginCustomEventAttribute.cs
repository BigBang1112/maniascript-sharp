namespace ManiaScriptSharp;

[AttributeUsage(AttributeTargets.Method)]
public class PluginCustomEventAttribute : Attribute
{
    public string Type { get; }

    public PluginCustomEventAttribute(string type)
    {
        Type = type;
    }
}