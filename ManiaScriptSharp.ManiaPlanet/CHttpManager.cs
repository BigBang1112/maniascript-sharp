namespace ManiaScriptSharp;

public partial class CHttpManager
{
    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void RequestCompleteEventHandler(CHttpRequest request);
}