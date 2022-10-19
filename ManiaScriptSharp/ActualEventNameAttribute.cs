namespace ManiaScriptSharp;

[AttributeUsage(AttributeTargets.Delegate)]
public class ActualEventNameAttribute : Attribute
{
    public string EventName { get; }

    public ActualEventNameAttribute(string eventName)
    {
        EventName = eventName;
    }
}