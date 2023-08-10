namespace ManiaScriptSharp;

[AttributeUsage(AttributeTargets.Delegate)]
public class ManiaScriptEventAttribute : Attribute
{
    public string EventList { get; }

    public ManiaScriptEventAttribute(string eventList)
    {
        EventList = eventList;
    }
}