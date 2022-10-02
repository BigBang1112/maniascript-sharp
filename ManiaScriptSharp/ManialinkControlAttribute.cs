namespace ManiaScriptSharp;

public class ManialinkControlAttribute : Attribute
{
    public string Id { get; }
    
    public ManialinkControlAttribute(string id)
    {
        Id = id;
    }
}