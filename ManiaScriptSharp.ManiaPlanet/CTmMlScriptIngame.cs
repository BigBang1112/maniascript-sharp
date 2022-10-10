using System.Collections.Immutable;

namespace ManiaScriptSharp;

public partial class CTmMlScriptIngame
{
    [ManiaScriptEvent(nameof(RaceEvents))]
    public delegate void RaceEventHandler(CTmRaceClientEvent e);
    
    [ManiaScriptEventList(nameof(RaceEventHandler))]
    public IList<CTmRaceClientEvent> RaceEvents { get; }
}
