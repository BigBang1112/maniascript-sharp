namespace ManiaScriptSharp;

public class ActualEventNameAttribute : Attribute
{
    public string EventName { get; }

    public ActualEventNameAttribute(string eventName)
    {
        EventName = eventName;
    }
}