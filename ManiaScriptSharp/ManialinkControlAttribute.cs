namespace ManiaScriptSharp;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class ManialinkControlAttribute : Attribute
{
    public string? Id { get; }
    public bool IgnoreValidation { get; set; }

    public ManialinkControlAttribute()
    {
        
    }
    
    public ManialinkControlAttribute(string id)
    {
        Id = id;
    }
}