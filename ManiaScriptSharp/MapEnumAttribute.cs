namespace ManiaScriptSharp;

[AttributeUsage(AttributeTargets.Class)]
public class MappedFromAttribute : Attribute
{
    public string NameOfMappedFrom { get; }

    public MappedFromAttribute(string nameOfMappedFrom)
	{
        NameOfMappedFrom = nameOfMappedFrom;
    }
}
