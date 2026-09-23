using System.Collections.Generic;
using ManiaScriptSharp;

namespace MyMode;

public class EventTest : ILib
{
    public struct Event
    {
        public int Id;
        public string Name;
    }

    private IList<Event> _events = [];
    public IList<Event> Events
    {
        get
        {
            var tempEvents = _events;
            _events = [];
            return tempEvents;
        }
    }

    private void Enqueue(Event e)
    {


        _events.Add(new Event { Id = e.Id, Name = e.Name });
    }
}