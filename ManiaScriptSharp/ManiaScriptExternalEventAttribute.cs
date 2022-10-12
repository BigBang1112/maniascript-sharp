namespace ManiaScriptSharp;

public class ManiaScriptExternalEventAttribute : Attribute
{
    public string EventClass { get; }
    public string EventKind { get; }
    public string Identifier { get; }
    public string[]? Parameters { get; set; }
    
    public ManiaScriptExternalEventAttribute(string eventClass, string eventKind, string identifier)
    {
        EventClass = eventClass;
        EventKind = eventKind;
        Identifier = identifier;
    }
}