namespace ManiaScriptSharp;

public partial class CMlControl
{
    [ManiaScriptExternalEvent(
        eventContext: nameof(CMlScript),
        eventList: nameof(CMlScript.PendingEvents),
        eventKind: nameof(CMlScriptEvent.Type.MouseClick),
        identifier: nameof(CMlScriptEvent.Control))]
    public event Action? MouseClick;
    
    [ManiaScriptExternalEvent(
        eventContext: nameof(CMlScript),
        eventList: nameof(CMlScript.PendingEvents),
        eventKind: nameof(CMlScriptEvent.Type.MouseOver),
        identifier: nameof(CMlScriptEvent.Control))]
    public event Action? MouseOver;
    
    [ManiaScriptExternalEvent(
        eventContext: nameof(CMlScript),
        eventList: nameof(CMlScript.PendingEvents),
        eventKind: nameof(CMlScriptEvent.Type.MouseOut),
        identifier: nameof(CMlScriptEvent.Control))]
    public event Action? MouseOut;
}