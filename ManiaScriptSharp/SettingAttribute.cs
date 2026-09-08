namespace ManiaScriptSharp;

[AttributeUsage(AttributeTargets.Field)]
public class SettingAttribute : Attribute
{
	public string? As { get; set; }
    public bool Translated { get; set; } = true;
    public bool Hidden { get; set; }
    public bool ReloadOnChange { get; set; }
    public string? CallOnChange { get; set; }
}
