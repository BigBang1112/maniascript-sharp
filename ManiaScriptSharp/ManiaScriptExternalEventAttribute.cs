namespace ManiaScriptSharp;

[AttributeUsage(AttributeTargets.Event)]
public class ManiaScriptExternalEventAttribute : Attribute
{
    /// <summary>
    /// Contextual script class name to which the event should be attached.
    /// </summary>
    public string EventContext { get; }
    
    /// <summary>
    /// An event list from the <see cref="EventContext"/> class.
    /// </summary>
    public string EventList { get; }
    
    /// <summary>
    /// Type of the event (taken from its enum value) used to determine the delegate.
    /// </summary>
    public string EventKind { get; }
    
    /// <summary>
    /// The name of the property from the event class that should be used for comparison.
    /// </summary>
    public string Identifier { get; }
    
    /// <summary>
    /// An optional name of the property applied on the class that contains this attribute used for the other side of the comparison.
    /// </summary>
    public string? Selector { get; set; }
    
    public string[]? Parameters { get; set; }
    
    public ManiaScriptExternalEventAttribute(string eventContext, string eventList, string eventKind, string identifier)
    {
        EventContext = eventContext;
        EventList = eventList;
        EventKind = eventKind;
        Identifier = identifier;
    }
}