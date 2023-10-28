namespace ManiaScriptSharp;

public partial class CTmMlScriptIngame
{
    [ManiaScriptEvent(nameof(RaceEvents))]
    public delegate void RaceEventHandler(CTmRaceClientEvent e);
}
