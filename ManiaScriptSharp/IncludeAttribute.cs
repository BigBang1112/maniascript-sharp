namespace ManiaScriptSharp;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class IncludeAttribute : Attribute
{
	public IncludeAttribute(Type includeType)
	{

	}
}
