namespace ManiaScriptSharp;

[AttributeUsage(AttributeTargets.Class)]
public class DeclarationModeAttribute : Attribute
{
    public string Name { get; }
    
    public DeclarationModeAttribute(string name)
	{
		Name = name;
	}
}
