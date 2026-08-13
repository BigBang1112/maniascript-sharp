namespace ManiaScriptSharp;

/// <summary>Binds a field to a manialink control by id (<c>Page.GetFirstChild</c>).</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ManialinkControlAttribute : Attribute
{
    public string? Id { get; }
    public bool IgnoreValidation { get; set; }
    public ManialinkControlAttribute() { }
    public ManialinkControlAttribute(string id) { Id = id; }
}
