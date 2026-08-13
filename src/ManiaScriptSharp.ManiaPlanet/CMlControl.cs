namespace ManiaScriptSharp;

public partial class CMlControl
{
    [ManiaScriptExternalEvent(
        eventContext: nameof(CMlScript),
        eventList: nameof(CMlScript.PendingEvents),
        eventType: nameof(CMlScriptEvent.Type.MouseClick),
        identifier: nameof(CMlScriptEvent.Control))]
    public event CMlScript.MouseClickEventHandler? MouseClick;

    [ManiaScriptExternalEvent(
        eventContext: nameof(CMlScript),
        eventList: nameof(CMlScript.PendingEvents),
        eventType: nameof(CMlScriptEvent.Type.MouseOver),
        identifier: nameof(CMlScriptEvent.Control))]
    public event CMlScript.MouseOverEventHandler? MouseOver;

    [ManiaScriptExternalEvent(
        eventContext: nameof(CMlScript),
        eventList: nameof(CMlScript.PendingEvents),
        eventType: nameof(CMlScriptEvent.Type.MouseOut),
        identifier: nameof(CMlScriptEvent.Control))]
    public event CMlScript.MouseOutEventHandler? MouseOut;
}
