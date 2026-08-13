namespace ManiaScriptSharp;

/// <summary>Marks a const or readonly field as a ManiaScript <c>#Setting</c>.</summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class SettingAttribute : Attribute
{
    /// <summary>Display name for the setting. If null, name is auto-translated from the field name.</summary>
    public string? As { get; set; }

    /// <summary>If true (default), the display name is wrapped in <c>_()</c> for translation.</summary>
    public bool Translated { get; set; } = true;

    /// <summary>When changed, sets a <c>Reload</c> field to true. Requires a <c>bool Reload</c> field on the class.</summary>
    public bool ReloadOnChange { get; set; }

    /// <summary>When changed, calls the method with the given name. Use <c>nameof()</c>.</summary>
    public string? CallOnChange { get; set; }
}
