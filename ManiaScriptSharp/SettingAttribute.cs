namespace ManiaScriptSharp;

[AttributeUsage(AttributeTargets.Field)]
public class SettingAttribute : Attribute
{
	public string? As { get; set; }
    public bool Translated { get; set; } = true;
}
