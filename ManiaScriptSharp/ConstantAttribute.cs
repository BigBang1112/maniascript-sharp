namespace ManiaScriptSharp;

[AttributeUsage(AttributeTargets.Field)]
public class ConstantAttribute : Attribute
{
    public bool IncludePrefix { get; set; } = true;
}
