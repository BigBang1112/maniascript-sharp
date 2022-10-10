namespace ManiaScriptSharp;

public class ManiaScriptEventAttribute : Attribute
{
    public string EventList { get; }

    public ManiaScriptEventAttribute(string eventList)
    {
        EventList = eventList;
    }
}