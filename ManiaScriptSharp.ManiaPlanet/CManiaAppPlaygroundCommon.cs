namespace ManiaScriptSharp;

public partial class CManiaAppPlaygroundCommon
{
    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void PendingEventHandler(CManiaAppPlaygroundEvent e);
}
