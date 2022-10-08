namespace ManiaScriptSharp;

public partial class CTmMode
{
    public delegate void PendingEventHandler(CTmModeEvent e);
    public delegate void StartLineEventHandler(CTmPlayer player);
    
    [ManiaScriptEventList(nameof(PendingEventHandler))]
    public IList<CTmModeEvent> PendingEvents { get; }
}