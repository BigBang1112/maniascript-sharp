namespace ManiaScriptSharp;

[AttributeUsage(AttributeTargets.Class)]
public class IgnoreGeneratedAttribute : Attribute
{
    public string? NameOfTypeOrProperty { get; }
    
    public IgnoreGeneratedAttribute()
	{

    }
    
    public IgnoreGeneratedAttribute(string nameOfTypeOrProperty)
    {
        NameOfTypeOrProperty = nameOfTypeOrProperty;
    }
}
