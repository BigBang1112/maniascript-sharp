namespace ManiaScriptSharp;

public class ManiaScriptEventListAttribute : Attribute
{
    public string GeneralEventHandlerName { get; }
    
    public ManiaScriptEventListAttribute(string generalEventHandlerName)
    {
        GeneralEventHandlerName = generalEventHandlerName;
    }
}